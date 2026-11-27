#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using Luckbound.Gameplay.Rooms;
using UnityEditor;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Authenticated V7 C1 encounter-plan binding and its fail-closed asset seam.</summary>
    public sealed partial class ApplicationBootstrapper
    {
        internal const string F10V7C1NodeId = "sample.patio.combat";
        internal const string F10V7C1TemplateId = "room.patio.c1";

        private static readonly object F10V7CombatPlanBindingFactoryKey = new object();
        private static readonly object F10V7C1MeasuredAssetFactoryKey = new object();
        private F10V7CombatPlanRuntimeBinding _f10V7CombatPlanRuntimeBinding;

        private static string F10V7C1DescriptorKey(string descriptorId, int revision) =>
            (descriptorId ?? string.Empty) + "\0" +
            revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static bool IsLowerF10V7C1Sha256(string value) => value != null && value.Length == 64 &&
            value.All(character => character >= '0' && character <= '9' ||
                character >= 'a' && character <= 'f');

        internal enum F10V7C1MeasuredAssetRole
        {
            RoomPrefab = 1,
            RoomSource = 2,
            EnemyPrefab = 3
        }

        internal bool TryCreateF10V7CombatPlanRuntimeBinding(
            ExpeditionGameplayComposition composition,
            out F10V7CombatPlanRuntimeBinding binding, out string error)
        {
            return TryCreateF10V7CombatPlanRuntimeBinding(composition, null, out binding, out error);
        }

        private bool TryCreateF10V7CombatPlanRuntimeBinding(
            ExpeditionGameplayComposition composition, object gateOwnerToken,
            out F10V7CombatPlanRuntimeBinding binding, out string error)
        {
            binding = null;
            error = string.Empty;
            if (!TryCaptureCurrentV7C1Attachment(composition, gateOwnerToken,
                    out V7C1AttachmentView attachment,
                    out error)) return false;

            if (!TryRestoreCurrentV7C1Plan(attachment.SessionBinding, attachment.Published,
                    attachment.ConfirmedJson, attachment.Revision,
                    out ResolvedEncounterPlanV1 resolvedPlan,
                    out _, out _,
                    out string planSha256, out error)) return false;

            if (!TryCreateFreshF10V7C1MeasuredSnapshot(attachment, resolvedPlan,
                    planSha256, gateOwnerToken,
                    out V7C1AttachmentView receiptAttachment,
                    out ResolvedEncounterPlanV1 receiptPlan,
                    out _, out _,
                    out string receiptPlanSha256,
                    out F10V7C1MeasuredAssetSnapshot receiptAssets,
                    out error)) return false;

            F10V7CombatPlanRuntimeBinding retained = _f10V7CombatPlanRuntimeBinding;
            if (retained != null)
            {
                if (!retained.IsBoundTo(this, receiptAttachment.SessionBinding, composition,
                        receiptAttachment.Revision, receiptAttachment.ConfirmedJson,
                        receiptAttachment.Scope, receiptAttachment.Room, receiptPlan,
                        retained.MeasuredAssets) ||
                    !AreEquivalentF10V7C1MeasuredSnapshots(retained.MeasuredAssets,
                        receiptAssets, this, receiptAttachment.SessionBinding,
                        receiptAttachment.Revision, receiptAttachment.Scope,
                        receiptAttachment.Room, receiptPlan))
                    return FailF10V7C1Binding(
                        "A different or stale V7 C1 runtime binding is already retained by App.",
                        out error);

                if (!F10V7C1RuntimePlanAdapter.TryBuildRuntimePlan(this,
                        receiptAttachment.SessionBinding, receiptAttachment.Revision,
                        receiptAttachment.Scope, receiptAttachment.Room, receiptPlan,
                        retained.MeasuredAssets, receiptAttachment.Transitions,
                        out _, out string retainedAdapterError))
                    return FailF10V7C1Binding(retainedAdapterError, out error);

                if (!TryCreateFreshF10V7C1MeasuredSnapshot(receiptAttachment,
                        receiptPlan, receiptPlanSha256, gateOwnerToken,
                        out V7C1AttachmentView retainedAttachmentAfterValidation,
                        out ResolvedEncounterPlanV1 retainedPlanAfterValidation,
                        out _, out _, out _,
                        out F10V7C1MeasuredAssetSnapshot retainedAssetsAfterValidation,
                        out error)) return false;
                if (!AreEquivalentF10V7C1MeasuredSnapshots(retained.MeasuredAssets,
                        retainedAssetsAfterValidation, this,
                        retainedAttachmentAfterValidation.SessionBinding,
                        retainedAttachmentAfterValidation.Revision,
                        retainedAttachmentAfterValidation.Scope,
                        retainedAttachmentAfterValidation.Room, retainedPlanAfterValidation) ||
                    !ReferenceEquals(_f10V7CombatPlanRuntimeBinding, retained) ||
                    !retained.IsBoundTo(this,
                        retainedAttachmentAfterValidation.SessionBinding, composition,
                        retainedAttachmentAfterValidation.Revision,
                        retainedAttachmentAfterValidation.ConfirmedJson,
                        retainedAttachmentAfterValidation.Scope,
                        retainedAttachmentAfterValidation.Room, retainedPlanAfterValidation,
                        retained.MeasuredAssets) ||
                    !TryValidateRetainedV7C1Coordinator(retained,
                        retainedAttachmentAfterValidation, out error))
                    return FailF10V7C1Binding(
                        "The retained V7 C1 binding or exact receipt changed during AssetDatabase validation.",
                        out error);

                binding = retained;
                return true;
            }

            if (!F10V7C1RuntimePlanAdapter.TryBuildRuntimePlan(this,
                    receiptAttachment.SessionBinding, receiptAttachment.Revision,
                    receiptAttachment.Scope, receiptAttachment.Room, receiptPlan,
                    receiptAssets, receiptAttachment.Transitions,
                    out RoomEncounterRuntimePlan runtimePlan, out string adapterError))
                return FailF10V7C1Binding(adapterError, out error);

            if (!TryCreateFreshF10V7C1MeasuredSnapshot(receiptAttachment,
                    receiptPlan, receiptPlanSha256, gateOwnerToken,
                    out V7C1AttachmentView confirmedAttachment,
                    out ResolvedEncounterPlanV1 confirmedPlan,
                    out EncounterPlanPinV1 confirmedPin,
                    out ExpeditionRoomCheckpointSaveDataV7 confirmedCheckpoint,
                    out string confirmedPlanSha256,
                    out F10V7C1MeasuredAssetSnapshot confirmedAssets,
                    out error)) return false;
            if (!AreEquivalentF10V7C1MeasuredSnapshots(receiptAssets, confirmedAssets,
                    this, confirmedAttachment.SessionBinding, confirmedAttachment.Revision,
                    confirmedAttachment.Scope, confirmedAttachment.Room, confirmedPlan))
                return FailF10V7C1Binding(
                    "The verified C1 assets changed during adapter validation; no runtime binding was retained.",
                    out error);
            if (_f10V7CombatPlanRuntimeBinding != null)
                return FailF10V7C1Binding(
                    "A V7 C1 runtime binding was retained during AssetDatabase validation; the issuer will not replace it.",
                    out error);

            var candidate = new F10V7CombatPlanRuntimeBinding(
                F10V7CombatPlanBindingFactoryKey, this, confirmedAttachment.SessionBinding,
                confirmedAttachment.Registration, confirmedAttachment.Activation, composition,
                confirmedAttachment.Run, confirmedAttachment.Revision,
                confirmedAttachment.ConfirmedJson, confirmedAttachment.Scope,
                confirmedAttachment.Room, confirmedPin, confirmedCheckpoint, confirmedPlan,
                runtimePlan, receiptAssets, confirmedPlanSha256);
            if (!candidate.IsBoundTo(this, confirmedAttachment.SessionBinding, composition,
                    confirmedAttachment.Revision, confirmedAttachment.ConfirmedJson,
                    confirmedAttachment.Scope, confirmedAttachment.Room, confirmedPlan,
                    receiptAssets))
                return FailF10V7C1Binding(
                    "The App-issued C1 runtime plan binding did not preserve its exact source identities.",
                    out error);

            _f10V7CombatPlanRuntimeBinding = candidate;
            binding = candidate;
            return true;
        }

        /// <summary>
        /// Payment-facing check. It succeeds only while the exact current C1 coordinator still
        /// retains the same runtime and has observed a physical clear of that exact roster.
        /// A later retry must retain its already captured fact and must not call this again.
        /// </summary>
        internal bool IsCurrentF10V7CombatPlanRuntimeBinding(
            F10V7CombatPlanRuntimeBinding planBinding,
            F10V7SessionInitializationBinding sessionBinding,
            ExpeditionGameplayComposition owner, RoomEncounterRuntimeContext context,
            RoomEncounterRuntime runtime, long revision)
        {
            return IsCurrentF10V7CombatPlanRuntimeBinding(planBinding, sessionBinding,
                owner, context, runtime, revision, null);
        }

        private bool IsCurrentF10V7CombatPlanRuntimeBinding(
            F10V7CombatPlanRuntimeBinding planBinding,
            F10V7SessionInitializationBinding sessionBinding,
            ExpeditionGameplayComposition owner, RoomEncounterRuntimeContext context,
            RoomEncounterRuntime runtime, long revision, object gateOwnerToken)
        {
            if (planBinding == null || sessionBinding == null || owner == null ||
                context == null || runtime == null ||
                !ReferenceEquals(_f10V7CombatPlanRuntimeBinding, planBinding) ||
                !planBinding.IsBoundTo(this, sessionBinding, owner, revision,
                    planBinding.ConfirmedJson, planBinding.Scope, planBinding.Room,
                    planBinding.ResolvedPlan, planBinding.MeasuredAssets) ||
                !ReferenceEquals(planBinding.SessionBinding, sessionBinding) ||
                !ReferenceEquals(planBinding.Scope, context.Scope) ||
                !ReferenceEquals(planBinding.RuntimePlan, context.Plan) ||
                context.RunId != planBinding.Run.RunId ||
                context.RoomInstanceId != planBinding.ResolvedPlan.RoomInstanceId ||
                !string.Equals(context.NodeId, F10V7C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(context.TemplateId, F10V7C1TemplateId, StringComparison.Ordinal) ||
                !context.IsCurrentRoomScope()) return false;

            if (!TryCaptureCurrentV7C1Attachment(owner, gateOwnerToken,
                    out V7C1AttachmentView attachment,
                    out string ignoredError) ||
                !ReferenceEquals(attachment.SessionBinding, sessionBinding) ||
                attachment.Revision != revision ||
                !ReferenceEquals(attachment.Scope, planBinding.Scope) ||
                !ReferenceEquals(attachment.Room, planBinding.Room) ||
                !string.Equals(attachment.ConfirmedJson, planBinding.ConfirmedJson,
                    StringComparison.Ordinal)) return false;

            F10C1RuntimeCoordinator coordinator = planBinding.Scope
                .GetComponent<F10C1RuntimeCoordinator>();
            return coordinator != null &&
                coordinator.IsBoundToV7PlanBinding(planBinding, sessionBinding, this,
                    owner, context, runtime, revision) &&
                coordinator.HasExactV7PhysicalClearObservation();
        }

        private bool TryCaptureCurrentV7C1Attachment(
            ExpeditionGameplayComposition composition, out V7C1AttachmentView attachment,
            out string error)
        {
            return TryCaptureCurrentV7C1Attachment(composition, null, out attachment, out error);
        }

        private bool TryCaptureCurrentV7C1Attachment(
            ExpeditionGameplayComposition composition, object gateOwnerToken,
            out V7C1AttachmentView attachment,
            out string error)
        {
            attachment = null;
            error = string.Empty;
            bool exactPaymentGate = gateOwnerToken != null &&
                IsExactF10V7C1PaymentGateOwner(composition, gateOwnerToken);
            if (!Application.isPlaying || !ReferenceEquals(_instance, this) ||
                !ReferenceEquals(ApplicationBootstrapper.Instance, this) || composition == null ||
                !composition.isActiveAndEnabled || !composition.IsReady ||
                gateOwnerToken != null && !exactPaymentGate ||
                (exactPaymentGate
                    ? HasPendingSessionMutationOutsideExactF10V7C1PaymentGate(composition, gateOwnerToken)
                    : HasPendingSessionMutation) || HasPendingRoomAdvance || IsRoomAdvanceInFlight)
                return FailF10V7C1Binding(
                    "C1 V7 requires the exact active App session with no pending mutation or room advance.",
                    out error);

            ExpeditionReadinessCompositionView readiness = composition.CaptureReadinessCompositionView();
            ExpeditionRunConfiguration run = readiness == null ? null : readiness.Run;
            ExpeditionRoomTransitionController transitions = readiness == null
                ? null : readiness.RoomTransitions;
            ExpeditionSessionReadinessRegistration registration = readiness == null
                ? null : readiness.ReadinessRegistration;
            F10V7SessionInitializationBinding sessionBinding =
                composition.CaptureV7InitializationBinding();
            F10V7PublishedSession published = _f10V7PublishedSession;
            if (readiness == null || !readiness.IsReady ||
                !ReferenceEquals(readiness.Owner, composition) ||
                !ReferenceEquals(readiness.Application, this) ||
                !ReferenceEquals(run, ActiveRun) || run == null || run.Route == null ||
                run.Route.GenerationVersion != 2 || transitions == null ||
                !ReferenceEquals(transitions, composition.RoomTransitions) ||
                registration == null || registration.IsDisposed || !registration.IsOwnerActive ||
                !ReferenceEquals(registration.Owner, composition) ||
                sessionBinding == null || !ReferenceEquals(registration.V7InitializationBinding, sessionBinding) ||
                published == null || !ReferenceEquals(published.Binding, sessionBinding) ||
                !registration.IsCurrentV7PublishedBinding(published) ||
                !IsCurrentV7PublishedSession(sessionBinding, registration, published.Revision) ||
                !TryGetCurrentV7PublishedSnapshot(sessionBinding,
                    out F10V7ExpeditionReadProjection projection, out string confirmedJson,
                    out long revision) || !ReferenceEquals(projection, published.Projection) ||
                !string.Equals(confirmedJson, published.ConfirmedJson, StringComparison.Ordinal) ||
                revision != published.Revision || revision != ActiveExpeditionRevision)
                return FailF10V7C1Binding(
                    "C1 requires the exact current published V7 App binding, projection, JSON, and revision.",
                    out error);

            ExpeditionSaveData activeSave = ActiveExpedition;
            if (activeSave == null ||
                !string.Equals(activeSave.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(activeSave.currentNodeId, F10V7C1NodeId, StringComparison.Ordinal) ||
                activeSave.visitedNodeIds == null || activeSave.visitedNodeIds.Length == 0 ||
                !string.Equals(activeSave.visitedNodeIds[activeSave.visitedNodeIds.Length - 1],
                    F10V7C1NodeId, StringComparison.Ordinal) ||
                !ReferenceEquals(transitions, composition.RoomTransitions) ||
                !string.Equals(transitions.CurrentNodeId, F10V7C1NodeId, StringComparison.Ordinal))
                return FailF10V7C1Binding(
                    "The authenticated current V7 route cursor is not settled on sample.patio.combat.",
                    out error);

            ExpeditionReadinessTransitionView transition = transitions.CaptureReadinessTransitionView();
            RoomLifetimeScope scope = transition == null ? null : transition.ActiveScope;
            RoomInstanceId expectedRoomId = ExpeditionRoomIdentity.ForNode(run.RunId, F10V7C1NodeId);
            if (transition == null || !transition.IsInitialized || transition.IsDisposed ||
                transition.IsTransitioning || transition.HasPreparedTransition ||
                !string.Equals(transition.ActiveNodeId, F10V7C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(transition.ActiveTemplateId, F10V7C1TemplateId, StringComparison.Ordinal) ||
                scope == null || !ReferenceEquals(scope, transitions.ActiveScope) ||
                !ReferenceEquals(transition.ActiveRoomRoot, scope.gameObject) || scope.IsDisposed ||
                !scope.isActiveAndEnabled || scope.RoomInstanceId != expectedRoomId ||
                !string.Equals(scope.TemplateId, F10V7C1TemplateId, StringComparison.Ordinal) ||
                !transitions.IsCurrentRoom(expectedRoomId) || transitions.CurrentRoomId != ActiveRoomInstanceId)
                return FailF10V7C1Binding(
                    "V7 C1 requires the exact active room scope and settled route activation.", out error);

            ExpeditionRoomActivationRegistration activation = registration.CurrentActivation;
            if (activation == null || activation.State != ExpeditionRoomActivationState.Bound ||
                !ReferenceEquals(activation.Scope, scope) ||
                !ReferenceEquals(activation.Controller, transitions) || activation.RunId != run.RunId ||
                !ReferenceEquals(activation.BoundRun, run) ||
                !string.Equals(activation.NodeId, F10V7C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(activation.TemplateId, F10V7C1TemplateId, StringComparison.Ordinal))
                return FailF10V7C1Binding(
                    "V7 readiness does not own the active C1 Bound activation.", out error);

            if (readiness.PlayerMotor == null || !readiness.PlayerMotor.isActiveAndEnabled ||
                !readiness.PlayerMotor.ActorId.IsValid || readiness.PlayerObject == null ||
                !ReferenceEquals(readiness.PlayerMotor.gameObject, readiness.PlayerObject) ||
                readiness.PlayerObject.GetComponent<Collider2D>() == null)
                return FailF10V7C1Binding(
                    "V7 C1 requires the exact bound live player actor and collider.", out error);

            RoomTemplatePrefab2D[] roomComponents = transition.ActiveRoomRoot
                .GetComponentsInChildren<RoomTemplatePrefab2D>(true);
            RoomTemplatePrefab2D room = roomComponents.Length == 1 ? roomComponents[0] : null;
            if (room == null || !room.isActiveAndEnabled || !room.gameObject.activeInHierarchy ||
                !room.transform.IsChildOf(scope.transform))
                return FailF10V7C1Binding(
                    "V7 C1 requires exactly one active RoomTemplatePrefab2D under the current scope.",
                    out error);

            try { room.ValidateGeneratedPrefab(); }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The active C1 room source/markers are not a validated generated prefab: " +
                    exception.Message, out error);
            }

            attachment = new V7C1AttachmentView(sessionBinding, published, projection,
                confirmedJson, revision, registration, activation, run, transitions, scope, room);
            return true;
        }

        /// <summary>
        /// The payment path may revalidate while holding its own profile-mutation gate. Exclude
        /// only that gate after the App-private owner/token pair matches; every other mutation
        /// lane remains a rejection. This deliberately does not infer ownership from gate counts.
        /// </summary>
        private bool HasPendingSessionMutationOutsideExactF10V7C1PaymentGate(
            ExpeditionGameplayComposition owner, object gateOwnerToken)
        {
            if (!IsExactF10V7C1PaymentGateOwner(owner, gateOwnerToken)) return true;
            return HasPendingExpeditionWrite || HasPendingSettlement ||
                HasPendingContractProgression || HasPendingCraftingWrite ||
                HasPendingEconomyMutation || HasPendingPermanentUpgradePurchase || HasPendingRoomAdvance ||
                IsRoomAdvanceInFlight || _pendingRoomAdvance != null || _pendingExpeditionCandidate != null ||
                _pendingExpeditionSessionPublication != null || _pendingF10V7TutorialAdvance != null ||
                _pendingF10V7C1CombatPayment != null ||
                _expeditionSessionMutationPublicationOwner != null ||
                _expeditionSessionStateCapture != null || _expeditionSessionMutationPreparer != null;
        }

        private bool TryRestoreCurrentV7C1Plan(
            F10V7SessionInitializationBinding sessionBinding, F10V7PublishedSession published,
            string confirmedJson, long revision, out ResolvedEncounterPlanV1 plan,
            out EncounterPlanPinV1 pin, out ExpeditionRoomCheckpointSaveDataV7 checkpoint,
            out string planSha256, out string error)
        {
            plan = null;
            pin = null;
            checkpoint = null;
            planSha256 = string.Empty;
            error = string.Empty;
            SaveRepository.F10V7SessionHandle handle = sessionBinding?.SessionHandle;
            if (published == null || sessionBinding == null || handle == null ||
                !ReferenceEquals(published.Binding, sessionBinding) || revision != published.Revision ||
                string.IsNullOrWhiteSpace(confirmedJson) ||
                !string.Equals(confirmedJson, published.ConfirmedJson, StringComparison.Ordinal) ||
                _saveRepository == null ||
                !ReferenceEquals(handle.Repository, _saveRepository) ||
                !ReferenceEquals(handle.ItemCatalog, sessionBinding.AdmissionItemCatalog) ||
                !ReferenceEquals(handle.Registry, sessionBinding.AdmissionRegistry) ||
                !_saveRepository.IsRetainedF10V7SessionHandle(handle, sessionBinding.AdmittedJson,
                    sessionBinding.Run.RunId, sessionBinding.AdmissionItemCatalog,
                    sessionBinding.AdmissionRegistry))
                return FailF10V7C1Binding(
                    "The current confirmed V7 JSON is not attached to its exact retained session handle/catalog/registry.",
                    out error);

            ProfileSaveEnvelopeV7 envelope;
            try
            {
                if (!SaveV7JsonCodec.TryDecodeProfile(StrictV7Utf8.GetBytes(confirmedJson),
                        out envelope, out SaveV7Error decodeError))
                    return FailF10V7C1Binding(
                        "The exact published ConfirmedJson is not a valid active V7 profile: " +
                        decodeError.Code + ": " + decodeError.Message, out error);
                if (envelope == null || envelope.contentVersion != 7 || envelope.activeExpedition == null ||
                    envelope.activeExpedition.contentVersion != 7 ||
                    !string.Equals(envelope.activeExpedition.runId,
                        sessionBinding.Run.RunId.ToString(), StringComparison.Ordinal))
                    return FailF10V7C1Binding(
                        "The exact published ConfirmedJson does not identify the retained active V7 run.",
                        out error);
            }
            catch (Exception exception)
            {
                return FailF10V7C1Binding(
                    "The exact published ConfirmedJson could not be decoded: " + exception.Message,
                    out error);
            }

            if (!TryClassifyF10V7CombatCurrentSnapshot(sessionBinding.PublishedRevision,
                    revision, sessionBinding.AdmittedJson, handle.ExactJson,
                    confirmedJson, out bool openingRollback, out string sourceBranchError))
                return FailF10V7C1Binding(sourceBranchError, out error);

            ExpeditionRunConfiguration restoredRun;
            ExpeditionSaveDataV7 state;
            ResolvedEncounterPlanV1 restoredPlan;
            SaveV7Error restoreError;
            bool restored = openingRollback
                ? SaveV7CandidateMapper.TryRestore(envelope,
                    sessionBinding.AdmissionItemCatalog, sessionBinding.AdmissionRegistry,
                    out restoredRun, out state, out restoredPlan, out restoreError)
                : SaveV7CandidateMapper.TryRestoreCurrentForSettlement(envelope,
                    sessionBinding.AdmissionItemCatalog, sessionBinding.AdmissionRegistry,
                    out restoredRun, out state, out restoredPlan, out restoreError);
            if (!restored)
                return FailF10V7C1Binding(
                    (openingRollback
                        ? "The exact opening V7 state could not be restored through its entry checkpoint: "
                        : "The promoted current V7 state could not be restored with the retained catalog: ") +
                    restoreError.Code + ": " + restoreError.Message, out error);
            if (restoredRun == null || state == null || restoredPlan == null ||
                restoredRun.RunId != sessionBinding.Run.RunId ||
                !ReferenceEquals(restoredRun.Inventory?.Catalog, sessionBinding.AdmissionItemCatalog) ||
                !string.Equals(state.currentNodeId, F10V7C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(published.Projection?.RestoredStateV7?.currentNodeId,
                    F10V7C1NodeId, StringComparison.Ordinal))
                return FailF10V7C1Binding(
                    "The selected restored V7 state is not the exact retained C1 run and current node.", out error);

            if (!StatesEqual(handle.BaselineState,
                    sessionBinding.Projection?.RestoredStateV7))
                return FailF10V7C1Binding(
                    "The retained opening handle baseline differs from the session binding's authenticated opening projection.",
                    out error);

            if (openingRollback)
            {
                bool openingProjectionRestored = F10V7ExpeditionReadProjector.TryCreate(envelope,
                    sessionBinding.AdmissionItemCatalog, sessionBinding.AdmissionRegistry,
                    out F10V7ExpeditionReadProjection openingProjection,
                    out SaveV7Error openingProjectionError);
                if (!ReferenceEquals(published.Projection, sessionBinding.Projection) ||
                    !StatesEqual(state, handle.BaselineState) ||
                    !openingProjectionRestored ||
                    openingProjection == null ||
                    !V7ProjectionsEqual(openingProjection, sessionBinding.Projection) ||
                    !V7ProjectionsEqual(openingProjection, published.Projection) ||
                    !StatesEqual(openingProjection.RestoredStateV7, state))
                    return FailF10V7C1Binding(
                        "The exact opening C1 rollback does not match the retained handle baseline, session binding, and published projection." +
                        (openingProjectionError.Code == SaveV7ErrorCode.None
                            ? string.Empty
                            : " " + openingProjectionError.Code + ": " + openingProjectionError.Message),
                        out error);
            }
            else
            {
                bool currentProjectionRestored = F10V7ExpeditionReadProjector.TryCreateCurrent(envelope,
                    sessionBinding.AdmissionItemCatalog, sessionBinding.AdmissionRegistry,
                    out F10V7ExpeditionReadProjection currentProjection,
                    out SaveV7Error currentProjectionError);
                if (revision <= sessionBinding.PublishedRevision ||
                    ReferenceEquals(published.Projection, sessionBinding.Projection) ||
                    !currentProjectionRestored ||
                    currentProjection == null ||
                    !V7ProjectionsEqual(currentProjection, published.Projection) ||
                    !StatesEqual(state, currentProjection.SourceStateV7) ||
                    !StatesEqual(state, currentProjection.RestoredStateV7) ||
                    !StatesEqual(state, published.Projection.SourceStateV7) ||
                    !StatesEqual(state, published.Projection.RestoredStateV7))
                    return FailF10V7C1Binding(
                        "The promoted current C1 state does not match its exact App publication and projected source/current checkpoint." +
                        (currentProjectionError.Code == SaveV7ErrorCode.None
                            ? string.Empty
                            : " " + currentProjectionError.Code + ": " + currentProjectionError.Message),
                        out error);
            }

            if (!SaveV7CandidateMapper.TryToDomainPin(state.encounterPlanPin,
                    out pin, out SaveV7Error pinError))
                return FailF10V7C1Binding(
                    "The current ConfirmedJson encounter pin is invalid: " +
                    pinError.Code + ": " + pinError.Message, out error);

            SaveV7Error checkpointError = default(SaveV7Error);
            bool checkpointDecoded = !string.IsNullOrWhiteSpace(state.roomEntryCheckpointJson) &&
                SaveV7JsonCodec.TryDecodeCheckpoint(StrictV7Utf8.GetBytes(state.roomEntryCheckpointJson),
                    out checkpoint, out checkpointError);
            if (!checkpointDecoded || checkpoint == null)
                return FailF10V7C1Binding(
                    "The current ConfirmedJson has no valid typed V7 C1 checkpoint: " +
                    checkpointError.Code + ": " + checkpointError.Message, out error);

            if (!EncounterPlanPinRestorerV1.TryRestore(pin, restoredRun, F10V7C1NodeId,
                    sessionBinding.AdmissionRegistry, out ResolvedEncounterPlanV1 pinnedPlan,
                    out EncounterPlanPinErrorV1 restorePinError))
                return FailF10V7C1Binding(
                    "The C1 plan could not be restored from the pin/registry: " +
                    restorePinError.Code + ": " + restorePinError.Message, out error);

            if (!F10V7C1RuntimePlanAdapter.TryValidatePinnedC1Plan(
                    restoredPlan, pin, checkpoint, out planSha256, out string identityError) ||
                !string.Equals(planSha256,
                    ResolvedEncounterPlanCanonicalV1.ComputeSha256(pinnedPlan), StringComparison.Ordinal) ||
                !string.Equals(planSha256,
                    ResolvedEncounterPlanCanonicalV1.ComputeSha256(restoredPlan), StringComparison.Ordinal))
                return FailF10V7C1Binding(
                    "The restored C1 plan digest differs from pin.entry/checkpoint.encounterEntry: " +
                    identityError, out error);

            plan = restoredPlan;
            return true;
        }

        private bool TryValidateRetainedV7C1Coordinator(F10V7CombatPlanRuntimeBinding binding,
            V7C1AttachmentView attachment, out string error)
        {
            error = string.Empty;
            if (binding == null || attachment == null ||
                !ReferenceEquals(binding.SessionBinding, attachment.SessionBinding) ||
                binding.Revision != attachment.Revision ||
                !ReferenceEquals(binding.Scope, attachment.Scope) ||
                !ReferenceEquals(binding.Room, attachment.Room))
                return FailF10V7C1Binding(
                    "The retained V7 C1 binding no longer matches its exact room activation.", out error);

            if (attachment.Activation.Context == null && attachment.Activation.Source == null)
                return true;

            F10C1RuntimeCoordinator coordinator = attachment.Scope.GetComponent<F10C1RuntimeCoordinator>();
            if (coordinator == null ||
                !coordinator.IsBoundToV7PlanBinding(binding, attachment.SessionBinding,
                    this, attachment.Composition, coordinator.Context, coordinator.Runtime,
                    attachment.Revision) ||
                !ReferenceEquals(attachment.Activation.Context, coordinator.Context) ||
                !ReferenceEquals(attachment.Activation.Source, coordinator.SnapshotSource))
                return FailF10V7C1Binding(
                    "The current C1 activation context/source is not owned by the exact retained V7 coordinator.",
                    out error);
            return true;
        }

        private static bool FailF10V7C1Binding(string message, out string error)
        {
            error = string.IsNullOrWhiteSpace(message)
                ? "V7 C1 runtime plan binding failed closed."
                : message;
            return false;
        }

        internal sealed class F10V7CombatPlanRuntimeBinding
        {
            internal F10V7CombatPlanRuntimeBinding(object factoryKey,
                ApplicationBootstrapper bootstrapper,
                F10V7SessionInitializationBinding sessionBinding,
                ExpeditionSessionReadinessRegistration registration,
                ExpeditionRoomActivationRegistration activation,
                ExpeditionGameplayComposition composition, ExpeditionRunConfiguration run,
                long revision, string confirmedJson, RoomLifetimeScope scope,
                RoomTemplatePrefab2D room, EncounterPlanPinV1 pin,
                ExpeditionRoomCheckpointSaveDataV7 checkpoint,
                ResolvedEncounterPlanV1 resolvedPlan, RoomEncounterRuntimePlan runtimePlan,
                F10V7C1MeasuredAssetSnapshot measuredAssets, string planSha256)
            {
                if (!ReferenceEquals(factoryKey, F10V7CombatPlanBindingFactoryKey))
                    throw new InvalidOperationException(
                        "Only the authenticated App C1 factory may issue this binding.");
                Bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
                SessionBinding = sessionBinding ?? throw new ArgumentNullException(nameof(sessionBinding));
                Registration = registration ?? throw new ArgumentNullException(nameof(registration));
                Activation = activation ?? throw new ArgumentNullException(nameof(activation));
                Composition = composition ?? throw new ArgumentNullException(nameof(composition));
                Run = run ?? throw new ArgumentNullException(nameof(run));
                Revision = revision;
                ConfirmedJson = confirmedJson ?? throw new ArgumentNullException(nameof(confirmedJson));
                Scope = scope ?? throw new ArgumentNullException(nameof(scope));
                Room = room ?? throw new ArgumentNullException(nameof(room));
                Pin = pin ?? throw new ArgumentNullException(nameof(pin));
                _checkpoint = SaveV7Clone.Clone(checkpoint) ??
                    throw new ArgumentNullException(nameof(checkpoint));
                ResolvedPlan = resolvedPlan ?? throw new ArgumentNullException(nameof(resolvedPlan));
                RuntimePlan = runtimePlan ?? throw new ArgumentNullException(nameof(runtimePlan));
                MeasuredAssets = measuredAssets ?? throw new ArgumentNullException(nameof(measuredAssets));
                PlanSha256 = planSha256 ?? throw new ArgumentNullException(nameof(planSha256));
                if (!string.Equals(PlanSha256,
                        ResolvedEncounterPlanCanonicalV1.ComputeSha256(ResolvedPlan),
                        StringComparison.Ordinal))
                    throw new InvalidOperationException("The App C1 binding digest must be computed from its resolved plan.");
            }

            internal ApplicationBootstrapper Bootstrapper { get; }
            internal F10V7SessionInitializationBinding SessionBinding { get; }
            internal ExpeditionSessionReadinessRegistration Registration { get; }
            internal ExpeditionRoomActivationRegistration Activation { get; }
            internal ExpeditionGameplayComposition Composition { get; }
            internal ExpeditionRunConfiguration Run { get; }
            internal long Revision { get; }
            internal string ConfirmedJson { get; }
            internal RoomLifetimeScope Scope { get; }
            internal RoomTemplatePrefab2D Room { get; }
            internal EncounterPlanPinV1 Pin { get; }
            internal ExpeditionRoomCheckpointSaveDataV7 Checkpoint => SaveV7Clone.Clone(_checkpoint);
            private readonly ExpeditionRoomCheckpointSaveDataV7 _checkpoint;
            internal ResolvedEncounterPlanV1 ResolvedPlan { get; }
            internal RoomEncounterRuntimePlan RuntimePlan { get; }
            internal F10V7C1MeasuredAssetSnapshot MeasuredAssets { get; }
            internal string PlanSha256 { get; }

            internal bool IsBoundTo(ApplicationBootstrapper bootstrapper,
                F10V7SessionInitializationBinding sessionBinding,
                ExpeditionGameplayComposition composition, long revision, string confirmedJson,
                RoomLifetimeScope scope, RoomTemplatePrefab2D room,
                ResolvedEncounterPlanV1 resolvedPlan, F10V7C1MeasuredAssetSnapshot measuredAssets)
            {
                return ReferenceEquals(Bootstrapper, bootstrapper) &&
                    ReferenceEquals(SessionBinding, sessionBinding) &&
                    ReferenceEquals(Composition, composition) && ReferenceEquals(Run, sessionBinding?.Run) &&
                    Revision == revision && string.Equals(ConfirmedJson, confirmedJson, StringComparison.Ordinal) &&
                    ReferenceEquals(Scope, scope) && ReferenceEquals(Room, room) &&
                    resolvedPlan != null && string.Equals(PlanSha256,
                        ResolvedEncounterPlanCanonicalV1.ComputeSha256(resolvedPlan),
                        StringComparison.Ordinal) &&
                    ReferenceEquals(MeasuredAssets, measuredAssets) &&
                    string.Equals(PlanSha256,
                        ResolvedEncounterPlanCanonicalV1.ComputeSha256(ResolvedPlan),
                        StringComparison.Ordinal) && ReferenceEquals(Activation.Scope, Scope) &&
                    ReferenceEquals(Registration.CurrentActivation, Activation);
            }
        }

        private sealed class V7C1AttachmentView
        {
            internal V7C1AttachmentView(F10V7SessionInitializationBinding sessionBinding,
                F10V7PublishedSession published, F10V7ExpeditionReadProjection projection,
                string confirmedJson, long revision,
                ExpeditionSessionReadinessRegistration registration,
                ExpeditionRoomActivationRegistration activation, ExpeditionRunConfiguration run,
                ExpeditionRoomTransitionController transitions, RoomLifetimeScope scope,
                RoomTemplatePrefab2D room)
            {
                SessionBinding = sessionBinding; Published = published; Projection = projection;
                ConfirmedJson = confirmedJson; Revision = revision; Registration = registration;
                Activation = activation; Run = run; Transitions = transitions; Scope = scope; Room = room;
                Composition = sessionBinding.Composition;
            }
            internal F10V7SessionInitializationBinding SessionBinding { get; }
            internal F10V7PublishedSession Published { get; }
            internal F10V7ExpeditionReadProjection Projection { get; }
            internal string ConfirmedJson { get; }
            internal long Revision { get; }
            internal ExpeditionSessionReadinessRegistration Registration { get; }
            internal ExpeditionRoomActivationRegistration Activation { get; }
            internal ExpeditionRunConfiguration Run { get; }
            internal ExpeditionRoomTransitionController Transitions { get; }
            internal RoomLifetimeScope Scope { get; }
            internal RoomTemplatePrefab2D Room { get; }
            internal ExpeditionGameplayComposition Composition { get; }
        }

        /// <summary>
        /// Opaque App-issued references measured by the approved Editor importer. The current
        /// candidate intentionally contains no ingestion method; its field remains null until
        /// the joint provenance cut is reviewed and integrated.
        /// </summary>
        internal sealed class F10V7C1MeasuredAssetSnapshot
        {
            private readonly F10V7C1MeasuredAssetReference[] _references;

            internal F10V7C1MeasuredAssetSnapshot(object factoryKey,
                ApplicationBootstrapper bootstrapper,
                F10V7SessionInitializationBinding sessionBinding, long revision,
                RoomLifetimeScope scope, RoomTemplatePrefab2D room,
                ResolvedEncounterPlanV1 plan,
                string physicalDescriptorPayloadSha256, string bundleManifestSha256,
                IEnumerable<F10V7C1MeasuredAssetReference> references)
            {
                if (!ReferenceEquals(factoryKey, F10V7C1MeasuredAssetFactoryKey))
                    throw new InvalidOperationException(
                        "Only App's reviewed provenance importer may mint a measured C1 asset snapshot.");
                Bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
                SessionBinding = sessionBinding ?? throw new ArgumentNullException(nameof(sessionBinding));
                Revision = revision;
                Scope = scope ?? throw new ArgumentNullException(nameof(scope));
                Room = room ?? throw new ArgumentNullException(nameof(room));
                PlanSha256 = ResolvedEncounterPlanCanonicalV1.ComputeSha256(
                    plan ?? throw new ArgumentNullException(nameof(plan)));
                if (!IsLowerF10V7C1Sha256(physicalDescriptorPayloadSha256) ||
                    !IsLowerF10V7C1Sha256(bundleManifestSha256))
                    throw new ArgumentException(
                        "The exact physical descriptor payload and bundle manifest SHA-256 values are required.");
                PhysicalDescriptorPayloadSha256 = physicalDescriptorPayloadSha256;
                BundleManifestSha256 = bundleManifestSha256;
                _references = (references ?? throw new ArgumentNullException(nameof(references))).ToArray();
                if (_references.Any(reference => reference == null) ||
                    _references.Select(reference => F10V7C1DescriptorKey(reference.DescriptorId,
                        reference.Revision) + "\0" + ((int)reference.Role).ToString(
                            System.Globalization.CultureInfo.InvariantCulture))
                        .Distinct(StringComparer.Ordinal).Count() != _references.Length)
                    throw new ArgumentException(
                        "Measured descriptor/role references must be non-null and unique.", nameof(references));
            }

            internal ApplicationBootstrapper Bootstrapper { get; }
            internal F10V7SessionInitializationBinding SessionBinding { get; }
            internal long Revision { get; }
            internal RoomLifetimeScope Scope { get; }
            internal RoomTemplatePrefab2D Room { get; }
            internal string PlanSha256 { get; }
            internal string PhysicalDescriptorPayloadSha256 { get; }
            internal string BundleManifestSha256 { get; }

            internal bool IsBoundTo(ApplicationBootstrapper bootstrapper,
                F10V7SessionInitializationBinding sessionBinding, long revision,
                RoomLifetimeScope scope, RoomTemplatePrefab2D room, ResolvedEncounterPlanV1 plan)
            {
                return ReferenceEquals(Bootstrapper, bootstrapper) &&
                    ReferenceEquals(SessionBinding, sessionBinding) && Revision == revision &&
                    ReferenceEquals(Scope, scope) && ReferenceEquals(Room, room) && plan != null &&
                    string.Equals(PlanSha256,
                        ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan), StringComparison.Ordinal);
            }

            internal bool HasExactDescriptors(ResolvedEncounterPlanV1 plan)
            {
                if (plan == null || plan.UsedDescriptors == null) return false;
                var expectedRoles = new HashSet<string>(StringComparer.Ordinal);
                foreach (EncounterCatalogDescriptor expected in plan.UsedDescriptors)
                {
                    if (expected == null) return false;
                    if (expected.Kind == EncounterCatalogDescriptorKind.Room)
                    {
                        expectedRoles.Add(AssetReferenceKey(expected, F10V7C1MeasuredAssetRole.RoomPrefab));
                        expectedRoles.Add(AssetReferenceKey(expected, F10V7C1MeasuredAssetRole.RoomSource));
                    }
                    else if (expected.Kind == EncounterCatalogDescriptorKind.Enemy)
                        expectedRoles.Add(AssetReferenceKey(expected, F10V7C1MeasuredAssetRole.EnemyPrefab));
                    else return false;
                }
                if (_references.Length != expectedRoles.Count) return false;
                foreach (F10V7C1MeasuredAssetReference reference in _references)
                {
                    EncounterCatalogDescriptor expected = plan.UsedDescriptors.SingleOrDefault(descriptor =>
                        descriptor != null && descriptor.Revision == reference.Revision &&
                        string.Equals(descriptor.DescriptorId, reference.DescriptorId, StringComparison.Ordinal));
                    if (expected == null || !expectedRoles.Remove(AssetReferenceKey(expected, reference.Role)) ||
                        reference.Kind != expected.Kind ||
                        !string.Equals(reference.DescriptorSha256, expected.Sha256, StringComparison.Ordinal) ||
                        !IsLowerF10V7C1Sha256(reference.FullDescriptorSha256) ||
                        !string.Equals(reference.FullDescriptorSha256,
                            PhysicalDescriptorPayloadSha256, StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(reference.AssetPath) || string.IsNullOrWhiteSpace(reference.Guid) ||
                        string.IsNullOrWhiteSpace(reference.LocalId) || !IsLowerF10V7C1Sha256(reference.AssetSha256) ||
                        !IsLowerF10V7C1Sha256(reference.MetaSha256) || reference.Asset == null) return false;
                }
                return expectedRoles.Count == 0;
            }

            internal bool TryGet(EncounterCatalogDescriptorRef descriptorRef,
                F10V7C1MeasuredAssetRole role,
                out F10V7C1MeasuredAssetReference reference)
            {
                reference = null;
                if (descriptorRef == null) return false;
                reference = _references.SingleOrDefault(candidate => candidate.Revision == descriptorRef.Revision &&
                    candidate.Role == role &&
                    string.Equals(candidate.DescriptorId, descriptorRef.DescriptorId, StringComparison.Ordinal));
                return reference != null;
            }

            internal bool TryGet(string descriptorId, int revision,
                F10V7C1MeasuredAssetRole role,
                out F10V7C1MeasuredAssetReference reference)
            {
                reference = _references.SingleOrDefault(candidate => candidate.Revision == revision &&
                    candidate.Role == role &&
                    string.Equals(candidate.DescriptorId, descriptorId, StringComparison.Ordinal));
                return reference != null;
            }

            private static string AssetReferenceKey(EncounterCatalogDescriptor descriptor,
                F10V7C1MeasuredAssetRole role) =>
                F10V7C1DescriptorKey(descriptor.DescriptorId, descriptor.Revision) + "\0" +
                ((int)role).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        internal sealed class F10V7C1MeasuredAssetReference
        {
            internal F10V7C1MeasuredAssetReference(object factoryKey, string descriptorId,
                int revision, EncounterCatalogDescriptorKind kind, F10V7C1MeasuredAssetRole role,
                string descriptorSha256,
                string fullDescriptorSha256, string assetPath, string guid, string localId,
                string assetSha256, string metaSha256, UnityEngine.Object asset)
            {
                if (!ReferenceEquals(factoryKey, F10V7C1MeasuredAssetFactoryKey))
                    throw new InvalidOperationException(
                        "Only App's reviewed provenance importer may mint measured asset references.");
                DescriptorId = descriptorId ?? throw new ArgumentNullException(nameof(descriptorId));
                Revision = revision; Kind = kind; Role = role;
                DescriptorSha256 = descriptorSha256 ?? throw new ArgumentNullException(nameof(descriptorSha256));
                FullDescriptorSha256 = fullDescriptorSha256 ??
                    throw new ArgumentNullException(nameof(fullDescriptorSha256));
                AssetPath = assetPath ?? throw new ArgumentNullException(nameof(assetPath));
                Guid = guid ?? throw new ArgumentNullException(nameof(guid));
                LocalId = localId ?? throw new ArgumentNullException(nameof(localId));
                AssetSha256 = assetSha256 ?? throw new ArgumentNullException(nameof(assetSha256));
                MetaSha256 = metaSha256 ?? throw new ArgumentNullException(nameof(metaSha256));
                Asset = asset ?? throw new ArgumentNullException(nameof(asset));
            }
            internal string DescriptorId { get; }
            internal int Revision { get; }
            internal EncounterCatalogDescriptorKind Kind { get; }
            internal F10V7C1MeasuredAssetRole Role { get; }
            internal string DescriptorSha256 { get; }
            /// <summary>SHA-256 of the exact physical-descriptor.json payload, not the composite row fingerprint.</summary>
            internal string FullDescriptorSha256 { get; }
            internal string AssetPath { get; }
            internal string Guid { get; }
            internal string LocalId { get; }
            internal string AssetSha256 { get; }
            internal string MetaSha256 { get; }
            internal UnityEngine.Object Asset { get; }
        }
    }

    /// <summary>Pure roster projection plus physical C1 adapter over App-issued measured assets.</summary>
    internal static class F10V7C1RuntimePlanAdapter
    {
        private const float MarkerPositionTolerance = 0.001f;

        internal static bool TryValidatePinnedC1Plan(ResolvedEncounterPlanV1 plan,
            EncounterPlanPinV1 pin, ExpeditionRoomCheckpointSaveDataV7 checkpoint,
            out string planSha256, out string error)
        {
            planSha256 = string.Empty;
            error = string.Empty;
            if (plan == null || pin == null || checkpoint == null || pin.Entry == null ||
                checkpoint.encounterEntry == null)
                return Fail("Resolved C1 plan, V7 pin entry, and typed checkpoint entry are required.",
                    out error);
            if (plan.Mode != EncounterCatalogBindingMode.Encounter || plan.SelectedVariant == null ||
                !string.Equals(plan.NodeId, ApplicationBootstrapper.F10V7C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(plan.TemplateId, ApplicationBootstrapper.F10V7C1TemplateId, StringComparison.Ordinal) ||
                plan.NodeKind != ExpeditionRouteNodeKind.Combat || plan.GenerationVersion != 2 ||
                plan.RoomInstanceId != ExpeditionRoomIdentity.ForNode(plan.RunId,
                    ApplicationBootstrapper.F10V7C1NodeId) ||
                !string.Equals(checkpoint.roomNodeId, ApplicationBootstrapper.F10V7C1NodeId,
                    StringComparison.Ordinal))
                return Fail("The restored plan or typed checkpoint is not the exact F10 C1 combat node.",
                    out error);

            try { planSha256 = ResolvedEncounterPlanCanonicalV1.ComputeSha256(plan); }
            catch (Exception exception)
            { return Fail("The resolved C1 plan has no canonical digest: " + exception.Message, out error); }

            if (!string.Equals(pin.Entry.NodeId, ApplicationBootstrapper.F10V7C1NodeId,
                    StringComparison.Ordinal) ||
                !string.Equals(pin.Entry.ResolvedPlanSha256, planSha256, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.encounterEntry.nodeId,
                    ApplicationBootstrapper.F10V7C1NodeId, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.encounterEntry.resolvedPlanSha256,
                    planSha256, StringComparison.Ordinal))
                return Fail("Canonical C1 plan digest differs from pin.entry or checkpoint.encounterEntry.",
                    out error);

            return TryDeriveRoster(plan, out _, out error);
        }

        internal static bool TryDeriveRoster(ResolvedEncounterPlanV1 plan,
            out ExpeditionEncounterRosterPlan roster, out string error)
        {
            roster = null;
            error = string.Empty;
            if (plan == null || plan.Mode != EncounterCatalogBindingMode.Encounter ||
                plan.SelectedVariant == null || plan.SelectedVariant.Waves == null ||
                plan.SelectedVariant.Waves.Count == 0 ||
                !string.Equals(plan.NodeId, ApplicationBootstrapper.F10V7C1NodeId,
                    StringComparison.Ordinal) ||
                !string.Equals(plan.TemplateId, ApplicationBootstrapper.F10V7C1TemplateId,
                    StringComparison.Ordinal) || plan.NodeKind != ExpeditionRouteNodeKind.Combat ||
                plan.GenerationVersion != 2 ||
                plan.RoomInstanceId != ExpeditionRoomIdentity.ForNode(plan.RunId,
                    ApplicationBootstrapper.F10V7C1NodeId))
                return Fail("A non-empty resolved F10 C1 combat plan is required to derive the runtime roster.",
                    out error);

            var identities = new Dictionary<string, EncounterSpawnIdentityV1>(StringComparer.Ordinal);
            foreach (EncounterSpawnIdentityV1 identity in plan.SpawnIdentities)
            {
                if (identity == null || string.IsNullOrWhiteSpace(identity.LocalSpawnId) ||
                    identities.ContainsKey(identity.LocalSpawnId) || identity.RunId != plan.RunId ||
                    identity.RouteGenerationVersion != checked((uint)plan.GenerationVersion) ||
                    !string.Equals(identity.NodeId, plan.NodeId, StringComparison.Ordinal) ||
                    identity.RoomInstanceId != plan.RoomInstanceId ||
                    !string.Equals(identity.EncounterId, plan.EncounterId, StringComparison.Ordinal))
                    return Fail("The resolved plan contains duplicate or mismatched spawn identity tuples.", out error);
                try { identity.EnsureCanonicalKeyMatches(identity.Key); }
                catch (Exception exception)
                { return Fail("A resolved spawn identity is not canonical: " + exception.Message, out error); }
                identities.Add(identity.LocalSpawnId, identity);
            }

            var seenSpawnIds = new HashSet<string>(StringComparer.Ordinal);
            var waves = new List<ExpeditionEncounterWavePlan>(plan.SelectedVariant.Waves.Count);
            for (int waveIndex = 0; waveIndex < plan.SelectedVariant.Waves.Count; waveIndex++)
            {
                EncounterCatalogWave sourceWave = plan.SelectedVariant.Waves[waveIndex];
                if (sourceWave == null || sourceWave.Spawns == null || sourceWave.Spawns.Count == 0)
                    return Fail("Every C1 encounter wave must contain at least one pinned spawn.", out error);
                var spawnKeys = new List<string>(sourceWave.Spawns.Count);
                foreach (EncounterCatalogSpawn spawn in sourceWave.Spawns)
                {
                    if (spawn == null || string.IsNullOrWhiteSpace(spawn.SpawnId) ||
                        string.IsNullOrWhiteSpace(spawn.SpawnAnchorId) ||
                        !seenSpawnIds.Add(spawn.SpawnId) ||
                        !identities.TryGetValue(spawn.SpawnId,
                            out EncounterSpawnIdentityV1 identity))
                        return Fail("The selected C1 variant has a missing, duplicate, or unbound spawn ID.",
                            out error);
                    spawnKeys.Add(identity.Key);
                }
                waves.Add(new ExpeditionEncounterWavePlan(waveIndex + 1, spawnKeys));
            }
            if (seenSpawnIds.Count == 0 || seenSpawnIds.Count != identities.Count ||
                identities.Keys.Any(spawnId => !seenSpawnIds.Contains(spawnId)))
                return Fail("Resolved spawn identities contain missing or extra C1 spawns.", out error);

            try
            {
                roster = new ExpeditionEncounterRosterPlan(waves);
                if (roster.Waves.Count != plan.SelectedVariant.Waves.Count)
                    return Fail("The derived C1 roster does not preserve the selected wave count.", out error);
            }
            catch (Exception exception)
            { return Fail("The pinned C1 roster could not be adapted: " + exception.Message, out error); }
            return true;
        }

        internal static bool TryBuildRuntimePlan(ApplicationBootstrapper bootstrapper,
            ApplicationBootstrapper.F10V7SessionInitializationBinding sessionBinding,
            long revision, RoomLifetimeScope scope, RoomTemplatePrefab2D room,
            ResolvedEncounterPlanV1 plan,
            ApplicationBootstrapper.F10V7C1MeasuredAssetSnapshot measuredAssets,
            out RoomEncounterRuntimePlan runtimePlan, out string error)
        {
            runtimePlan = null;
            return measuredAssets == null
                ? Fail("No App-retained measured asset snapshot is available, and this legacy overload cannot prove the exact App-captured room transition controller.",
                    out error)
                : Fail("This legacy overload cannot prove the exact App-captured room transition controller.",
                    out error);
        }

        internal static bool TryBuildRuntimePlan(ApplicationBootstrapper bootstrapper,
            ApplicationBootstrapper.F10V7SessionInitializationBinding sessionBinding,
            long revision, RoomLifetimeScope scope, RoomTemplatePrefab2D room,
            ResolvedEncounterPlanV1 plan,
            ApplicationBootstrapper.F10V7C1MeasuredAssetSnapshot measuredAssets,
            ExpeditionRoomTransitionController controller,
            out RoomEncounterRuntimePlan runtimePlan, out string error)
        {
            runtimePlan = null;
            error = string.Empty;
            try
            {
                if (!TryDeriveRoster(plan, out ExpeditionEncounterRosterPlan roster, out error)) return false;
                ExpeditionGameplayComposition composition = sessionBinding == null
                    ? null : sessionBinding.Composition;
                if (bootstrapper == null || composition == null ||
                    !sessionBinding.IsOwnedBy(bootstrapper, composition) || controller == null ||
                    !ReferenceEquals(controller, composition.RoomTransitions))
                    return Fail("The C1 adapter requires the room transition controller retained by the exact App session binding.",
                        out error);
                if (bootstrapper == null || sessionBinding == null || scope == null || room == null ||
                    measuredAssets == null ||
                    !measuredAssets.IsBoundTo(bootstrapper, sessionBinding, revision, scope, room, plan) ||
                    !measuredAssets.HasExactDescriptors(plan))
                    return Fail("The App has no exact provenance-verified measured asset snapshot for this plan/scope/revision.",
                        out error);
                if (!TryValidateC1DescriptorSet(plan, out error) ||
                    !TryValidateDescriptorFingerprints(plan, measuredAssets, out error) ||
                    !TryResolveAndValidateRoom(plan, measuredAssets, controller, scope, room,
                        out RoomTemplateData roomData, out error)) return false;

                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer < 0 || LayerMask.GetMask("Ground") != (1 << groundLayer))
                    return Fail("The exact Ground layer is unavailable for the C1 actor adapters.", out error);
                LayerMask groundMask = 1 << groundLayer;
                var actorWaves = new List<RoomEncounterWaveActorDefinition>(plan.SelectedVariant.Waves.Count);
                for (int waveIndex = 0; waveIndex < plan.SelectedVariant.Waves.Count; waveIndex++)
                {
                    EncounterCatalogWave sourceWave = plan.SelectedVariant.Waves[waveIndex];
                    if (sourceWave == null || sourceWave.Spawns == null || sourceWave.Spawns.Count == 0)
                        return Fail("Every resolved C1 wave must contain at least one spawn.", out error);

                    var actorDefinitions = new List<RoomEncounterActorDefinition>(sourceWave.Spawns.Count);
                    foreach (EncounterCatalogSpawn spawn in sourceWave.Spawns)
                    {
                        if (!TryResolveSupportedEnemy(spawn, measuredAssets,
                                out GameObject actorPrefab, out error) ||
                            !TryResolveC1SpawnAnchor(room, roomData, spawn.SpawnAnchorId,
                                out RoomPositionMarker2D anchor, out error)) return false;

                        Vector3 scopeLocalPosition = scope.transform.InverseTransformPoint(anchor.transform.position);
                        Quaternion scopeLocalRotation = Quaternion.Inverse(scope.transform.rotation) *
                            anchor.transform.rotation;
                        if (!IsFinite(scopeLocalPosition) || !IsFinite(scopeLocalRotation))
                            return Fail("A resolved C1 spawn anchor has a non-finite scope-local pose.", out error);

                        EncounterSpawnIdentityV1 identity = plan.GetSpawnIdentity(spawn.SpawnId);
                        var actor = new RoomEncounterActorDefinition(identity.Key, actorPrefab,
                            groundMask, scopeLocalPosition, scopeLocalRotation);
                        if (!actor.SupportsConcreteActorAdapter)
                            return Fail("A pinned C1 enemy does not resolve to one supported Slime or Huesoguardia prefab.",
                                out error);
                        actorDefinitions.Add(actor);
                    }

                    actorWaves.Add(new RoomEncounterWaveActorDefinition(waveIndex + 1, actorDefinitions));
                }

                var candidate = new RoomEncounterRuntimePlan(roster, actorWaves);
                if (!candidate.HasCompleteCoverage || !candidate.HasOnlySupportedActors)
                    return Fail("The measured C1 runtime plan does not cover the exact pinned roster with supported actors.",
                        out error);
                runtimePlan = candidate;
                return true;
            }
            catch (Exception exception)
            {
                runtimePlan = null;
                return Fail("The measured C1 runtime adapter failed closed: " +
                    exception.GetType().Name + ".", out error);
            }
        }

        internal static bool TryValidateCompositeDescriptorFingerprint(
            EncounterCatalogDescriptor descriptor, string fullDescriptorSha256,
            string physicalDescriptorPayloadSha256, string bundleManifestSha256,
            out string error)
        {
            error = string.Empty;
            if (descriptor == null || descriptor.Revision <= 0 ||
                descriptor.Kind != EncounterCatalogDescriptorKind.Room &&
                descriptor.Kind != EncounterCatalogDescriptorKind.Enemy ||
                !IsLowerSha256(fullDescriptorSha256) ||
                !IsLowerSha256(physicalDescriptorPayloadSha256) ||
                !IsLowerSha256(bundleManifestSha256) ||
                !string.Equals(fullDescriptorSha256, physicalDescriptorPayloadSha256,
                    StringComparison.Ordinal))
                return Fail("The exact physical descriptor payload and bundle manifest must be measured SHA-256 values.",
                    out error);

            if (!F10PhysicalDescriptorTableV1.TryComputeFingerprint(descriptor.DescriptorId,
                    descriptor.Revision, physicalDescriptorPayloadSha256,
                    bundleManifestSha256, out F10PhysicalDescriptorFingerprintResultV1 measured) ||
                !string.Equals(measured.Descriptor.Kind,
                    descriptor.Kind == EncounterCatalogDescriptorKind.Room ? "room" : "enemy",
                    StringComparison.Ordinal) ||
                !string.Equals(descriptor.Sha256, measured.Fingerprint, StringComparison.Ordinal))
                return Fail("The descriptor identity does not match the fixed-table composite fingerprint for this exact payload and bundle manifest.",
                    out error);
            return true;
        }

        private static bool TryValidateC1DescriptorSet(ResolvedEncounterPlanV1 plan,
            out string error)
        {
            error = string.Empty;
            if (plan?.SelectedVariant?.RoomDescriptorRef == null || plan.UsedDescriptors == null ||
                plan.SelectedVariant.Waves == null)
                return Fail("The resolved C1 plan is missing its room descriptor or waves.", out error);

            var referenced = new HashSet<string>(StringComparer.Ordinal);
            EncounterCatalogDescriptorRef roomReference = plan.SelectedVariant.RoomDescriptorRef;
            if (!string.Equals(roomReference.DescriptorId, "room.patio.c1", StringComparison.Ordinal) ||
                !referenced.Add(DescriptorKey(roomReference.DescriptorId, roomReference.Revision)))
                return Fail("The resolved C1 variant must reference exactly the fixed C1 room descriptor.", out error);

            foreach (EncounterCatalogWave wave in plan.SelectedVariant.Waves)
            {
                if (wave == null || wave.Spawns == null) return Fail("The resolved C1 plan contains an invalid wave.", out error);
                foreach (EncounterCatalogSpawn spawn in wave.Spawns)
                {
                    if (spawn == null || spawn.EnemyDescriptorRef == null ||
                        !string.Equals(spawn.EnemyId, spawn.EnemyDescriptorRef.DescriptorId,
                            StringComparison.Ordinal) ||
                        spawn.EnemyId != "enemy.slime" && spawn.EnemyId != "enemy.huesoguardia" ||
                        spawn.EnemyDescriptorRef.Revision <= 0)
                        return Fail("Every C1 spawn must resolve once to its own supported enemy descriptor.", out error);
                    referenced.Add(DescriptorKey(spawn.EnemyDescriptorRef.DescriptorId,
                        spawn.EnemyDescriptorRef.Revision));
                }
            }

            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (EncounterCatalogDescriptor descriptor in plan.UsedDescriptors)
            {
                if (descriptor == null ||
                    !used.Add(DescriptorKey(descriptor.DescriptorId, descriptor.Revision)) ||
                    !referenced.Contains(DescriptorKey(descriptor.DescriptorId, descriptor.Revision)))
                    return Fail("The resolved C1 descriptor set contains a duplicate or unreferenced asset.", out error);
                EncounterCatalogDescriptorKind expectedKind =
                    string.Equals(descriptor.DescriptorId, "room.patio.c1", StringComparison.Ordinal)
                        ? EncounterCatalogDescriptorKind.Room
                        : EncounterCatalogDescriptorKind.Enemy;
                if (descriptor.Kind != expectedKind)
                    return Fail("The resolved C1 descriptor kind does not match the fixed physical descriptor table.", out error);
            }
            return referenced.SetEquals(used)
                ? true
                : Fail("The resolved C1 descriptor set omits a referenced room or enemy asset.", out error);
        }

        private static bool TryValidateDescriptorFingerprints(ResolvedEncounterPlanV1 plan,
            ApplicationBootstrapper.F10V7C1MeasuredAssetSnapshot measuredAssets,
            out string error)
        {
            error = string.Empty;
            foreach (EncounterCatalogDescriptor descriptor in plan.UsedDescriptors)
            {
                ApplicationBootstrapper.F10V7C1MeasuredAssetRole role =
                    descriptor.Kind == EncounterCatalogDescriptorKind.Room
                        ? ApplicationBootstrapper.F10V7C1MeasuredAssetRole.RoomPrefab
                        : ApplicationBootstrapper.F10V7C1MeasuredAssetRole.EnemyPrefab;
                if (!measuredAssets.TryGet(descriptor.DescriptorId, descriptor.Revision, role,
                        out ApplicationBootstrapper.F10V7C1MeasuredAssetReference reference) ||
                    !TryValidateCompositeDescriptorFingerprint(descriptor,
                        reference.FullDescriptorSha256,
                        measuredAssets.PhysicalDescriptorPayloadSha256,
                        measuredAssets.BundleManifestSha256, out error)) return false;
            }
            return true;
        }

        private static bool TryResolveAndValidateRoom(ResolvedEncounterPlanV1 plan,
            ApplicationBootstrapper.F10V7C1MeasuredAssetSnapshot measuredAssets,
            ExpeditionRoomTransitionController controller,
            RoomLifetimeScope scope, RoomTemplatePrefab2D room,
            out RoomTemplateData roomData, out string error)
        {
            roomData = null;
            error = string.Empty;
            if (!Application.isPlaying || scope == null || room == null ||
                !scope.isActiveAndEnabled || scope.IsDisposed ||
                scope.RoomInstanceId != plan.RoomInstanceId ||
                !string.Equals(scope.TemplateId, ApplicationBootstrapper.F10V7C1TemplateId,
                    StringComparison.Ordinal) ||
                !room.isActiveAndEnabled || !room.gameObject.activeInHierarchy ||
                !room.transform.IsChildOf(scope.transform))
                return Fail("The adapter requires the exact active C1 room scope and room instance.", out error);

            RoomTemplatePrefab2D[] activeRooms = scope.GetComponentsInChildren<RoomTemplatePrefab2D>(true);
            if (activeRooms.Length != 1 || activeRooms[0] != room)
                return Fail("The active C1 scope must contain exactly one RoomTemplatePrefab2D.", out error);

            EncounterCatalogDescriptorRef roomDescriptor = plan.SelectedVariant.RoomDescriptorRef;
            if (!measuredAssets.TryGet(roomDescriptor,
                    ApplicationBootstrapper.F10V7C1MeasuredAssetRole.RoomPrefab,
                    out ApplicationBootstrapper.F10V7C1MeasuredAssetReference prefabReference) ||
                !measuredAssets.TryGet(roomDescriptor,
                    ApplicationBootstrapper.F10V7C1MeasuredAssetRole.RoomSource,
                    out ApplicationBootstrapper.F10V7C1MeasuredAssetReference sourceReference) ||
                !F10PhysicalDescriptorTableV1.TryGetDescriptor(roomDescriptor.DescriptorId,
                    out F10PhysicalDescriptorV1 fixedRoom) ||
                !TryValidateMeasuredAssetReference(prefabReference, fixedRoom.RootAssetPath,
                    typeof(GameObject), out error) ||
                !TryValidateMeasuredAssetReference(sourceReference, fixedRoom.SourceDefinitionAssetPath,
                    typeof(TextAsset), out error)) return false;

            GameObject roomPrefab = prefabReference.Asset as GameObject;
            TextAsset roomSource = sourceReference.Asset as TextAsset;
            RoomTemplatePrefab2D[] prefabRooms = roomPrefab == null
                ? Array.Empty<RoomTemplatePrefab2D>()
                : roomPrefab.GetComponentsInChildren<RoomTemplatePrefab2D>(true);
            if (roomPrefab == null || roomSource == null || prefabRooms.Length != 1 ||
                prefabRooms[0] == null || prefabRooms[0].gameObject != roomPrefab ||
                prefabRooms[0].SourceDefinition != roomSource || room.SourceDefinition != roomSource)
                return Fail("The active C1 room and its persistent prefab must retain the exact measured TextAsset source.",
                    out error);

            if (controller == null || measuredAssets.SessionBinding?.Composition == null ||
                !ReferenceEquals(controller,
                    measuredAssets.SessionBinding.Composition.RoomTransitions) ||
                !controller.TryGetActiveRoomPrefabSource(scope, room,
                    out GameObject sourcePrefab) || !ReferenceEquals(sourcePrefab, roomPrefab))
                return Fail("The App-retained transition controller does not prove that this exact active room was instantiated from the measured persistent C1 prefab.",
                    out error);

            Transform instanceTransform = room.transform;
            Transform prefabTransform = prefabRooms[0].transform;
            if (!IsNear(instanceTransform.localPosition, prefabTransform.localPosition) ||
                Quaternion.Angle(instanceTransform.localRotation, prefabTransform.localRotation) > MarkerPositionTolerance ||
                !IsNear(instanceTransform.localScale, prefabTransform.localScale) ||
                Quaternion.Angle(instanceTransform.rotation, prefabTransform.rotation) > MarkerPositionTolerance ||
                !IsNear(instanceTransform.lossyScale, prefabTransform.lossyScale))
                return Fail("The room instance may inherit transition world placement, but may not override prefab root pose or scale.",
                    out error);

            try
            {
                RoomTemplateData prefabData = prefabRooms[0].ValidateGeneratedPrefab();
                roomData = room.ValidateGeneratedPrefab();
                if (prefabData == null || roomData == null || prefabData.schemaVersion != 1 ||
                    roomData.schemaVersion != 1 ||
                    !string.Equals(prefabData.templateId, ApplicationBootstrapper.F10V7C1TemplateId,
                        StringComparison.Ordinal) ||
                    !string.Equals(roomData.templateId, ApplicationBootstrapper.F10V7C1TemplateId,
                        StringComparison.Ordinal))
                    return Fail("The measured room prefab and active instance must pass the exact C1 JSON geometry validation.",
                        out error);
            }
            catch (Exception exception)
            {
                return Fail("The measured C1 prefab or active room failed source/geometry/marker validation: " +
                    exception.GetType().Name + ".", out error);
            }
            return true;
        }

        private static bool TryResolveSupportedEnemy(EncounterCatalogSpawn spawn,
            ApplicationBootstrapper.F10V7C1MeasuredAssetSnapshot measuredAssets,
            out GameObject actorPrefab, out string error)
        {
            actorPrefab = null;
            error = string.Empty;
            if (spawn == null || spawn.EnemyDescriptorRef == null ||
                !string.Equals(spawn.EnemyId, spawn.EnemyDescriptorRef.DescriptorId,
                    StringComparison.Ordinal) ||
                spawn.EnemyId != "enemy.slime" && spawn.EnemyId != "enemy.huesoguardia" ||
                !F10PhysicalDescriptorTableV1.TryGetDescriptor(spawn.EnemyId,
                    out F10PhysicalDescriptorV1 descriptor) ||
                !measuredAssets.TryGet(spawn.EnemyDescriptorRef,
                    ApplicationBootstrapper.F10V7C1MeasuredAssetRole.EnemyPrefab,
                    out ApplicationBootstrapper.F10V7C1MeasuredAssetReference reference) ||
                !TryValidateMeasuredAssetReference(reference, descriptor.RootAssetPath,
                    typeof(GameObject), out error))
                return Fail("A C1 spawn must resolve through the fixed table to a measured Slime or Huesoguardia prefab.",
                    out error);
            actorPrefab = reference.Asset as GameObject;
            return actorPrefab != null || Fail("The measured enemy prefab is not a GameObject.", out error);
        }

        internal static bool TryResolveC1SpawnAnchor(RoomTemplatePrefab2D room,
            RoomTemplateData roomData, string spawnAnchorId,
            out RoomPositionMarker2D anchor, out string error)
        {
            anchor = null;
            error = string.Empty;
            if (room == null || roomData == null || string.IsNullOrWhiteSpace(spawnAnchorId))
                return Fail("A resolved C1 spawn requires its direct JSON SpawnAnchorId.", out error);

            RoomPositionMarkerData[] definitions = (roomData.markers ?? Array.Empty<RoomPositionMarkerData>())
                .Where(marker => marker != null && string.Equals(marker.id, spawnAnchorId,
                    StringComparison.Ordinal)).ToArray();
            RoomPositionMarker2D[] components = room.GetComponentsInChildren<RoomPositionMarker2D>(true)
                .Where(marker => marker != null && string.Equals(marker.MarkerId, spawnAnchorId,
                    StringComparison.Ordinal)).ToArray();
            if (definitions.Length != 1 || components.Length != 1 ||
                !string.Equals(definitions[0].kind, "enemy_spawn", StringComparison.Ordinal) ||
                !string.Equals(components[0].Kind, "enemy_spawn", StringComparison.Ordinal) ||
                components[0].TilePosition != new Vector2Int(definitions[0].tileX, definitions[0].tileY) ||
                !components[0].enabled || !components[0].gameObject.activeInHierarchy ||
                !components[0].transform.IsChildOf(room.transform))
                return Fail("The direct C1 SpawnAnchorId must resolve to one active enemy_spawn JSON marker and child component.",
                    out error);

            Vector3 expectedLocal = new Vector3((definitions[0].tileX + 0.5f) * roomData.tileSize,
                definitions[0].tileY * roomData.tileSize, 0f);
            Vector3 expectedWorld = room.transform.TransformPoint(expectedLocal);
            if (!IsNear(components[0].transform.localPosition, expectedLocal) ||
                Quaternion.Angle(components[0].transform.localRotation, Quaternion.identity) > MarkerPositionTolerance ||
                !IsNear(components[0].transform.localScale, Vector3.one) ||
                !IsNear(components[0].transform.position, expectedWorld))
                return Fail("The C1 enemy_spawn marker pose differs from its exact JSON tile position.", out error);

            anchor = components[0];
            return true;
        }

        private static bool TryValidateMeasuredAssetReference(
            ApplicationBootstrapper.F10V7C1MeasuredAssetReference reference,
            string expectedPath, Type expectedType, out string error)
        {
            error = string.Empty;
            if (reference == null || string.IsNullOrWhiteSpace(expectedPath) ||
                reference.Asset == null || !expectedType.IsInstanceOfType(reference.Asset) ||
                !EditorUtility.IsPersistent(reference.Asset) ||
                !string.Equals(reference.AssetPath, expectedPath, StringComparison.Ordinal) ||
                !string.Equals(AssetDatabase.GetAssetPath(reference.Asset), expectedPath,
                    StringComparison.Ordinal) || !AssetDatabase.IsMainAsset(reference.Asset) ||
                AssetDatabase.LoadMainAssetAtPath(expectedPath) != reference.Asset ||
                !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(reference.Asset,
                    out string currentGuid, out long currentLocalId) ||
                !string.Equals(currentGuid, reference.Guid, StringComparison.Ordinal) ||
                !string.Equals(currentLocalId.ToString(CultureInfo.InvariantCulture),
                    reference.LocalId, StringComparison.Ordinal))
                return Fail("A measured C1 asset no longer matches its fixed path, persistent object, GUID, or local ID.",
                    out error);

            DirectoryInfo projectRoot = Directory.GetParent(Application.dataPath);
            if (projectRoot == null)
                return Fail("The Unity project root cannot be resolved for measured C1 asset revalidation.", out error);
            string diskPath = Path.GetFullPath(Path.Combine(projectRoot.FullName,
                expectedPath.Replace('/', Path.DirectorySeparatorChar)));
            if (!TryComputeFileSha256(diskPath, out string assetSha256) ||
                !TryComputeFileSha256(diskPath + ".meta", out string metaSha256) ||
                !string.Equals(assetSha256, reference.AssetSha256, StringComparison.Ordinal) ||
                !string.Equals(metaSha256, reference.MetaSha256, StringComparison.Ordinal))
                return Fail("A measured C1 asset or .meta hash changed after provenance capture.", out error);
            return true;
        }

        private static bool TryComputeFileSha256(string path, out string digest)
        {
            digest = string.Empty;
            try
            {
                using (FileStream stream = File.OpenRead(path))
                using (SHA256 sha256 = SHA256.Create())
                {
                    byte[] bytes = sha256.ComputeHash(stream);
                    var builder = new StringBuilder(bytes.Length * 2);
                    foreach (byte value in bytes) builder.Append(value.ToString("x2", CultureInfo.InvariantCulture));
                    digest = builder.ToString();
                }
                return IsLowerSha256(digest);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
            catch (ArgumentException) { return false; }
        }

        private static bool IsNear(Vector3 actual, Vector3 expected) =>
            IsFinite(actual) && IsFinite(expected) &&
            (actual - expected).sqrMagnitude <= MarkerPositionTolerance * MarkerPositionTolerance;

        private static bool IsFinite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static bool IsFinite(Quaternion value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z) &&
            !float.IsNaN(value.w) && !float.IsInfinity(value.w);

        private static bool Fail(string message, out string error)
        {
            error = string.IsNullOrWhiteSpace(message)
                ? "V7 C1 plan adaptation failed closed."
                : message;
            return false;
        }

        private static string DescriptorKey(string descriptorId, int revision) =>
            (descriptorId ?? string.Empty) + "\0" + revision.ToString(System.Globalization.CultureInfo.InvariantCulture);

        private static bool IsLowerSha256(string value) => value != null && value.Length == 64 &&
            value.All(character => character >= '0' && character <= '9' || character >= 'a' && character <= 'f');
    }
}
#endif
