#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    /// Private C1→E1 V7 transaction gateway. Physical authority is checked before this gateway
    /// acquires the profile gate; the exact pending then owns the gate, lease, candidate, clear,
    /// binder publication and E1 reservation through readback and activation.
    /// </summary>
    public sealed partial class ApplicationBootstrapper
    {
        private static readonly UTF8Encoding StrictF10V7C1PaymentUtf8 = new UTF8Encoding(false, true);
        private F10V7C1CombatPaymentPending _pendingF10V7C1CombatPayment;
        private F10V7C1PaymentResourceCapture _f10V7C1PaymentResourceCapture;
        private ExpeditionGameplayComposition _f10V7C1PaymentGateOwner;
        private object _f10V7C1PaymentGateOwnerToken;

        /// <summary>Starts a C1 payment only from the coordinator's exact private physical clear.</summary>
        internal Task<F10V7C1CombatPaymentOutcome> CompleteAndAdvanceF10V7CombatAsync(
            ExpeditionGameplayComposition owner, RoomEncounterRuntime runtime,
            RoomEncounterClearObservation clearObservation)
        {
            F10V7C1CombatPaymentPending existing = _pendingF10V7C1CombatPayment;
            if (existing != null)
            {
                if (ReferenceEquals(existing.Owner, owner) && ReferenceEquals(existing.Runtime, runtime) &&
                    ReferenceEquals(existing.ClearObservation, clearObservation))
                    return ExecuteF10V7C1CombatPaymentPendingAsync(existing);
                return Task.FromResult(F10V7C1CombatPaymentOutcome.PendingResult(
                    "An exact App-owned C1 payment is retained; a different clear cannot replace or duplicate it."));
            }

            if (!TryCaptureCurrentF10V7C1PaymentInputs(owner, runtime, clearObservation, null,
                    out F10V7SessionInitializationBinding binding,
                    out ExpeditionSessionReadinessRegistration registration,
                    out ExpeditionReadinessCompositionView readiness,
                    out RoomEncounterRuntimeContext context,
                    out ExpeditionRoomTransitionController controller,
                    out F10C1RuntimeCoordinator coordinator,
                    out F10V7CombatPlanRuntimeBinding planBinding,
                    out long revision, out string error))
                return Task.FromResult(F10V7C1CombatPaymentOutcome.Rejected(error));

            return StartF10V7C1CombatPaymentAsync(owner, binding, registration, readiness,
                context, controller, coordinator, planBinding, runtime, clearObservation, revision);
        }

        /// <summary>
        /// Retries the exact retained C1 transaction. It intentionally does not query the historical
        /// C1 coordinator, plan binding or scope; those references remain evidence in the pending.
        /// </summary>
        internal Task<F10V7C1CombatPaymentOutcome> RetryPendingF10V7CombatAdvanceAsync(
            ExpeditionGameplayComposition owner)
        {
            F10V7C1CombatPaymentPending pending = _pendingF10V7C1CombatPayment;
            if (pending == null)
                return Task.FromResult(F10V7C1CombatPaymentOutcome.Rejected(
                    "There is no retained V7 C1 payment to retry."));
            if (!ReferenceEquals(pending.Owner, owner))
                return Task.FromResult(F10V7C1CombatPaymentOutcome.PendingResult(
                    "The retained V7 C1 payment belongs to another exact App owner."));
            return ExecuteF10V7C1CombatPaymentPendingAsync(pending);
        }

        internal static bool MatchesF10V7C1PaymentGateIdentity(object requestedOwner,
            object requestedToken, object retainedOwner, object retainedToken) =>
            requestedOwner != null && requestedToken != null &&
            ReferenceEquals(requestedOwner, retainedOwner) &&
            ReferenceEquals(requestedToken, retainedToken);

        private bool IsExactF10V7C1PaymentGateOwner(
            ExpeditionGameplayComposition owner, object gateOwnerToken) =>
            MatchesF10V7C1PaymentGateIdentity(owner, gateOwnerToken,
                _f10V7C1PaymentGateOwner, _f10V7C1PaymentGateOwnerToken);

        private void ReleaseF10V7C1PaymentGate(
            ExpeditionGameplayComposition owner, object gateOwnerToken)
        {
            if (!IsExactF10V7C1PaymentGateOwner(owner, gateOwnerToken))
                throw new InvalidOperationException("Only the exact retained C1 payment gate owner may release the App gate.");
            _profileMutationGate.Release();
            _f10V7C1PaymentGateOwner = null;
            _f10V7C1PaymentGateOwnerToken = null;
        }

        private async Task<F10V7C1CombatPaymentOutcome> StartF10V7C1CombatPaymentAsync(
            ExpeditionGameplayComposition owner,
            F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessCompositionView readiness,
            RoomEncounterRuntimeContext context,
            ExpeditionRoomTransitionController controller,
            F10C1RuntimeCoordinator coordinator,
            F10V7CombatPlanRuntimeBinding planBinding,
            RoomEncounterRuntime runtime, RoomEncounterClearObservation clearObservation,
            long expectedRevision)
        {
            bool gateAcquired = false;
            bool resourcesTransferred = false;
            IDisposable pauseLease = null;
            object gateOwnerToken = new object();
            F10V7CombatAdvanceCandidate candidate = null;
            ExpeditionReadinessPreparedPublication publication = null;
            F10V7C1PaymentResourceCapture captureReceipt = null;
            ExpeditionRoomTransitionController.PreparedRoomTransition reservationForCleanup = null;
            string error = string.Empty;
            F10V7C1CombatPaymentOutcome outcome = null;
            F10V7C1CombatPaymentPending pending = null;

            try
            {
                await _profileMutationGate.WaitAsync();
                gateAcquired = true;
                _f10V7C1PaymentGateOwner = owner;
                _f10V7C1PaymentGateOwnerToken = gateOwnerToken;

                if (_pendingF10V7C1CombatPayment != null ||
                    !TryCaptureCurrentF10V7C1PaymentInputs(owner, runtime, clearObservation, gateOwnerToken,
                        out F10V7SessionInitializationBinding gatedBinding,
                        out ExpeditionSessionReadinessRegistration gatedRegistration,
                        out ExpeditionReadinessCompositionView gatedReadiness,
                        out RoomEncounterRuntimeContext gatedContext,
                        out ExpeditionRoomTransitionController gatedController,
                        out F10C1RuntimeCoordinator gatedCoordinator,
                        out F10V7CombatPlanRuntimeBinding gatedPlanBinding,
                        out long gatedRevision, out error) ||
                    !SameF10V7C1PaymentInputs(binding, registration, readiness, context,
                        controller, coordinator, planBinding, expectedRevision,
                        gatedBinding, gatedRegistration, gatedReadiness, gatedContext,
                        gatedController, gatedCoordinator, gatedPlanBinding, gatedRevision))
                {
                    outcome = F10V7C1CombatPaymentOutcome.Rejected(string.IsNullOrWhiteSpace(error)
                        ? "The C1 clear or V7 owner changed while waiting for the App mutation gate."
                        : error);
                }
                else if (!TryAcquireF10V7C1PaymentPauseLease(owner, binding, registration,
                             out pauseLease, out error))
                {
                    outcome = F10V7C1CombatPaymentOutcome.Rejected(error);
                }
                else if (!TryCaptureCurrentF10V7C1PaymentInputs(owner, runtime, clearObservation, gateOwnerToken,
                             out gatedBinding, out gatedRegistration, out gatedReadiness,
                             out gatedContext, out gatedController, out gatedCoordinator,
                             out gatedPlanBinding, out gatedRevision, out error) ||
                         !SameF10V7C1PaymentInputs(binding, registration, readiness, context,
                             controller, coordinator, planBinding, expectedRevision,
                             gatedBinding, gatedRegistration, gatedReadiness, gatedContext,
                             gatedController, gatedCoordinator, gatedPlanBinding, gatedRevision))
                {
                    outcome = F10V7C1CombatPaymentOutcome.Rejected(string.IsNullOrWhiteSpace(error)
                        ? "The exact physical C1 clear, binding, revision, or owner changed while acquiring its pause lease."
                        : error);
                }
                else if (!TryCaptureF10V7C1LiveResources(binding, owner, gatedReadiness,
                             gatedContext, planBinding, expectedRevision, pauseLease, gateOwnerToken,
                             out ExpeditionSaveData capturedCommonState, out ExpeditionInventory liveInventory,
                             out captureReceipt, out error))
                {
                    outcome = F10V7C1CombatPaymentOutcome.Rejected(error);
                }
                else
                {
                    _f10V7C1PaymentResourceCapture = captureReceipt;
                    try
                    {
                        if (!TryPrepareF10V7CombatAdvanceCandidate(binding, owner, context, runtime,
                                clearObservation, capturedCommonState, gateOwnerToken, out candidate,
                                out reservationForCleanup, out error))
                        {
                            outcome = F10V7C1CombatPaymentOutcome.Rejected(error);
                        }
                        else
                        {
                            string candidateCommonBeforeBinder = JsonUtility.ToJson(candidate.CandidateCommon);
                            if (!owner.TryPrepareF10V7C1PaymentPublication(binding,
                                    ActiveRun, candidate.CandidateRun, candidate.CandidateCommon,
                                    expectedRevision, out publication, out error) ||
                                publication == null || !string.Equals(candidateCommonBeforeBinder,
                                    JsonUtility.ToJson(candidate.CandidateCommon), StringComparison.Ordinal) ||
                                !ReferenceEquals(publication.CandidateSave, candidate.CandidateCommon) ||
                                !ReferenceEquals(publication.Run, candidate.CandidateRun) ||
                                !ReferenceEquals(publication.Activation, registration.CurrentActivation) ||
                                publication.PreparedAtRevision != expectedRevision ||
                                !candidate.PreparedTransition.IsPending ||
                                !ReferenceEquals(candidate.Controller, controller) ||
                                !ReferenceEquals(owner.RoomTransitions, controller))
                            {
                                if (string.IsNullOrWhiteSpace(error))
                                    error = "The binder, candidate, or exact prepared E1 reservation did not remain identical.";
                                outcome = F10V7C1CombatPaymentOutcome.Rejected(error);
                            }
                            else
                            {
                                pending = new F10V7C1CombatPaymentPending
                                {
                                    Owner = owner,
                                    Binding = binding,
                                    Registration = registration,
                                    Context = context,
                                    Controller = controller,
                                    Coordinator = coordinator,
                                    Runtime = runtime,
                                    PlanBinding = planBinding,
                                    ClearObservation = clearObservation,
                                    Candidate = candidate,
                                    PreparedTransition = candidate.PreparedTransition,
                                    BinderPublication = publication,
                                    ResourceCapture = captureReceipt,
                                    SourceProjection = captureReceipt.SourceProjection,
                                    SourceJson = candidate.SourceJson,
                                    ExpectedRevision = expectedRevision,
                                    GateOwnerToken = gateOwnerToken,
                                    SimulationPauseLease = pauseLease,
                                    MutationGateRetained = true
                                };
                                _pendingF10V7C1CombatPayment = pending;
                                pauseLease = null;
                                gateAcquired = false;
                                resourcesTransferred = true;
                            }
                        }
                    }
                    finally { _f10V7C1PaymentResourceCapture = null; }

                    if (resourcesTransferred)
                        outcome = await ExecuteF10V7C1CombatPaymentPendingAsync(pending);
                }
            }
            catch (Exception exception)
            {
                error = "The exact V7 C1 payment preparation failed closed: " + exception.Message;
                outcome = F10V7C1CombatPaymentOutcome.Rejected(error);
            }
            finally
            {
                if (!resourcesTransferred)
                {
                    Exception cleanupError = null;
                    ExpeditionRoomTransitionController.PreparedRoomTransition retainedReservation =
                        candidate?.PreparedTransition ?? reservationForCleanup;
                    if (retainedReservation != null && retainedReservation.IsPending)
                    {
                        try { retainedReservation.Dispose(); }
                        catch (Exception exception) { cleanupError = exception; }
                    }
                    if (cleanupError == null && pauseLease != null)
                    {
                        try { pauseLease.Dispose(); pauseLease = null; }
                        catch (Exception exception) { cleanupError = exception; }
                    }
                    if (cleanupError != null && gateAcquired)
                    {
                        pending = new F10V7C1CombatPaymentPending
                        {
                            Owner = owner,
                            Binding = binding,
                            Registration = registration,
                            Context = context,
                            Controller = controller,
                            Coordinator = coordinator,
                            Runtime = runtime,
                            PlanBinding = planBinding,
                            ClearObservation = clearObservation,
                            Candidate = candidate,
                            PreparedTransition = retainedReservation,
                            BinderPublication = publication,
                            ResourceCapture = captureReceipt,
                            SourceProjection = captureReceipt?.SourceProjection,
                            SourceJson = candidate?.SourceJson,
                            ExpectedRevision = expectedRevision,
                            GateOwnerToken = gateOwnerToken,
                            SimulationPauseLease = pauseLease,
                            MutationGateRetained = true,
                            UnwrittenCleanupFailed = true
                        };
                        _pendingF10V7C1CombatPayment = pending;
                        pauseLease = null;
                        gateAcquired = false;
                        outcome = F10V7C1CombatPaymentOutcome.PendingResult(
                            "Pre-write cleanup failed; the exact owner, reservation, lease, and App gate remain retained. " +
                            cleanupError.Message);
                    }
                    else if (gateAcquired)
                    {
                        ReleaseF10V7C1PaymentGate(owner, gateOwnerToken);
                        gateAcquired = false;
                    }
                }
            }
            return outcome ?? F10V7C1CombatPaymentOutcome.Rejected(
                "The C1→E1 V7 payment ended without a transaction outcome.");
        }

        private bool TryCaptureCurrentF10V7C1PaymentInputs(
            ExpeditionGameplayComposition owner, RoomEncounterRuntime runtime,
            RoomEncounterClearObservation clearObservation, object gateOwnerToken,
            out F10V7SessionInitializationBinding binding,
            out ExpeditionSessionReadinessRegistration registration,
            out ExpeditionReadinessCompositionView readiness,
            out RoomEncounterRuntimeContext context,
            out ExpeditionRoomTransitionController controller,
            out F10C1RuntimeCoordinator coordinator,
            out F10V7CombatPlanRuntimeBinding planBinding,
            out long revision, out string error)
        {
            binding = owner == null ? null : owner.CaptureV7InitializationBinding();
            readiness = owner == null ? null : owner.CaptureReadinessCompositionView();
            registration = readiness?.ReadinessRegistration;
            context = null;
            controller = null;
            coordinator = null;
            planBinding = null;
            revision = ActiveExpeditionRevision;
            error = string.Empty;

            if (owner == null || runtime == null || clearObservation == null || binding == null ||
                readiness == null || registration == null)
            {
                error = "The exact active V7 C1 owner, runtime and physical clear are required.";
                return false;
            }

            controller = owner.RoomTransitions;
            RoomLifetimeScope scope = controller == null ? null : controller.ActiveScope;
            coordinator = scope == null ? null : scope.GetComponent<F10C1RuntimeCoordinator>();
            context = coordinator == null ? null : coordinator.Context;
            if (coordinator == null || context == null || !ReferenceEquals(context.Scope, scope) ||
                !ReferenceEquals(coordinator.Runtime, runtime) ||
                !ReferenceEquals(coordinator.PhysicalClearObservation, clearObservation) ||
                !coordinator.HasExactV7PhysicalClearObservation() ||
                !ReferenceEquals(runtime.LastClearObservation, clearObservation) ||
                !context.IsCurrentRoomScope())
            {
                error = "The current scope coordinator does not own this exact physical C1 clear observation.";
                return false;
            }

            if (!TryValidateF10V7CombatOwner(binding, owner, context, gateOwnerToken,
                    out readiness, out controller, out revision, out error)) return false;
            if (!TryCreateF10V7CombatPlanRuntimeBinding(owner, gateOwnerToken,
                    out planBinding, out error) || planBinding == null ||
                !IsCurrentF10V7CombatPlanRuntimeBinding(planBinding, binding, owner,
                    context, runtime, revision, gateOwnerToken) ||
                !ReferenceEquals(planBinding.Scope, scope) ||
                !ReferenceEquals(planBinding.Registration, registration) ||
                !ReferenceEquals(planBinding.Activation, registration.CurrentActivation) ||
                !ReferenceEquals(coordinator.PhysicalClearObservation, clearObservation) ||
                !coordinator.HasExactV7PhysicalClearObservation())
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "The App C1 plan/runtime binding did not retain this exact clear, scope, and revision.";
                return false;
            }
            return true;
        }

        private static bool SameF10V7C1PaymentInputs(
            F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessCompositionView readiness, RoomEncounterRuntimeContext context,
            ExpeditionRoomTransitionController controller, F10C1RuntimeCoordinator coordinator,
            F10V7CombatPlanRuntimeBinding planBinding, long revision,
            F10V7SessionInitializationBinding otherBinding,
            ExpeditionSessionReadinessRegistration otherRegistration,
            ExpeditionReadinessCompositionView otherReadiness, RoomEncounterRuntimeContext otherContext,
            ExpeditionRoomTransitionController otherController, F10C1RuntimeCoordinator otherCoordinator,
            F10V7CombatPlanRuntimeBinding otherPlanBinding, long otherRevision)
        {
            return ReferenceEquals(binding, otherBinding) &&
                ReferenceEquals(registration, otherRegistration) &&
                ReferenceEquals(readiness.ReadinessRegistration, otherReadiness.ReadinessRegistration) &&
                ReferenceEquals(readiness.Owner, otherReadiness.Owner) &&
                ReferenceEquals(readiness.Run, otherReadiness.Run) &&
                ReferenceEquals(readiness.PlayerMotor, otherReadiness.PlayerMotor) &&
                ReferenceEquals(readiness.ItemCollector, otherReadiness.ItemCollector) &&
                ReferenceEquals(context, otherContext) && ReferenceEquals(controller, otherController) &&
                ReferenceEquals(coordinator, otherCoordinator) && ReferenceEquals(planBinding, otherPlanBinding) &&
                revision == otherRevision;
        }

        private bool TryAcquireF10V7C1PaymentPauseLease(
            ExpeditionGameplayComposition owner, F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration, out IDisposable lease, out string error)
        {
            lease = null;
            error = string.Empty;
            if (binding == null || registration == null ||
                !ReferenceEquals(_expeditionSimulationPauseLeaseProviderOwner, owner) ||
                _expeditionSimulationPauseLeaseProvider == null ||
                !IsCurrentV7PublishedSession(binding, registration, ActiveExpeditionRevision))
            {
                error = "The exact V7 C1 App owner does not have its current simulation pause lease provider.";
                return false;
            }
            try { lease = _expeditionSimulationPauseLeaseProvider(); }
            catch (Exception exception)
            {
                error = "The V7 C1 simulation could not be paused: " + exception.Message;
                return false;
            }
            if (lease != null) return true;
            error = "The V7 C1 simulation pause provider returned no lease.";
            return false;
        }

        private bool TryCaptureF10V7C1LiveResources(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition owner,
            ExpeditionReadinessCompositionView readiness, RoomEncounterRuntimeContext context,
            F10V7CombatPlanRuntimeBinding planBinding, long revision, IDisposable pauseLease,
            object gateOwnerToken, out ExpeditionSaveData captured,
            out ExpeditionInventory liveInventory, out F10V7C1PaymentResourceCapture receipt,
            out string error)
        {
            captured = null;
            liveInventory = null;
            receipt = null;
            error = string.Empty;
            if (binding == null || owner == null || readiness == null || context == null ||
                planBinding == null || pauseLease == null || gateOwnerToken == null ||
                _profileMutationGate.CurrentCount != 0 || !ReferenceEquals(_pendingF10V7C1CombatPayment, null) ||
                !ReferenceEquals(readiness.Owner, owner) || !ReferenceEquals(readiness.Application, this) ||
                !ReferenceEquals(readiness.ReadinessRegistration?.V7InitializationBinding, binding) ||
                readiness.PlayerMotor == null || readiness.ItemCollector == null ||
                !ReferenceEquals(readiness.PlayerMotor, owner.PlayerMotor) ||
                !ReferenceEquals(readiness.ItemCollector.Inventory, ActiveRun?.Inventory) ||
                !ReferenceEquals(ActiveRun?.Inventory?.Catalog, binding.AdmissionItemCatalog) ||
                readiness.PlayerMotor.CombatStats == null ||
                !TryGetCurrentV7PublishedSnapshot(binding,
                    out F10V7ExpeditionReadProjection projection, out string confirmedJson,
                    out long publishedRevision) || projection == null ||
                publishedRevision != revision || revision != ActiveExpeditionRevision ||
                !IsCurrentV7PublishedSession(binding, readiness.ReadinessRegistration, revision) ||
                !StatesEqual(ActiveExpedition, projection.CompositionReadOnlyView))
            {
                error = "The live C1 binder, pause lease, current projection, or App mutation gate changed before capture.";
                return false;
            }

            liveInventory = readiness.ItemCollector.Inventory;
            try
            {
                captured = projection.CompositionReadOnlyView.Clone();
                captured.inventorySnapshotJson = JsonUtility.ToJson(liveInventory.CreateSnapshot());
                captured.healthPotionCharges = ExpeditionSaveMapper.GetHealthPotionCharges(liveInventory);
                captured.maximumHealth = readiness.PlayerMotor.CombatStats.MaximumHealth;
                captured.currentHealth = readiness.PlayerMotor.CurrentHealth;
                captured.maximumEnergy = readiness.PlayerMotor.MaximumEnergy;
                captured.currentEnergy = readiness.PlayerMotor.CurrentEnergy;
                receipt = new F10V7C1PaymentResourceCapture(binding, owner,
                    readiness.ReadinessRegistration, readiness, context, planBinding,
                    captured, liveInventory, pauseLease, gateOwnerToken,
                    confirmedJson, projection, revision);
                return true;
            }
            catch (Exception exception)
            {
                error = "The live C1 binder state could not be captured: " + exception.Message;
                captured = null;
                liveInventory = null;
                receipt = null;
                return false;
            }
        }

        private async Task<F10V7C1CombatPaymentOutcome> ExecuteF10V7C1CombatPaymentPendingAsync(
            F10V7C1CombatPaymentPending pending)
        {
            if (!pending.TryAcquireOperation(out object operationToken))
                return F10V7C1CombatPaymentOutcome.PendingResult(
                    "The exact C1 payment is already committing or reconciling; no duplicate operation was started.");
            try
            {
                if (!ReferenceEquals(_pendingF10V7C1CombatPayment, pending) ||
                    !pending.MutationGateRetained || _profileMutationGate.CurrentCount != 0)
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The exact C1 payment no longer owns the retained App mutation gate.");

                if (pending.UnwrittenCleanupFailed)
                    return TryResolveF10V7C1UnwrittenCleanup(pending);

                if (!IsExactF10V7C1PendingOwner(pending, operationToken, out string ownerError))
                    return F10V7C1CombatPaymentOutcome.PendingResult(ownerError);

                F10V7SessionCommitResult commit = pending.NextRepositoryAction switch
                {
                    F10V7C1RepositoryAction.Retry => await _saveRepository
                        .RetryF10V7SessionForPreproductionAsync(pending.Candidate.Handle),
                    _ => await _saveRepository.CommitF10V7SessionForPreproductionAsync(
                        pending.Candidate.Handle, pending.Candidate.PinnedCandidate)
                };
                // Retain the repository's exact result before inspecting owner identity or revision.
                pending.CaptureRepositoryCommitResult(commit);

                if (!IsExactF10V7C1PendingOwner(pending, operationToken, out ownerError))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The repository result was retained, but App owner/gate reconciliation failed: " + ownerError);

                if (commit == null)
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The repository returned no V7 commit result; the exact C1 candidate remains retained.");
                if (!commit.Persisted)
                {
                    if (commit.Pending || pending.RepositoryPending || pending.RepositoryConfirmed)
                        return F10V7C1CombatPaymentOutcome.PendingResult(commit.Message);
                    F10V7C1CombatPaymentOutcome cleanup = DiscardUnwrittenF10V7C1Payment(pending,
                        "The repository rejected the C1 payment before confirming a write: " + commit.Message);
                    return cleanup;
                }
                if (commit.Pending || pending.RepositoryPending || !pending.RepositoryConfirmed ||
                    !string.Equals(commit.ConfirmedJson, pending.ConfirmedJson, StringComparison.Ordinal) ||
                    string.IsNullOrWhiteSpace(pending.ConfirmedJson))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The repository write or exact primary readback remains pending; retry retains the same payment candidate.");

                if (!TryValidateConfirmedF10V7C1Payment(pending, out string readbackError))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The confirmed bytes were retained but failed exact C1→E1 validation: " + readbackError);

                if (!TryPromoteConfirmedF10V7C1Payment(pending, out string promotionError))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The exact confirmed C1 payment remains retained while App promotion is reconciled: " + promotionError);

                if (!pending.ActorPublished)
                {
                    if (!pending.Owner.PublishF10V7C1PaymentRun(pending.Binding,
                            pending.ResourceCapture.SourceRun, pending.BinderPublication,
                            pending.ConfirmedJson, pending.ConfirmedProjection,
                            pending.ExpectedRevision, pending.ConfirmedRevision,
                            out string publicationError))
                        return F10V7C1CombatPaymentOutcome.PendingResult(
                            "The durable E1 candidate remains retained while the same actor/inventory publication retries: " + publicationError);
                    pending.ActorPublished = pending.BinderPublication.ActorPublishReturned;
                }
                if (!pending.BinderPublication.PublishConfirmedF10V7C1PaymentWitness(
                        pending, operationToken, out string witnessError))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The exact E1 run/readback is durable, but readiness witness publication remains pending: " + witnessError);
                if (!pending.ActorPublished ||
                    !pending.Registration.IsAdmittedV7SnapshotCurrent(pending.Binding,
                        pending.ConfirmedJson, pending.ConfirmedProjection, pending.ConfirmedRevision))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The V7 E1 projection or actor publication is not yet reconciled to the exact readback.");

                if (!pending.TransitionActivated)
                {
                    if (IsExactF10V7C1PaymentE1Activation(pending))
                    {
                        pending.TransitionActivated = true;
                    }
                    else if (pending.Controller != null &&
                             ReferenceEquals(pending.Owner.RoomTransitions, pending.Controller) &&
                             ReferenceEquals(pending.Candidate.PreparedTransition, pending.PreparedTransition) &&
                             pending.Candidate.PreparedTransition != null &&
                             pending.Candidate.PreparedTransition.IsPending)
                    {
                        bool committed;
                        try { committed = pending.Controller.TryCommitPreparedTransition(
                            pending.Candidate.PreparedTransition); }
                        catch (Exception exception)
                        {
                            committed = IsExactF10V7C1PaymentE1Activation(pending);
                            if (!committed)
                                return F10V7C1CombatPaymentOutcome.PendingResult(
                                    "The exact E1 reservation remains retained after activation threw: " + exception.Message);
                        }
                        if (!committed && !IsExactF10V7C1PaymentE1Activation(pending))
                            return F10V7C1CombatPaymentOutcome.PendingResult(
                                "The durable C1 payment is retained, but its exact prepared E1 transition is not active: " +
                                pending.Controller.LastTransitionError);
                        pending.TransitionActivated = IsExactF10V7C1PaymentE1Activation(pending);
                    }
                    else
                    {
                        return F10V7C1CombatPaymentOutcome.PendingResult(
                            "The durable E1 candidate is retained; its exact prepared transition was consumed or ended without activation.");
                    }
                }

                if (!pending.TransitionActivated || !IsExactF10V7C1PaymentE1Activation(pending))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The E1 room is not bound to the exact confirmed V7 run and actor publication.");

                if (!TryReleaseCompletedF10V7C1Payment(pending, out string releaseError))
                    return F10V7C1CombatPaymentOutcome.PendingResult(
                        "The paid E1 room is active, but its retained gate/lease cleanup is still pending: " + releaseError);

                return F10V7C1CombatPaymentOutcome.Completed(
                    "The exact physical C1 clear is paid once, read back, published to E1, and activated.");
            }
            catch (Exception exception)
            {
                if (pending != null) pending.RepositoryPending = true;
                return F10V7C1CombatPaymentOutcome.PendingResult(
                    "The same C1 payment candidate remains retained for exact retry: " + exception.Message);
            }
            finally { pending.ReleaseOperation(operationToken); }
        }

        private bool IsExactF10V7C1PendingOwner(F10V7C1CombatPaymentPending pending,
            object operationToken, out string error)
        {
            error = string.Empty;
            ExpeditionGameplayComposition owner = pending?.Owner;
            ExpeditionSessionReadinessRegistration registration = pending?.Registration;
            F10V7SessionInitializationBinding binding = pending?.Binding;
            if (pending == null || !ReferenceEquals(_pendingF10V7C1CombatPayment, pending) ||
                !pending.MutationGateRetained || _profileMutationGate.CurrentCount != 0 ||
                pending.SimulationPauseLease == null || pending.GateOwnerToken == null ||
                !IsExactF10V7C1PaymentGateOwner(owner, pending.GateOwnerToken) ||
                operationToken == null || !ReferenceEquals(pending.ActiveOperationToken, operationToken) ||
                !ReferenceEquals(_expeditionSimulationPauseLeaseProviderOwner, owner) ||
                owner == null || !owner.isActiveAndEnabled || registration == null || registration.IsDisposed ||
                !ReferenceEquals(registration.Owner, owner) || !ReferenceEquals(registration.Application, this) ||
                !ReferenceEquals(registration.V7InitializationBinding, binding) ||
                binding == null || !binding.IsCurrentFor(this, owner) ||
                !ReferenceEquals(pending.Candidate?.Binding, binding) ||
                !ReferenceEquals(pending.Candidate.Owner, owner) ||
                !ReferenceEquals(pending.Candidate.ClearObservation, pending.ClearObservation) ||
                !ReferenceEquals(pending.Candidate.Context, pending.Context) ||
                !ReferenceEquals(pending.Candidate.Runtime, pending.Runtime) ||
                !ReferenceEquals(pending.Candidate.Controller, pending.Controller) ||
                pending.ResourceCapture == null ||
                !ReferenceEquals(pending.ResourceCapture.Binding, binding) ||
                !ReferenceEquals(pending.ResourceCapture.Owner, owner) ||
                !ReferenceEquals(pending.ResourceCapture.Registration, registration) ||
                !ReferenceEquals(pending.ResourceCapture.Context, pending.Context) ||
                !ReferenceEquals(pending.ResourceCapture.PlanBinding, pending.PlanBinding) ||
                !ReferenceEquals(pending.ResourceCapture.SourceProjection, pending.SourceProjection) ||
                !string.Equals(pending.ResourceCapture.SourceJson, pending.SourceJson, StringComparison.Ordinal) ||
                pending.ResourceCapture.Revision != pending.ExpectedRevision ||
                !ReferenceEquals(pending.ResourceCapture.PauseLease, pending.SimulationPauseLease) ||
                !ReferenceEquals(pending.ResourceCapture.GateOwnerToken, pending.GateOwnerToken) ||
                !ReferenceEquals(pending.Candidate.PreparedTransition, pending.PreparedTransition) ||
                !ReferenceEquals(pending.BinderPublication?.Registration, registration) ||
                !ReferenceEquals(pending.BinderPublication?.Run, pending.Candidate.CandidateRun) ||
                !ReferenceEquals(pending.BinderPublication?.CandidateSave, pending.Candidate.CandidateCommon) ||
                _pendingF10V7TutorialAdvance != null || HasPendingExpeditionWrite ||
                HasPendingSettlement ||
                HasPendingContractProgression || HasPendingCraftingWrite || HasPendingEconomyMutation ||
                HasPendingPermanentUpgradePurchase || HasPendingRoomAdvance ||
                _pendingRoomAdvance != null || IsRoomAdvanceInFlight ||
                _pendingExpeditionCandidate != null || _pendingExpeditionSessionPublication != null ||
                _expeditionSessionMutationPublicationOwner != null ||
                _expeditionSessionStateCapture != null || _expeditionSessionMutationPreparer != null)
            {
                error = "The exact V7 C1 pending, lease, App gate, owner, or candidate changed; no replacement operation may start.";
                return false;
            }

            if (pending.RepositoryConfirmed)
            {
                if (_f10V7PublishedSession == null ||
                    !ReferenceEquals(_f10V7PublishedSession.Binding, binding) ||
                    !(_f10V7PublishedSession.Revision == pending.ExpectedRevision ||
                      _f10V7PublishedSession.Revision == pending.ExpectedRevision + 1) ||
                    (_f10V7PublishedSession.Revision == pending.ExpectedRevision + 1 &&
                     (!string.Equals(_f10V7PublishedSession.ConfirmedJson, pending.ConfirmedJson,
                          StringComparison.Ordinal) ||
                      !ReferenceEquals(_f10V7PublishedSession.Projection, pending.ConfirmedProjection))))
                {
                    error = "The App published session is neither the retained C1 base nor this exact readback promotion.";
                    return false;
                }
            }
            else if (!IsCurrentV7PublishedSession(binding, registration, pending.ExpectedRevision) ||
                     !TryGetCurrentV7PublishedSnapshot(binding,
                         out F10V7ExpeditionReadProjection sourceProjection,
                         out string sourceJson, out long sourceRevision) ||
                     !ReferenceEquals(sourceProjection, pending.SourceProjection) ||
                     !string.Equals(sourceJson, pending.SourceJson, StringComparison.Ordinal) ||
                     sourceRevision != pending.ExpectedRevision)
            {
                error = "The current C1 ConfirmedJson/projection/revision changed before the retained write was confirmed.";
                return false;
            }
            return true;
        }

        internal bool IsExactF10V7C1PaymentReadinessPublication(
            F10V7C1CombatPaymentPending pending, object operationToken,
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessPreparedPublication publication)
        {
            if (!IsExactF10V7C1PendingOwner(pending, operationToken, out _) ||
                pending.Candidate == null || publication == null ||
                !ReferenceEquals(pending.Registration, registration) ||
                !ReferenceEquals(pending.BinderPublication, publication) ||
                !pending.AppPromoted || !pending.ActorPublished ||
                pending.ConfirmedRevision != pending.ExpectedRevision + 1 ||
                pending.ConfirmedProjection == null ||
                pending.ConfirmedCommitResult == null ||
                !pending.ConfirmedCommitResult.Persisted || pending.ConfirmedCommitResult.Pending ||
                !string.Equals(pending.ConfirmedCommitResult.ConfirmedJson,
                    pending.ConfirmedJson, StringComparison.Ordinal) ||
                !publication.ActorPublishReturned ||
                !ReferenceEquals(publication.Run, pending.Candidate?.CandidateRun) ||
                !ReferenceEquals(publication.CandidateSave, pending.Candidate?.CandidateCommon) ||
                !StatesEqual(publication.CandidateSave,
                    pending.ConfirmedProjection.CompositionReadOnlyView) ||
                !TryGetCurrentV7PublishedSnapshot(pending.Binding,
                    out F10V7ExpeditionReadProjection currentProjection, out string currentJson,
                    out long currentRevision) ||
                !ReferenceEquals(currentProjection, pending.ConfirmedProjection) ||
                !string.Equals(currentJson, pending.ConfirmedJson, StringComparison.Ordinal) ||
                currentRevision != pending.ConfirmedRevision ||
                !ReferenceEquals(ActiveRun, pending.Candidate.CandidateRun) ||
                ActiveExpeditionRevision != pending.ConfirmedRevision ||
                !StatesEqual(ActiveExpedition, pending.ConfirmedProjection.CompositionReadOnlyView) ||
                !string.Equals(pending.ConfirmedProjection.SourceStateV7?.currentNodeId,
                    F10V7E1NodeId, StringComparison.Ordinal))
                return false;
            return true;
        }

        private bool TryValidateConfirmedF10V7C1Payment(
            F10V7C1CombatPaymentPending pending, out string error)
        {
            error = string.Empty;
            if (pending == null || !pending.RepositoryConfirmed ||
                pending.ConfirmedCommitResult == null || !pending.ConfirmedCommitResult.Persisted ||
                pending.ConfirmedCommitResult.Pending ||
                !string.Equals(pending.ConfirmedCommitResult.ConfirmedJson,
                    pending.ConfirmedJson, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(pending.SourceJson) ||
                string.IsNullOrWhiteSpace(pending.ConfirmedJson) ||
                pending.Candidate == null || pending.Candidate.PinnedCandidate == null ||
                pending.Candidate.CandidateCommon == null || pending.Binding?.AdmissionRegistry == null)
            {
                error = "No exact current C1 source, candidate, or repository readback is retained.";
                return false;
            }

            try
            {
                if (!SaveV7JsonCodec.TryDecodeProfile(StrictF10V7C1PaymentUtf8.GetBytes(pending.SourceJson),
                        out ProfileSaveEnvelopeV7 source, out SaveV7Error validationError) ||
                    !SaveV7JsonCodec.TryDecodeProfile(StrictF10V7C1PaymentUtf8.GetBytes(pending.ConfirmedJson),
                        out ProfileSaveEnvelopeV7 confirmed, out validationError))
                {
                    error = validationError.Code + ": " + validationError.Message;
                    return false;
                }
                if (source == null || confirmed == null || source.activeExpedition == null ||
                    confirmed.activeExpedition == null || confirmed.contentVersion != 7 ||
                    confirmed.generation != checked(source.generation + 1) ||
                    confirmed.sequence != checked(source.sequence + 1) ||
                    !SaveV7CandidateMapper.TryValidateCommit(confirmed,
                        pending.Candidate.CandidateRun, pending.Binding.AdmissionRegistry,
                        out validationError) ||
                    !SaveV7CandidateMapper.TryValidateTransition(source, null,
                        confirmed, out validationError) ||
                    !SaveV7CandidateMapper.HasSameProfilePayload(source.profile, confirmed.profile))
                {
                    error = validationError.Code == SaveV7ErrorCode.None
                        ? "The readback is not the single validated, profile-preserving C1→E1 transition."
                        : validationError.Code + ": " + validationError.Message;
                    return false;
                }

                ProfileSaveEnvelopeV7 candidateEnvelope = pending.Candidate.PinnedCandidate.CreateEnvelope(
                    confirmed.generation, confirmed.sequence, confirmed.savedAtUtc);
                if (!SaveV7CandidateMapper.HasSameProfilePayload(candidateEnvelope.profile, confirmed.profile) ||
                    !string.Equals(JsonUtility.ToJson(candidateEnvelope.activeExpedition),
                        JsonUtility.ToJson(confirmed.activeExpedition), StringComparison.Ordinal) ||
                    !string.Equals(confirmed.activeExpedition.currentNodeId,
                        "sample.patio.exploration", StringComparison.Ordinal) ||
                    !string.Equals(confirmed.activeExpedition.runId,
                        pending.Candidate.CandidateRun.RunId.ToString(), StringComparison.Ordinal))
                {
                    error = "The exact readback differs from the retained paid candidate or fixed E1 target.";
                    return false;
                }

                if (!F10V7ExpeditionReadProjector.TryCreateCurrent(confirmed,
                        pending.Binding.AdmissionItemCatalog, pending.Binding.AdmissionRegistry,
                        out F10V7ExpeditionReadProjection projection, out validationError) ||
                    projection == null || projection.CheckpointAuthorityV7 == null ||
                    !string.Equals(projection.SourceStateV7.currentNodeId,
                        "sample.patio.exploration", StringComparison.Ordinal) ||
                    !string.Equals(projection.CheckpointAuthorityV7.roomNodeId,
                        "sample.patio.exploration", StringComparison.Ordinal) ||
                    !string.Equals(projection.SourceStateV7.encounterPlanPin?.entry?.nodeId,
                        "sample.patio.exploration", StringComparison.Ordinal) ||
                    !StatesEqual(projection.CompositionReadOnlyView,
                        pending.Candidate.CandidateCommon))
                {
                    error = validationError.Code == SaveV7ErrorCode.None
                        ? "The authenticated E1 projection or checkpoint differs from the exact binder candidate."
                        : validationError.Code + ": " + validationError.Message;
                    return false;
                }

                if (pending.ConfirmedProjection == null)
                {
                    pending.ConfirmedProjection = projection;
                    pending.ConfirmedRevision = checked(pending.ExpectedRevision + 1);
                }
                else if (!V7ProjectionsEqual(pending.ConfirmedProjection, projection) ||
                         pending.ConfirmedRevision != checked(pending.ExpectedRevision + 1))
                {
                    error = "The same confirmed JSON restored to a different V7 E1 projection or revision.";
                    return false;
                }
                return true;
            }
            catch (Exception exception) when (exception is EncoderFallbackException ||
                exception is ArgumentException || exception is FormatException ||
                exception is OverflowException || exception is InvalidOperationException ||
                exception is SaveV7ValidationException)
            {
                error = "Exact C1→E1 readback validation failed: " + exception.Message;
                return false;
            }
        }

        private bool TryPromoteConfirmedF10V7C1Payment(
            F10V7C1CombatPaymentPending pending, out string error)
        {
            error = string.Empty;
            if (pending?.ConfirmedProjection == null || pending.ConfirmedRevision != pending.ExpectedRevision + 1 ||
                !pending.RepositoryConfirmed)
            {
                error = "The V7 C1 payment has no exact confirmed readback projection to promote.";
                return false;
            }

            F10V7PublishedSession published = _f10V7PublishedSession;
            if (published == null || !ReferenceEquals(published.Binding, pending.Binding))
            {
                error = "The App no longer retains this V7 session owner.";
                return false;
            }

            bool exactBase = published.Revision == pending.ExpectedRevision &&
                string.Equals(published.ConfirmedJson, pending.SourceJson, StringComparison.Ordinal) &&
                ReferenceEquals(published.Projection, pending.SourceProjection) &&
                _activeExpeditionRevision == pending.ExpectedRevision &&
                StatesEqual(ActiveExpedition, pending.SourceProjection.CompositionReadOnlyView) &&
                ReferenceEquals(ActiveRun, pending.ResourceCapture.SourceRun);
            bool exactPartial = published.Revision == pending.ConfirmedRevision &&
                string.Equals(published.ConfirmedJson, pending.ConfirmedJson, StringComparison.Ordinal) &&
                ReferenceEquals(published.Projection, pending.ConfirmedProjection) &&
                (_activeExpeditionRevision == pending.ExpectedRevision ||
                 _activeExpeditionRevision == pending.ConfirmedRevision) &&
                (StatesEqual(ActiveExpedition, pending.SourceProjection.CompositionReadOnlyView) ||
                 StatesEqual(ActiveExpedition, pending.ConfirmedProjection.CompositionReadOnlyView)) &&
                (ReferenceEquals(ActiveRun, pending.ResourceCapture.SourceRun) ||
                 ReferenceEquals(ActiveRun, pending.Candidate.CandidateRun));

            if (!exactBase && !exactPartial)
            {
                error = "The current App state is neither the exact C1 base nor a partial promotion of this readback.";
                return false;
            }

            if (exactBase && !pending.Registration.CanPromoteF10V7C1Payment(
                    pending.Binding, pending.ResourceCapture.SourceRun,
                    pending.Candidate.CandidateRun, pending.SourceJson,
                    pending.SourceProjection, pending.ConfirmedJson,
                    pending.ConfirmedProjection, pending.ExpectedRevision,
                    pending.ConfirmedRevision))
            {
                error = "The exact C1 source and session owner changed before E1 promotion.";
                return false;
            }

            if (exactBase && !published.TryPromote(pending.ConfirmedJson,
                    pending.ConfirmedProjection, pending.Candidate.CandidateRun,
                    pending.ConfirmedRevision))
            {
                error = "The App could not retain the exact confirmed E1 JSON, projection, run, and revision.";
                return false;
            }

            // Reconcile the owner as an idempotent tuple. The binding.Run remains the admitted
            // ancestry; the published run is the current restored-and-paid C1→E1 run.
            ActiveRun = pending.Candidate.CandidateRun;
            _activeExpedition = pending.ConfirmedProjection.CompositionReadOnlyView.Clone();
            _activeExpeditionRevision = pending.ConfirmedRevision;
            try
            {
                pending.Registration.RecordPromotedAdmittedV7Snapshot(
                    pending.ConfirmedJson, pending.ConfirmedProjection, pending.ConfirmedRevision);
            }
            catch (Exception exception)
            {
                error = "The App V7 projection is promoted but readiness reconciliation remains pending: " + exception.Message;
                return false;
            }
            pending.AppPromoted = true;
            return true;
        }

        private bool IsExactF10V7C1PaymentE1Activation(F10V7C1CombatPaymentPending pending)
        {
            if (pending == null || pending.ConfirmedProjection == null || !pending.AppPromoted ||
                !pending.ActorPublished || pending.ConfirmedRevision != pending.ExpectedRevision + 1 ||
                _activeExpeditionRevision != pending.ConfirmedRevision || ActiveExpedition == null ||
                !StatesEqual(ActiveExpedition, pending.ConfirmedProjection.CompositionReadOnlyView) ||
                !ReferenceEquals(ActiveRun, pending.Candidate.CandidateRun) ||
                !ReferenceEquals(pending.Owner.RoomTransitions, pending.Controller) ||
                !TryGetCurrentV7PublishedSnapshot(pending.Binding,
                    out F10V7ExpeditionReadProjection projection, out string confirmedJson,
                    out long revision) || !ReferenceEquals(projection, pending.ConfirmedProjection) ||
                !string.Equals(confirmedJson, pending.ConfirmedJson, StringComparison.Ordinal) ||
                revision != pending.ConfirmedRevision ||
                !pending.Registration.IsAdmittedV7SnapshotCurrent(pending.Binding,
                    pending.ConfirmedJson, pending.ConfirmedProjection, pending.ConfirmedRevision)) return false;

            ExpeditionRoomTransitionController transitions = pending.Owner.RoomTransitions;
            ExpeditionRoomActivationRegistration activation = pending.Registration.CurrentActivation;
            RoomInstanceId expectedRoomId = ExpeditionRoomIdentity.ForNode(
                pending.Candidate.CandidateRun.RunId, "sample.patio.exploration");
            return transitions != null && !transitions.IsDisposed &&
                transitions.CurrentRoomId == expectedRoomId &&
                string.Equals(transitions.CurrentNodeId, "sample.patio.exploration", StringComparison.Ordinal) &&
                ActiveRoomInstanceId == expectedRoomId &&
                activation != null && activation.State == ExpeditionRoomActivationState.Bound &&
                ReferenceEquals(activation.Controller, transitions) &&
                ReferenceEquals(activation.Scope, transitions.ActiveScope) &&
                string.Equals(activation.NodeId, "sample.patio.exploration", StringComparison.Ordinal) &&
                string.Equals(activation.TemplateId, F10V7E1TemplateId, StringComparison.Ordinal) &&
                activation.RunId == pending.Candidate.CandidateRun.RunId &&
                ReferenceEquals(activation.BoundRun, pending.Candidate.CandidateRun);
        }

        private bool TryReleaseCompletedF10V7C1Payment(
            F10V7C1CombatPaymentPending pending, out string error)
        {
            error = string.Empty;
            if (pending == null || !ReferenceEquals(_pendingF10V7C1CombatPayment, pending) ||
                !pending.MutationGateRetained || _profileMutationGate.CurrentCount != 0 ||
                !IsExactF10V7C1PaymentGateOwner(pending.Owner, pending.GateOwnerToken))
            {
                error = "The completed payment no longer owns its exact App gate.";
                return false;
            }
            try
            {
                pending.SimulationPauseLease?.Dispose();
                pending.SimulationPauseLease = null;
            }
            catch (Exception exception)
            {
                error = "The exact C1 pause lease could not be released: " + exception.Message;
                return false;
            }
            try { ReleaseF10V7C1PaymentGate(pending.Owner, pending.GateOwnerToken); }
            catch (Exception exception)
            {
                error = "The exact C1 App mutation gate could not be released: " + exception.Message;
                return false;
            }
            pending.MutationGateRetained = false;
            _pendingF10V7C1CombatPayment = null;
            return true;
        }

        private F10V7C1CombatPaymentOutcome DiscardUnwrittenF10V7C1Payment(
            F10V7C1CombatPaymentPending pending, string message)
        {
            if (pending.RepositoryConfirmed || pending.RepositoryPending)
                return F10V7C1CombatPaymentOutcome.PendingResult(
                    "A confirmed or uncertain write cannot discard its C1 candidate.");
            try
            {
                if (pending.PreparedTransition != null && pending.PreparedTransition.IsPending)
                    pending.PreparedTransition.Dispose();
            }
            catch (Exception exception)
            {
                pending.UnwrittenCleanupFailed = true;
                return F10V7C1CombatPaymentOutcome.PendingResult(
                    "The unwritten C1 reservation cleanup failed and remains retained: " + exception.Message);
            }
            if (!TryReleaseCompletedF10V7C1Payment(pending, out string releaseError))
            {
                pending.UnwrittenCleanupFailed = true;
                return F10V7C1CombatPaymentOutcome.PendingResult(
                    "The unwritten C1 transaction could not release its exact gate/lease: " + releaseError);
            }
            return F10V7C1CombatPaymentOutcome.Rejected(message);
        }

        private F10V7C1CombatPaymentOutcome TryResolveF10V7C1UnwrittenCleanup(
            F10V7C1CombatPaymentPending pending)
        {
            try
            {
                if (pending.PreparedTransition != null && pending.PreparedTransition.IsPending)
                    pending.PreparedTransition.Dispose();
            }
            catch (Exception exception)
            {
                return F10V7C1CombatPaymentOutcome.PendingResult(
                    "The exact unwritten transition reservation remains locked: " + exception.Message);
            }
            if (!TryReleaseCompletedF10V7C1Payment(pending, out string releaseError))
                return F10V7C1CombatPaymentOutcome.PendingResult(releaseError);
            return F10V7C1CombatPaymentOutcome.Rejected(
                "The unwritten C1 candidate was cleaned up; submit a new physical clear after recovery.");
        }

        private bool IsExactF10V7C1PaymentBindingBaseOrPartial(F10V7C1CombatPaymentPending pending)
        {
            if (pending == null || _f10V7PublishedSession == null ||
                !ReferenceEquals(_f10V7PublishedSession.Binding, pending.Binding)) return false;
            bool baseState = _f10V7PublishedSession.Revision == pending.ExpectedRevision &&
                string.Equals(_f10V7PublishedSession.ConfirmedJson, pending.SourceJson, StringComparison.Ordinal) &&
                ReferenceEquals(_f10V7PublishedSession.Projection, pending.SourceProjection) &&
                _activeExpeditionRevision == pending.ExpectedRevision &&
                StatesEqual(ActiveExpedition, pending.SourceProjection.CompositionReadOnlyView) &&
                ReferenceEquals(ActiveRun, pending.ResourceCapture.SourceRun);
            bool targetState = _f10V7PublishedSession.Revision == pending.ConfirmedRevision &&
                string.Equals(_f10V7PublishedSession.ConfirmedJson, pending.ConfirmedJson, StringComparison.Ordinal) &&
                ReferenceEquals(_f10V7PublishedSession.Projection, pending.ConfirmedProjection) &&
                _activeExpeditionRevision == pending.ConfirmedRevision &&
                StatesEqual(ActiveExpedition, pending.ConfirmedProjection.CompositionReadOnlyView) &&
                ReferenceEquals(ActiveRun, pending.Candidate.CandidateRun);
            return baseState || targetState;
        }
    }

    internal enum F10V7C1RepositoryAction
    {
        Commit = 0,
        Retry = 1,
    }

    internal sealed class F10V7C1PaymentResourceCapture
    {
        internal F10V7C1PaymentResourceCapture(
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding,
            ExpeditionGameplayComposition owner, ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessCompositionView readiness, RoomEncounterRuntimeContext context,
            ApplicationBootstrapper.F10V7CombatPlanRuntimeBinding planBinding,
            ExpeditionSaveData captured, ExpeditionInventory inventory, IDisposable pauseLease,
            object gateOwnerToken, string sourceJson,
            F10V7ExpeditionReadProjection sourceProjection, long revision)
        {
            Binding = binding;
            Owner = owner;
            Registration = registration;
            Readiness = readiness;
            Context = context;
            PlanBinding = planBinding;
            Captured = captured;
            Inventory = inventory;
            PauseLease = pauseLease;
            GateOwnerToken = gateOwnerToken;
            SourceJson = sourceJson;
            SourceProjection = sourceProjection;
            Revision = revision;
            SourceRun = readiness.Run;
        }

        internal ApplicationBootstrapper.F10V7SessionInitializationBinding Binding { get; }
        internal ExpeditionGameplayComposition Owner { get; }
        internal ExpeditionSessionReadinessRegistration Registration { get; }
        internal ExpeditionReadinessCompositionView Readiness { get; }
        internal RoomEncounterRuntimeContext Context { get; }
        internal ApplicationBootstrapper.F10V7CombatPlanRuntimeBinding PlanBinding { get; }
        internal ExpeditionSaveData Captured { get; }
        internal ExpeditionInventory Inventory { get; }
        internal ExpeditionRunConfiguration SourceRun { get; }
        internal IDisposable PauseLease { get; }
        internal object GateOwnerToken { get; }
        internal string SourceJson { get; }
        internal F10V7ExpeditionReadProjection SourceProjection { get; }
        internal long Revision { get; }

        internal bool IsExactFor(ApplicationBootstrapper app,
            ApplicationBootstrapper.F10V7SessionInitializationBinding binding,
            ExpeditionGameplayComposition owner, ExpeditionReadinessCompositionView readiness,
            RoomEncounterRuntimeContext context, ExpeditionSaveData captured,
            ExpeditionInventory inventory, long revision)
        {
            return app != null && ReferenceEquals(Binding, binding) &&
                ReferenceEquals(Owner, owner) && ReferenceEquals(Registration, readiness?.ReadinessRegistration) &&
                HasSameReadinessIdentity(Readiness, readiness) &&
                ReferenceEquals(readiness.Application, app) && ReferenceEquals(readiness.Owner, owner) &&
                ReferenceEquals(Context, context) &&
                ReferenceEquals(Captured, captured) && ReferenceEquals(Inventory, inventory) &&
                ReferenceEquals(SourceRun, readiness?.Run) && ReferenceEquals(inventory, readiness?.ItemCollector?.Inventory) &&
                PauseLease != null && GateOwnerToken != null && revision == Revision &&
                app.ActiveExpeditionRevision == revision &&
                app.IsCurrentV7PublishedSession(binding, Registration, revision) &&
                app.TryGetCurrentV7PublishedSnapshot(binding,
                    out F10V7ExpeditionReadProjection projection, out string json, out long currentRevision) &&
                ReferenceEquals(projection, SourceProjection) &&
                string.Equals(json, SourceJson, StringComparison.Ordinal) && currentRevision == revision;
        }

        internal static bool HasSameReadinessIdentity(ExpeditionReadinessCompositionView left,
            ExpeditionReadinessCompositionView right)
        {
            return left != null && right != null &&
                ReferenceEquals(left.Owner, right.Owner) && ReferenceEquals(left.Application, right.Application) &&
                ReferenceEquals(left.ReadinessRegistration, right.ReadinessRegistration) &&
                ReferenceEquals(left.Run, right.Run) && ReferenceEquals(left.PlayerObject, right.PlayerObject) &&
                ReferenceEquals(left.PlayerMotor, right.PlayerMotor) && ReferenceEquals(left.ItemCollector, right.ItemCollector) &&
                ReferenceEquals(left.RoomTransitions, right.RoomTransitions) && ReferenceEquals(left.InputSource, right.InputSource) &&
                ReferenceEquals(left.PauseController, right.PauseController) && ReferenceEquals(left.CameraRig, right.CameraRig) &&
                ReferenceEquals(left.ProjectilePrefab, right.ProjectilePrefab) && ReferenceEquals(left.SkillCatalog, right.SkillCatalog) &&
                ReferenceEquals(left.ItemCatalog, right.ItemCatalog) && ReferenceEquals(left.WeaponCatalog, right.WeaponCatalog) &&
                ReferenceEquals(left.ArmorCatalog, right.ArmorCatalog) && left.IsReady == right.IsReady;
        }
    }

    internal sealed class F10V7C1CombatPaymentPending
    {
        private object _activeOperationToken;
        private int _operationInFlight;
        private F10V7SessionCommitResult _commitResult;
        private F10V7SessionCommitResult _confirmedCommitResult;

        internal ExpeditionGameplayComposition Owner;
        internal ApplicationBootstrapper.F10V7SessionInitializationBinding Binding;
        internal ExpeditionSessionReadinessRegistration Registration;
        internal RoomEncounterRuntimeContext Context;
        internal ExpeditionRoomTransitionController Controller;
        internal F10C1RuntimeCoordinator Coordinator;
        internal RoomEncounterRuntime Runtime;
        internal ApplicationBootstrapper.F10V7CombatPlanRuntimeBinding PlanBinding;
        internal RoomEncounterClearObservation ClearObservation;
        internal ApplicationBootstrapper.F10V7CombatAdvanceCandidate Candidate;
        internal ExpeditionRoomTransitionController.PreparedRoomTransition PreparedTransition;
        internal ExpeditionReadinessPreparedPublication BinderPublication;
        internal F10V7C1PaymentResourceCapture ResourceCapture;
        internal F10V7ExpeditionReadProjection SourceProjection;
        internal string SourceJson;
        internal long ExpectedRevision;
        internal object GateOwnerToken;
        internal IDisposable SimulationPauseLease;
        internal bool MutationGateRetained;
        internal bool RepositoryPending;
        internal bool RepositoryConfirmed;
        internal bool AppPromoted;
        internal bool ActorPublished;
        internal bool TransitionActivated;
        internal bool UnwrittenCleanupFailed;
        internal string ConfirmedJson;
        internal F10V7ExpeditionReadProjection ConfirmedProjection;
        internal long ConfirmedRevision;

        internal F10V7SessionCommitResult CommitResult => _commitResult;
        internal F10V7SessionCommitResult ConfirmedCommitResult => _confirmedCommitResult;
        internal object ActiveOperationToken => _activeOperationToken;
        internal int OperationInFlight => Volatile.Read(ref _operationInFlight);
        internal F10V7C1RepositoryAction NextRepositoryAction => RepositoryConfirmed || RepositoryPending
            ? F10V7C1RepositoryAction.Retry : F10V7C1RepositoryAction.Commit;

        internal void CaptureRepositoryCommitResult(F10V7SessionCommitResult result)
        {
            _commitResult = result;
            if (result != null && result.Persisted && !result.Pending &&
                !string.IsNullOrWhiteSpace(result.ConfirmedJson))
            {
                if (ConfirmedJson == null || string.Equals(ConfirmedJson,
                        result.ConfirmedJson, StringComparison.Ordinal))
                {
                    ConfirmedJson = result.ConfirmedJson;
                    _confirmedCommitResult ??= result;
                    RepositoryConfirmed = true;
                    RepositoryPending = false;
                }
                else RepositoryPending = true;
            }
            else if (result == null || result.Pending || RepositoryConfirmed)
                RepositoryPending = true;
        }

        internal bool TryAcquireOperation(out object operationToken)
        {
            operationToken = null;
            if (Interlocked.CompareExchange(ref _operationInFlight, 1, 0) != 0) return false;
            object token = new object();
            _commitResult = null;
            _activeOperationToken = token;
            operationToken = token;
            return true;
        }

        internal void ReleaseOperation(object operationToken)
        {
            if (operationToken == null || !ReferenceEquals(_activeOperationToken, operationToken)) return;
            _activeOperationToken = null;
            Interlocked.Exchange(ref _operationInFlight, 0);
        }
    }

    internal sealed class F10V7C1CombatPaymentOutcome
    {
        private F10V7C1CombatPaymentOutcome(bool succeeded, bool pending, string message)
        {
            Succeeded = succeeded;
            Pending = pending;
            Message = message ?? string.Empty;
        }

        internal bool Succeeded { get; }
        internal bool Pending { get; }
        internal string Message { get; }

        internal static F10V7C1CombatPaymentOutcome Completed(string message) =>
            new F10V7C1CombatPaymentOutcome(true, false, message);
        internal static F10V7C1CombatPaymentOutcome PendingResult(string message) =>
            new F10V7C1CombatPaymentOutcome(false, true, message);
        internal static F10V7C1CombatPaymentOutcome Rejected(string message) =>
            new F10V7C1CombatPaymentOutcome(false, false, message);
    }
}
#endif
