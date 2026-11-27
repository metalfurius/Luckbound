#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>
    /// C1 physical-clear to E1 V7 candidate builder. It does not commit, retry, or publish; its
    /// caller must retain the authenticated session owner, mutation gate, pause lease, and this
    /// exact candidate until repository confirmation/readback and actor publication complete.
    /// </summary>
    public sealed partial class ApplicationBootstrapper
    {
        private const string F10V7E1NodeId = "sample.patio.exploration";
        private const string F10V7E1TemplateId = "room.patio.e1";

        /// <summary>
        /// Prepares a detached candidate from the current published session. The caller must first
        /// retain the mutation gate and simulation lease, then obtain capturedCommonState from the
        /// live V7 actor binder under that lease. This method only checks the capture's allowed
        /// shape; the object alone is not proof of actor capture, write confirmation, or authority.
        /// </summary>
        internal bool TryPrepareF10V7CombatAdvanceCandidate(
            F10V7SessionInitializationBinding sessionBinding, ExpeditionGameplayComposition owner,
            RoomEncounterRuntimeContext context,
            RoomEncounterRuntime runtime, RoomEncounterClearObservation clearObservation,
            ExpeditionSaveData capturedCommonState, object gateOwnerToken,
            out F10V7CombatAdvanceCandidate candidate,
            out ExpeditionRoomTransitionController.PreparedRoomTransition reservationForCleanup,
            out string error)
        {
            candidate = null;
            reservationForCleanup = null;
            error = string.Empty;
            ExpeditionRoomTransitionController.PreparedRoomTransition preparedTransition = null;
            if (sessionBinding == null || owner == null ||
                context == null || runtime == null || clearObservation == null || capturedCommonState == null)
                return FailF10RoomAdvance("The published V7 session, live C1 clear, and captured actor state are required.", out error);

            if (!TryValidateF10V7CombatOwner(sessionBinding, owner, context, gateOwnerToken,
                    out ExpeditionReadinessCompositionView readiness, out ExpeditionRoomTransitionController controller,
                    out long expectedRevision, out error)) return false;

            if (!TryGetCurrentV7PublishedSnapshot(sessionBinding,
                    out F10V7ExpeditionReadProjection publishedProjection,
                    out string confirmedCurrentJson, out long publishedRevision) ||
                publishedProjection == null || string.IsNullOrWhiteSpace(confirmedCurrentJson) ||
                publishedRevision != expectedRevision || publishedRevision != ActiveExpeditionRevision ||
                !IsCurrentV7PublishedSession(sessionBinding, readiness.ReadinessRegistration, publishedRevision))
                return FailF10RoomAdvance("C1 payment requires the exact current ConfirmedJson and projection retained by this published V7 session revision.", out error);

            SaveRepository.F10V7SessionHandle handle = sessionBinding.SessionHandle;
            if (handle == null || !ReferenceEquals(handle.Repository, _saveRepository) ||
                !ReferenceEquals(handle.ItemCatalog, sessionBinding.AdmissionItemCatalog) ||
                !ReferenceEquals(handle.Registry, sessionBinding.AdmissionRegistry) ||
                !handle.IsBoundTo(_saveRepository, handle.ExactJson, handle.RunId,
                    sessionBinding.AdmissionItemCatalog, sessionBinding.AdmissionRegistry) ||
                !_saveRepository.IsRetainedF10V7SessionHandle(handle, handle.ExactJson, handle.RunId,
                    sessionBinding.AdmissionItemCatalog, sessionBinding.AdmissionRegistry))
                return FailF10RoomAdvance("The current room is not owned by the retained authenticated V7 handle and its original catalog pin.", out error);

            ProfileSaveEnvelopeV7 openingEnvelope;
            ProfileSaveEnvelopeV7 currentEnvelope;
            try
            {
                if (!SaveV7JsonCodec.TryDecodeProfile(StrictV7Utf8.GetBytes(sessionBinding.AdmittedJson),
                        out openingEnvelope, out SaveV7Error openingError))
                    return FailF10RoomAdvance("The retained admission JSON no longer authenticates: " + openingError.Code + ": " + openingError.Message, out error);
                if (!SaveV7JsonCodec.TryDecodeProfile(StrictV7Utf8.GetBytes(confirmedCurrentJson),
                        out currentEnvelope, out SaveV7Error currentError))
                    return FailF10RoomAdvance("The supplied current confirmed JSON does not authenticate: " + currentError.Code + ": " + currentError.Message, out error);
            }
            catch (Exception exception) when (exception is EncoderFallbackException || exception is ArgumentException)
            { return FailF10RoomAdvance("A V7 source JSON is not strict UTF-8: " + exception.Message, out error); }

            if (openingEnvelope == null || openingEnvelope.activeExpedition == null ||
                currentEnvelope == null || currentEnvelope.activeExpedition == null ||
                currentEnvelope.contentVersion != 7 ||
                !HasSameF10V7RoomProfile(openingEnvelope.profile, currentEnvelope.profile) ||
                !RunId.TryParse(currentEnvelope.activeExpedition.runId, out RunId currentRunId) ||
                currentRunId != handle.RunId ||
                !string.Equals(currentEnvelope.activeExpedition.currentNodeId,
                    F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal))
                return FailF10RoomAdvance("C1 payment requires the exact currently published C1 V7 snapshot with the original permanent profile intact; a current snapshot may equal the admitted JSON when the session is still at C1.", out error);

            ItemCatalog itemCatalog = sessionBinding.AdmissionItemCatalog;
            EncounterCatalogRegistryV1 registry = sessionBinding.AdmissionRegistry;
            if (itemCatalog == null || registry == null)
                return FailF10RoomAdvance("The original V7 item catalog and encounter registry are unavailable.", out error);

            if (!TryClassifyF10V7CombatCurrentSnapshot(sessionBinding.PublishedRevision,
                    publishedRevision, sessionBinding.AdmittedJson, handle.ExactJson,
                    confirmedCurrentJson, out bool openingRollback, out string sourceBranchError))
                return FailF10RoomAdvance(sourceBranchError, out error);

            ExpeditionRunConfiguration exactRun;
            ExpeditionSaveDataV7 currentState;
            ResolvedEncounterPlanV1 currentPlan;
            SaveV7Error restoreError;
            if (openingRollback)
            {
                if (!SaveV7CandidateMapper.TryRestore(currentEnvelope, itemCatalog, registry,
                        out exactRun, out ExpeditionSaveDataV7 envelopeRestoredState,
                        out currentPlan, out restoreError) || envelopeRestoredState == null)
                    return FailF10RoomAdvance(restoreError.Code == SaveV7ErrorCode.None
                        ? "The exact opening C1 envelope did not restore through its authenticated checkpoint."
                        : restoreError.Code + ": " + restoreError.Message, out error);
                if (!TrySelectF10V7CombatBaseline(openingRollback: true,
                        sessionBinding.Projection.RestoredStateV7, handle.BaselineState,
                        envelopeRestoredState, out currentState) ||
                    !StatesEqual(currentState, envelopeRestoredState) ||
                    !StatesEqual(currentState, publishedProjection.RestoredStateV7) ||
                    !V7ProjectionsEqual(sessionBinding.Projection, publishedProjection))
                    return FailF10RoomAdvance("Direct C1 reopening did not retain the exact opening rollback baseline in its handle, binding, and published projection.", out error);
            }
            else
            {
                if (!SaveV7CandidateMapper.TryRestoreCurrentForSettlement(currentEnvelope, itemCatalog, registry,
                        out exactRun, out ExpeditionSaveDataV7 confirmedCurrentState,
                        out currentPlan, out restoreError) || confirmedCurrentState == null)
                    return FailF10RoomAdvance(restoreError.Code == SaveV7ErrorCode.None
                        ? "The current confirmed C1 envelope did not restore through the retained V7 catalog and pin."
                        : restoreError.Code + ": " + restoreError.Message, out error);
                if (!TrySelectF10V7CombatBaseline(openingRollback: false, null, handle.BaselineState,
                        confirmedCurrentState, out currentState) ||
                    currentState == null)
                    return FailF10RoomAdvance("The post-admission C1 baseline could not be selected from its exact confirmed state.", out error);
                if (!F10V7ExpeditionReadProjector.TryCreateCurrent(currentEnvelope, itemCatalog, registry,
                        out F10V7ExpeditionReadProjection confirmedProjection, out SaveV7Error projectionError) ||
                    confirmedProjection == null || !V7ProjectionsEqual(confirmedProjection, publishedProjection) ||
                    !StatesEqual(confirmedProjection.SourceStateV7, currentState) ||
                    !StatesEqual(confirmedProjection.RestoredStateV7, currentState))
                    return FailF10RoomAdvance(projectionError.Code == SaveV7ErrorCode.None
                        ? "The post-admission C1 candidate is not the exact confirmed current state published by this owner revision."
                        : projectionError.Code + ": " + projectionError.Message, out error);
            }

            if (exactRun == null || currentState == null || currentPlan == null ||
                exactRun.RunId != handle.RunId || currentPlan.Mode != EncounterCatalogBindingMode.Encounter ||
                !string.Equals(currentPlan.NodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                !ReferenceEquals(exactRun.Inventory?.Catalog, itemCatalog))
                return FailF10RoomAdvance(restoreError.Code == SaveV7ErrorCode.None
                    ? "The selected C1 source did not restore through the original V7 catalog and pin."
                    : restoreError.Code + ": " + restoreError.Message, out error);

            if (!sessionBinding.Run.RunId.Equals(exactRun.RunId) ||
                !string.Equals(sessionBinding.Run.Route?.CanonicalForm, exactRun.Route?.CanonicalForm, StringComparison.Ordinal) ||
                !ReferenceEquals(ActiveRun, readiness.Run) || ActiveRun == null ||
                ActiveRun.RunId != exactRun.RunId || ActiveRun.Seed != exactRun.Seed ||
                !string.Equals(ActiveRun.ClassId, exactRun.ClassId, StringComparison.Ordinal) ||
                !string.Equals(ActiveRun.RaceId, exactRun.RaceId, StringComparison.Ordinal) ||
                ActiveRun.Inventory == null || readiness.ItemCollector == null ||
                !ReferenceEquals(readiness.ItemCollector.Inventory, ActiveRun.Inventory) ||
                !ReferenceEquals(ActiveRun.Inventory.Catalog, itemCatalog))
                return FailF10RoomAdvance("The live V7 actor, collector, run identity, and retained item catalog no longer identify one owner.", out error);

            ExpeditionSaveData currentCommon = publishedProjection.CompositionReadOnlyView;
            if (currentCommon == null ||
                !StatesEqual(publishedProjection.RestoredStateV7, currentState) ||
                !StatesEqual(currentCommon, ActiveExpedition) ||
                !StatesEqual(capturedCommonState, OverlayCapturedF10V7RoomResources(
                    currentCommon, capturedCommonState)))
                return FailF10RoomAdvance("The selected published C1 baseline or actor capture shape differs from the active C1 session snapshot.", out error);

            if (!TryValidateF10RunAndRoute(exactRun, currentCommon, context, controller,
                    out ExpeditionRouteNode sourceNode, out string targetNodeId, out string routeError) ||
                !string.Equals(targetNodeId, F10V7E1NodeId, StringComparison.Ordinal) ||
                !string.Equals(sourceNode.TemplateId, F10TechnicalEncounterPlanProvider.C1TemplateId, StringComparison.Ordinal))
                return FailF10RoomAdvance(string.IsNullOrEmpty(routeError)
                    ? "The authenticated C1 source must advance to its fixed E1 route edge."
                    : routeError, out error);
            if (!TryValidateF10CombatClear(exactRun, context, runtime, clearObservation, out error)) return false;

            ExpeditionEncounterPlanProjection livePlan;
            try { livePlan = ExpeditionEncounterPlanProjection.Create(currentPlan); }
            catch (Exception exception)
            { return FailF10RoomAdvance("The pinned current C1 plan could not be projected: " + exception.Message, out error); }
            string sourcePlanSha256;
            try { sourcePlanSha256 = ResolvedEncounterPlanCanonicalV1.ComputeSha256(currentPlan); }
            catch (Exception exception)
            { return FailF10RoomAdvance("The exact current C1 plan digest could not be computed: " + exception.Message, out error); }
            if (!SaveV7CandidateMapper.TryToDomainPin(currentState.encounterPlanPin,
                    out EncounterPlanPinV1 sourcePin, out SaveV7Error sourcePinError) ||
                sourcePin == null || sourcePin.Entry == null ||
                !string.Equals(sourcePin.Entry.NodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(sourcePin.Entry.ResolvedPlanSha256, sourcePlanSha256, StringComparison.Ordinal))
                return FailF10RoomAdvance(sourcePinError.Code != SaveV7ErrorCode.None
                    ? sourcePinError.Code + ": " + sourcePinError.Message
                    : "The current resolved C1 plan digest does not match the exact current V7 pin entry.", out error);
            ExpeditionRoomCheckpointSaveDataV7 sourceCheckpoint = null;
            SaveV7Error checkpointError = default(SaveV7Error);
            bool decodedSourceCheckpoint = !string.IsNullOrWhiteSpace(currentState.roomEntryCheckpointJson) &&
                SaveV7JsonCodec.TryDecodeCheckpoint(StrictV7Utf8.GetBytes(currentState.roomEntryCheckpointJson),
                    out sourceCheckpoint, out checkpointError);
            if (!decodedSourceCheckpoint ||
                sourceCheckpoint == null || sourceCheckpoint.encounterEntry == null ||
                !string.Equals(sourceCheckpoint.roomNodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(sourceCheckpoint.encounterEntry.nodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(sourceCheckpoint.encounterEntry.resolvedPlanSha256, sourcePlanSha256, StringComparison.Ordinal))
                return FailF10RoomAdvance(checkpointError.Code != SaveV7ErrorCode.None
                    ? checkpointError.Code + ": " + checkpointError.Message
                    : "The current C1 checkpoint entry does not match the exact resolved plan digest.", out error);

            // The accepted C1 activation currently has no private App-issued binding that joins
            // this exact restored plan to the exact adapted RuntimePlan and measured actor assets.
            // A roster equality check cannot establish that provenance. Keep payment-candidate
            // construction fail-closed until the owner-side factory retains and presents that
            // binding; do not let a caller-supplied plan digest or matching roster act as authority.
            if (context.Plan == null || context.Plan.Roster == null || livePlan.Roster == null ||
                !SameF10Roster(context.Plan.Roster, livePlan.Roster))
                return FailF10RoomAdvance("The live C1 runtime roster does not match the current V7 pinned plan projection.", out error);
            if (!HasAuthenticatedF10V7CombatPlanRuntimeBinding(sessionBinding, owner, context,
                    runtime, publishedRevision, gateOwnerToken))
                return FailF10RoomAdvance("C1 payment candidate is unavailable until the current App owner supplies its private plan/runtime/asset binding for this exact published session revision.", out error);
            if (!HasAuthenticatedF10V7CombatResourceCapture(sessionBinding, owner, readiness,
                    context, capturedCommonState, ActiveRun.Inventory, publishedRevision))
                return FailF10RoomAdvance("C1 payment candidate is unavailable until the current App owner supplies an actor-binder capture produced under its retained simulation lease and revision.", out error);

            if (!TryBuildF10CombatRewardRequest(exactRun, currentCommon, sourceNode,
                    context, runtime, clearObservation,
                    out ExpeditionEncounterRewardRequest rewardRequest, out error)) return false;
            ExpeditionEncounterRewardResolution resolution;
            try { resolution = ExpeditionEncounterRewardPolicy.Resolve(rewardRequest); }
            catch (Exception exception)
            { return FailF10RoomAdvance("The deterministic C1 reward policy rejected its physical clear context: " + exception.Message, out error); }
            if (resolution == null || resolution.RoomPayout == null || resolution.ExplorationCacheReward != null ||
                resolution.OfferRequirement != null)
                return FailF10RoomAdvance("The C1 reward policy returned a cache, offer, or missing room payout.", out error);

            ExpeditionInventory candidateInventory;
            if (!TryCloneF10V7LiveCombatInventory(itemCatalog, ActiveRun.Inventory,
                    capturedCommonState.inventorySnapshotJson, out candidateInventory,
                    out string expectedInventoryJson, out error)) return false;

            ExpeditionSaveData candidateCommon = capturedCommonState.Clone();
            if (!TryApplyF10PolicyRoomPayment(currentCommon, candidateCommon,
                    candidateInventory, resolution, combat: true, out error)) return false;
            candidateCommon.inventorySnapshotJson = JsonUtility.ToJson(candidateInventory.CreateSnapshot());
            candidateCommon.healthPotionCharges = ExpeditionSaveMapper.GetHealthPotionCharges(candidateInventory);

            string sourceRoomIdText = context.RoomInstanceId.ToString();
            string resultId = "room-result.v1:" + sourceRoomIdText;
            string resultJson = SerializeF10RoomResult(context.RoomInstanceId,
                F10TechnicalEncounterPlanProvider.C1NodeId, F10V7E1NodeId);
            ExpeditionSaveData advancedCommon;
            try
            {
                advancedCommon = ExpeditionSaveMapper.CompleteAndAdvanceRoom(candidateCommon,
                    resultId, resultJson, F10V7E1NodeId);
            }
            catch (Exception exception)
            { return FailF10RoomAdvance("The paid C1 result could not advance the composition view to E1: " + exception.Message, out error); }

            if (!SaveV7CandidateMapper.TryToDomainPin(currentState.encounterPlanPin, out EncounterPlanPinV1 currentPin, out SaveV7Error pinError) ||
                currentPin.RunId != exactRun.RunId || currentPin.Entry == null ||
                !string.Equals(currentPin.Entry.NodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                !registry.TryGetExact(currentPin.CatalogId, currentPin.CatalogRevision,
                    currentPin.CatalogSha256, out EncounterCatalogManifest manifest, out EncounterCatalogError catalogError))
                return FailF10RoomAdvance(pinError.Code != SaveV7ErrorCode.None
                    ? pinError.Code + ": " + pinError.Message
                    : "The current C1 pin no longer resolves to its exact retained catalog.", out error);
            if (!EncounterPlanResolverV1.TryResolve(manifest, exactRun, F10V7E1NodeId,
                    currentState.pactIds, out ResolvedEncounterPlanV1 e1Plan,
                    out EncounterPlanResolutionErrorV1 e1Error))
                return FailF10RoomAdvance(e1Error.Code + ": " + e1Error.Message, out error);

            EncounterPlanPinV1 nextPin;
            try { nextPin = EncounterPlanPinV1.Append(currentPin, e1Plan, F10V7E1NodeId); }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            { return FailF10RoomAdvance("The exact E1 encounter plan could not be appended to the current pin: " + exception.Message, out error); }

            ExpeditionSaveDataV7 nextState = SaveV7Clone.Clone(currentState);
            nextState.currentNodeId = advancedCommon.currentNodeId;
            nextState.visitedNodeIds = (string[])advancedCommon.visitedNodeIds.Clone();
            nextState.completedRoomIds = (string[])advancedCommon.completedRoomIds.Clone();
            nextState.completedRoomResults = advancedCommon.completedRoomResults.Select(result =>
                new ExpeditionRoomCompletionSaveDataV7
                {
                    roomNodeId = result.roomNodeId, resultId = result.resultId,
                    resultJson = result.resultJson
                }).ToArray();
            nextState.inventorySnapshotJson = advancedCommon.inventorySnapshotJson;
            nextState.maximumHealth = advancedCommon.maximumHealth;
            nextState.currentHealth = advancedCommon.currentHealth;
            nextState.maximumEnergy = advancedCommon.maximumEnergy;
            nextState.currentEnergy = advancedCommon.currentEnergy;
            nextState.healthPotionCharges = advancedCommon.healthPotionCharges;
            nextState.accumulatedRenown = advancedCommon.accumulatedRenown;
            nextState.claimedRewardIds = (string[])advancedCommon.claimedRewardIds.Clone();
            nextState.encounterPlanPin = SaveV7CandidateMapper.ToPinDto(nextPin);
            // Prepare writes the E1 checkpoint only after the real actor capture and C1 payment above.
            nextState.roomEntryCheckpointJson = null;

            PinnedProfileCandidateV7 pinnedCandidate;
            ExpeditionRunConfiguration candidateRun;
            try
            {
                pinnedCandidate = SaveV7CandidateMapper.Prepare(currentEnvelope.profile,
                    nextState, nextPin);
                candidateRun = exactRun.WithInventory(candidateInventory);
                ProfileSaveEnvelopeV7 candidateEnvelope = pinnedCandidate.CreateEnvelope(
                    checked(currentEnvelope.generation + 1), checked(currentEnvelope.sequence + 1),
                    DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                if (!SaveV7CandidateMapper.TryValidateCommit(candidateEnvelope, candidateRun, registry,
                        out SaveV7Error validationError) ||
                    !SaveV7CandidateMapper.TryValidateTransition(currentEnvelope, null,
                        candidateEnvelope, out validationError))
                    return FailF10RoomAdvance(validationError.Code + ": " + validationError.Message, out error);
                if (!HasSameF10V7RoomProfile(currentEnvelope.profile,
                        candidateEnvelope.profile))
                    return FailF10RoomAdvance("The C1 room candidate changed the complete permanent V7 profile.", out error);
                if (!string.Equals(candidateEnvelope.activeExpedition.currentNodeId, F10V7E1NodeId, StringComparison.Ordinal) ||
                    candidateEnvelope.activeExpedition.encounterPlanPin.entry == null ||
                    !string.Equals(candidateEnvelope.activeExpedition.encounterPlanPin.entry.nodeId,
                        F10V7E1NodeId, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(candidateEnvelope.activeExpedition.roomEntryCheckpointJson))
                    return FailF10RoomAdvance("The encoded V7 candidate lacks the exact E1 pin entry or post-capture checkpoint.", out error);

                // Composition must consume the same post-payment E1 state and checkpoint
                // that the V7 candidate will persist, rather than the pre-advance C1 view.
                if (!SaveV7JsonCodec.TryDecodeProfile(SaveV7JsonCodec.EncodeProfile(candidateEnvelope),
                        out ProfileSaveEnvelopeV7 authenticatedCandidateEnvelope,
                        out SaveV7Error authenticationError))
                    return FailF10RoomAdvance(authenticationError.Code + ": " + authenticationError.Message, out error);
                if (!F10V7ExpeditionReadProjector.TryCreateCurrent(authenticatedCandidateEnvelope,
                        itemCatalog, registry, out F10V7ExpeditionReadProjection preparedProjection,
                        out SaveV7Error projectionError))
                    return FailF10RoomAdvance(projectionError.Code + ": " + projectionError.Message, out error);

                bool transitionPrepared = controller.TryPrepareTransition(context.RoomInstanceId,
                    F10V7E1NodeId, out preparedTransition);
                // Transfer the handle to the gateway before constructing the candidate. If
                // construction or later cleanup throws, the owner can retain this reservation.
                reservationForCleanup = preparedTransition;
                if (!transitionPrepared)
                    return FailF10RoomAdvance("The exact E1 room could not be reserved before the V7 commit.", out error);

                candidate = new F10V7CombatAdvanceCandidate(sessionBinding, handle,
                    confirmedCurrentJson, expectedRevision, owner, controller, preparedTransition, context, runtime,
                    clearObservation, currentPlan, e1Plan, rewardRequest, resolution,
                    resultId, resultJson, expectedInventoryJson, candidateInventory,
                    candidateRun, preparedProjection.CompositionReadOnlyView, pinnedCandidate);
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is FormatException ||
                exception is OverflowException || exception is InvalidOperationException ||
                exception is SaveV7ValidationException)
            {
                reservationForCleanup = preparedTransition;
                return FailF10RoomAdvance("The authenticated C1-to-E1 V7 candidate is invalid: " + exception.Message, out error);
            }
        }

        private bool TryValidateF10V7CombatOwner(F10V7SessionInitializationBinding binding,
            ExpeditionGameplayComposition owner, RoomEncounterRuntimeContext context,
            out ExpeditionReadinessCompositionView readiness,
            out ExpeditionRoomTransitionController controller, out long expectedRevision,
            out string error)
        {
            return TryValidateF10V7CombatOwner(binding, owner, context, null,
                out readiness, out controller, out expectedRevision, out error);
        }

        private bool TryValidateF10V7CombatOwner(F10V7SessionInitializationBinding binding,
            ExpeditionGameplayComposition owner, RoomEncounterRuntimeContext context,
            object gateOwnerToken, out ExpeditionReadinessCompositionView readiness,
            out ExpeditionRoomTransitionController controller, out long expectedRevision,
            out string error)
        {
            readiness = null;
            controller = null;
            expectedRevision = _activeExpeditionRevision;
            error = string.Empty;
            bool exactPaymentGate = gateOwnerToken != null &&
                IsExactF10V7C1PaymentGateOwner(owner, gateOwnerToken);
            if (gateOwnerToken != null && !exactPaymentGate)
                return FailF10RoomAdvance(
                    "C1 V7 payment revalidation requires the exact App-private gate owner token.", out error);
            if (exactPaymentGate &&
                HasPendingSessionMutationOutsideExactF10V7C1PaymentGate(owner, gateOwnerToken))
                return FailF10RoomAdvance(
                    "Another App session mutation is pending while the exact C1 payment gate is held.", out error);
            if (!Application.isPlaying || !ReferenceEquals(ApplicationBootstrapper.Instance, this) ||
                binding == null || owner == null || !owner.isActiveAndEnabled || !owner.IsReady ||
                !ReferenceEquals(_f10V7PublishedSession?.Binding, binding) ||
                !binding.IsOwnedBy(this, owner) || !IsCurrentV7SessionInitializationBinding(binding, owner) ||
                !_f10V7AdmissionProfileWriteBlock || !SaveDataReady || _saveRepository == null ||
                !ReferenceEquals(binding.AdmissionRepository, _saveRepository) ||
                !ReferenceEquals(_expeditionSimulationPauseLeaseProviderOwner, owner) ||
                _expeditionSimulationPauseLeaseProvider == null || ActiveRun == null || _activeExpedition == null ||
                _activeExpeditionRevision == long.MaxValue || HasPendingSettlement || HasPendingContractProgression ||
                HasPendingCraftingWrite || HasPendingEconomyMutation || HasPendingPermanentUpgradePurchase ||
                HasPendingRoomAdvance || _pendingExpeditionCandidate != null ||
                _expeditionSessionMutationPublicationOwner != null ||
                _expeditionSessionStateCapture != null || _expeditionSessionMutationPreparer != null)
                return FailF10RoomAdvance("C1 V7 payment requires the current private session owner and an otherwise empty mutation lane.", out error);

            readiness = owner.CaptureReadinessCompositionView();
            ExpeditionSessionReadinessRegistration registration = readiness?.ReadinessRegistration;
            controller = owner.RoomTransitions;
            ExpeditionRoomActivationRegistration activation = registration?.CurrentActivation;
            if (readiness == null || !readiness.IsReady || !ReferenceEquals(readiness.Owner, owner) ||
                !ReferenceEquals(readiness.Application, this) || !ReferenceEquals(readiness.Run, ActiveRun) ||
                !ReferenceEquals(readiness.PlayerMotor, owner.PlayerMotor) || controller == null ||
                !ReferenceEquals(readiness.RoomTransitions, controller) || controller.IsDisposed ||
                activation == null || !registration.IsCurrentActivation(activation) ||
                activation.State != ExpeditionRoomActivationState.Bound ||
                !ReferenceEquals(activation.Controller, controller) ||
                !ReferenceEquals(activation.Scope, controller.ActiveScope) ||
                !ReferenceEquals(activation.Context, context) || context.Controller != controller ||
                !ReferenceEquals(context.Scope, controller.ActiveScope) || !context.IsCurrentRoomScope() ||
                controller.CurrentRoomId != context.RoomInstanceId ||
                !string.Equals(controller.CurrentNodeId, F10TechnicalEncounterPlanProvider.C1NodeId, StringComparison.Ordinal) ||
                _activeExpeditionRevision != expectedRevision)
                return FailF10RoomAdvance("The published V7 actor, readiness activation, and live C1 room scope no longer identify one owner.", out error);
            return true;
        }

        private static ExpeditionSaveData OverlayCapturedF10V7RoomResources(
            ExpeditionSaveData source, ExpeditionSaveData captured)
        {
            if (source == null || captured == null) return null;
            ExpeditionSaveData expected = source.Clone();
            expected.inventorySnapshotJson = captured.inventorySnapshotJson;
            expected.maximumHealth = captured.maximumHealth;
            expected.currentHealth = captured.currentHealth;
            expected.maximumEnergy = captured.maximumEnergy;
            expected.currentEnergy = captured.currentEnergy;
            expected.healthPotionCharges = captured.healthPotionCharges;
            return expected;
        }

        private static bool HasSameF10V7RoomProfile(ProfileSaveDataV7 source,
            ProfileSaveDataV7 candidate)
        {
            return SaveV7CandidateMapper.HasSameProfilePayload(source, candidate);
        }

        private static bool TryInventorySnapshotsMatch(ItemCatalog catalog, string leftJson,
            string rightJson, out string error)
        {
            error = string.Empty;
            try
            {
                ExpeditionInventorySnapshot leftSnapshot = JsonUtility.FromJson<ExpeditionInventorySnapshot>(leftJson);
                ExpeditionInventorySnapshot rightSnapshot = JsonUtility.FromJson<ExpeditionInventorySnapshot>(rightJson);
                if (leftSnapshot == null || rightSnapshot == null)
                    return FailF10RoomAdvance("An exact C1 inventory snapshot is missing.", out error);
                ExpeditionInventory left = ExpeditionInventory.Restore(catalog, leftSnapshot);
                ExpeditionInventory right = ExpeditionInventory.Restore(catalog, rightSnapshot);
                if (!string.Equals(JsonUtility.ToJson(left.CreateSnapshot()),
                        JsonUtility.ToJson(right.CreateSnapshot()), StringComparison.Ordinal))
                    return FailF10RoomAdvance("The captured binder inventory differs from the current live C1 collector inventory.", out error);
                return true;
            }
            catch (Exception exception)
            { return FailF10RoomAdvance("A C1 inventory snapshot could not be authenticated: " + exception.Message, out error); }
        }

        private static bool SameF10Roster(ExpeditionEncounterRosterPlan left,
            ExpeditionEncounterRosterPlan right)
        {
            if (left == null || right == null || left.Waves.Count != right.Waves.Count) return false;
            for (int index = 0; index < left.Waves.Count; index++)
            {
                ExpeditionEncounterWavePlan a = left.Waves[index];
                ExpeditionEncounterWavePlan b = right.Waves[index];
                if (a == null || b == null || a.WaveNumber != b.WaveNumber ||
                    !a.SpawnKeys.SequenceEqual(b.SpawnKeys, StringComparer.Ordinal)) return false;
            }
            return true;
        }

        /// <summary>
        /// Explicit fail-closed seam for the owner-side C1 adapter. The current provider only
        /// exposes a RoomEncounterRuntimePlan whose roster is not proof that its actors were
        /// adapted from this exact current pinned ResolvedEncounterPlanV1. The future owner
        /// factory must issue an unforgeable binding after it verifies the pin entry and C1
        /// checkpoint digest, retains the resolved/runtime plan instances plus measured asset
        /// references, current handle/revision, activation scope, and callback owner. Until that
        /// API is integrated, no durable payment candidate may be prepared.
        /// </summary>
        private bool HasAuthenticatedF10V7CombatPlanRuntimeBinding(
            F10V7SessionInitializationBinding sessionBinding, ExpeditionGameplayComposition owner,
            RoomEncounterRuntimeContext context, RoomEncounterRuntime runtime, long revision,
            object gateOwnerToken)
        {
            F10V7CombatPlanRuntimeBinding binding = _f10V7CombatPlanRuntimeBinding;
            return binding != null && IsCurrentF10V7CombatPlanRuntimeBinding(binding,
                sessionBinding, owner, context, runtime, revision, gateOwnerToken);
        }

        /// <summary>
        /// The capture DTO is only a value shape. Until the retained App owner issues an internal
        /// capture receipt from the real player binder and item collector under the active pause
        /// lease/revision, caller-supplied health, charges, or inventory cannot enter a candidate.
        /// </summary>
        private bool HasAuthenticatedF10V7CombatResourceCapture(
            F10V7SessionInitializationBinding sessionBinding, ExpeditionGameplayComposition owner,
            ExpeditionReadinessCompositionView readiness, RoomEncounterRuntimeContext context,
            ExpeditionSaveData capturedCommonState, ExpeditionInventory liveInventory, long revision)
        {
            F10V7C1PaymentResourceCapture receipt = _f10V7C1PaymentResourceCapture;
            return receipt != null && receipt.IsExactFor(this, sessionBinding, owner, readiness,
                context, capturedCommonState, liveInventory, revision) &&
                ReferenceEquals(receipt.PlanBinding, _f10V7CombatPlanRuntimeBinding) &&
                _profileMutationGate.CurrentCount == 0;
        }

        /// <summary>
        /// Distinguishes the exact opening publication (the only branch whose entry checkpoint
        /// must be restored) from a later owner-confirmed JSON/revision, such as Tutorial->C1.
        /// Callers must pass values read from the retained session handle and current App publisher.
        /// </summary>
        private static bool TryClassifyF10V7CombatCurrentSnapshot(long openingPublishedRevision,
            long currentPublishedRevision, string admittedJson, string handleExactJson,
            string currentConfirmedJson, out bool openingRollback, out string error)
        {
            openingRollback = false;
            error = string.Empty;
            if (openingPublishedRevision < 0 || currentPublishedRevision < openingPublishedRevision ||
                string.IsNullOrWhiteSpace(admittedJson) || string.IsNullOrWhiteSpace(handleExactJson) ||
                string.IsNullOrWhiteSpace(currentConfirmedJson))
            {
                error = "The published C1 source lacks an exact opening/current JSON and monotonic owner revision.";
                return false;
            }

            bool exactOpening = currentPublishedRevision == openingPublishedRevision &&
                string.Equals(currentConfirmedJson, admittedJson, StringComparison.Ordinal) &&
                string.Equals(currentConfirmedJson, handleExactJson, StringComparison.Ordinal);
            if (exactOpening)
            {
                openingRollback = true;
                return true;
            }

            bool laterConfirmed = currentPublishedRevision > openingPublishedRevision &&
                !string.Equals(currentConfirmedJson, admittedJson, StringComparison.Ordinal) &&
                !string.Equals(currentConfirmedJson, handleExactJson, StringComparison.Ordinal);
            if (laterConfirmed) return true;

            error = "The C1 source is neither the exact admitted opening snapshot nor a later distinct owner-confirmed revision.";
            return false;
        }

        /// <summary>Selects a detached V7 baseline after its owner/revision branch was authenticated.</summary>
        private static bool TrySelectF10V7CombatBaseline(bool openingRollback,
            ExpeditionSaveDataV7 openingRestoredState, ExpeditionSaveDataV7 handleBaseline,
            ExpeditionSaveDataV7 confirmedCurrentState, out ExpeditionSaveDataV7 selectedState)
        {
            selectedState = null;
            if (openingRollback)
            {
                if (openingRestoredState == null || handleBaseline == null ||
                    !StatesEqual(openingRestoredState, handleBaseline)) return false;
                selectedState = SaveV7Clone.Clone(openingRestoredState);
                return selectedState != null;
            }

            if (confirmedCurrentState == null) return false;
            selectedState = SaveV7Clone.Clone(confirmedCurrentState);
            return selectedState != null;
        }

        /// <summary>
        /// Verifies that the capture matches the current actor/collector inventory, then clones
        /// from that live inventory. This intentionally permits C1 consumption and pickups since
        /// room entry; the method is not actor-capture authority and remains behind the private
        /// binder/lease gate above.
        /// </summary>
        private static bool TryCloneF10V7LiveCombatInventory(ItemCatalog catalog,
            ExpeditionInventory liveInventory, string capturedInventorySnapshotJson,
            out ExpeditionInventory candidateInventory, out string liveInventorySnapshotJson,
            out string error)
        {
            candidateInventory = null;
            liveInventorySnapshotJson = string.Empty;
            error = string.Empty;
            if (catalog == null || liveInventory == null ||
                !ReferenceEquals(liveInventory.Catalog, catalog) ||
                string.IsNullOrWhiteSpace(capturedInventorySnapshotJson))
                return FailF10RoomAdvance("The C1 binder and live collector inventory are not bound to the retained item catalog.", out error);

            try
            {
                ExpeditionInventorySnapshot liveSnapshot = liveInventory.CreateSnapshot();
                liveInventorySnapshotJson = JsonUtility.ToJson(liveSnapshot);
                if (!TryInventorySnapshotsMatch(catalog, capturedInventorySnapshotJson,
                        liveInventorySnapshotJson, out error)) return false;
                candidateInventory = ExpeditionInventory.Restore(catalog, liveSnapshot);
                return candidateInventory != null;
            }
            catch (Exception exception)
            { return FailF10RoomAdvance("The live C1 collector inventory could not be validated or cloned: " + exception.Message, out error); }
        }

        internal sealed class F10V7CombatAdvanceCandidate
        {
            internal F10V7CombatAdvanceCandidate(F10V7SessionInitializationBinding binding,
                SaveRepository.F10V7SessionHandle handle, string sourceJson, long expectedRevision,
                ExpeditionGameplayComposition owner, ExpeditionRoomTransitionController controller,
                ExpeditionRoomTransitionController.PreparedRoomTransition preparedTransition,
                RoomEncounterRuntimeContext context, RoomEncounterRuntime runtime,
                RoomEncounterClearObservation clearObservation, ResolvedEncounterPlanV1 sourcePlan,
                ResolvedEncounterPlanV1 targetPlan, ExpeditionEncounterRewardRequest rewardRequest,
                ExpeditionEncounterRewardResolution rewardResolution, string resultId, string resultJson,
                string expectedInventoryJson, ExpeditionInventory candidateInventory,
                ExpeditionRunConfiguration candidateRun, ExpeditionSaveData commonCandidate,
                PinnedProfileCandidateV7 pinnedCandidate)
            {
                Binding = binding;
                Handle = handle;
                SourceJson = sourceJson;
                ExpectedRevision = expectedRevision;
                Owner = owner;
                Controller = controller;
                PreparedTransition = preparedTransition;
                Context = context;
                Runtime = runtime;
                ClearObservation = clearObservation;
                SourcePlan = sourcePlan;
                TargetPlan = targetPlan;
                RewardRequest = rewardRequest;
                RewardResolution = rewardResolution;
                ResultId = resultId;
                ResultJson = resultJson;
                ExpectedInventoryJson = expectedInventoryJson;
                CandidateInventory = ExpeditionInventory.Restore(candidateInventory.Catalog,
                    candidateInventory.CreateSnapshot());
                CandidateRun = candidateRun.WithInventory(CandidateInventory);
                CandidateCommon = commonCandidate?.Clone() ??
                    throw new ArgumentNullException(nameof(commonCandidate));
                PinnedCandidate = pinnedCandidate;
            }

            internal F10V7SessionInitializationBinding Binding { get; }
            internal SaveRepository.F10V7SessionHandle Handle { get; }
            internal string SourceJson { get; }
            internal long ExpectedRevision { get; }
            internal ExpeditionGameplayComposition Owner { get; }
            internal ExpeditionRoomTransitionController Controller { get; }
            internal ExpeditionRoomTransitionController.PreparedRoomTransition PreparedTransition { get; }
            internal RoomEncounterRuntimeContext Context { get; }
            internal RoomEncounterRuntime Runtime { get; }
            internal RoomEncounterClearObservation ClearObservation { get; }
            internal ResolvedEncounterPlanV1 SourcePlan { get; }
            internal ResolvedEncounterPlanV1 TargetPlan { get; }
            internal ExpeditionEncounterRewardRequest RewardRequest { get; }
            internal ExpeditionEncounterRewardResolution RewardResolution { get; }
            internal string ResultId { get; }
            internal string ResultJson { get; }
            internal string ExpectedInventoryJson { get; }
            internal ExpeditionInventory CandidateInventory { get; }
            internal ExpeditionRunConfiguration CandidateRun { get; }
            internal ExpeditionSaveData CandidateCommon { get; }
            internal PinnedProfileCandidateV7 PinnedCandidate { get; }
        }
    }
}
#endif
