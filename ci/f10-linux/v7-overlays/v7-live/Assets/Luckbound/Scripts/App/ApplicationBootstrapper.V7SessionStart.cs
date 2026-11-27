#if UNITY_EDITOR
using System;
using System.Text;
using System.Threading.Tasks;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Core.Saves.V7;
using UnityEngine;

namespace Luckbound.App
{
    /// <summary>Starts a composition from the repository-retained authenticated V7 opening handle.</summary>
    public sealed partial class ApplicationBootstrapper
    {
        private static readonly UTF8Encoding StrictV7Utf8 = new UTF8Encoding(false, true);
        private static readonly object F10V7SessionBindingFactoryKey = new object();
        private bool _f10V7SessionStartAttempted;
        private F10V7SessionInitializationBinding _f10V7SessionStartAttemptOwner;
        private F10V7PublishedSession _f10V7PublishedSession;
        private PendingF10V7TutorialAdvance _f10V7ReadinessReceiptPending;
        private ExpeditionReadinessPreparedPublication _f10V7ReadinessReceiptPublication;
        private F10V7ExpeditionReadProjection _f10V7ReadinessReceiptProjection;
        private string _f10V7ReadinessReceiptConfirmedJson;
        private long _f10V7ReadinessReceiptBaseRevision;
        private bool _f10V7ReadinessReceiptPromoted;

        internal Task<F10V7SessionStartResult> StartAdmittedV7TechnicalSessionAsync(
            ExpeditionGameplayComposition composition)
        {
            string error = composition == null
                ? "Technical V7 composition requires an active composition owner."
                : "Technical V7 composition requires the opaque verified physical-input receipt overload.";
            return Task.FromResult(F10V7SessionStartResult.Rejected(error));
        }

        internal Task<F10V7SessionStartResult> StartAdmittedV7TechnicalSessionAsync(
            ExpeditionGameplayComposition composition, F10V7VerifiedPhysicalInputs verifiedInputs)
        {
            if (verifiedInputs == null)
                return Task.FromResult(F10V7SessionStartResult.Rejected(
                    "Technical V7 composition requires the opaque verified physical-input receipt."));
            return StartAdmittedV7SessionAsync(composition, verifiedInputs);
        }

        internal Task<F10V7SessionStartResult> StartAdmittedV7SessionAsync(
            ExpeditionGameplayComposition composition) =>
            StartAdmittedV7SessionAsync(composition, null);

        internal async Task<F10V7SessionStartResult> StartAdmittedV7SessionAsync(
            ExpeditionGameplayComposition composition, F10V7VerifiedPhysicalInputs verifiedInputs)
        {
            if (composition == null)
                return F10V7SessionStartResult.Rejected("A live expedition composition is required.");

            await _profileMutationGate.WaitAsync();
            F10V7SessionInitializationBinding binding = null;
            bool compositionStarted = false;
            try
            {
                if (!ReferenceEquals(_instance, this) || !composition.isActiveAndEnabled ||
                    !SaveDataReady || _saveRepository == null ||
                    _f10V7AdmissionOwner == null || _f10V7AdmissionContext == null ||
                    !_f10V7AdmissionProfileWriteBlock || _f10V7PublishedSession != null ||
                    _f10V7SessionStartAttempted || ActiveRun != null || _activeExpedition != null ||
                    _activeExpeditionRevision != _f10V7AdmissionContext.AppRevision ||
                    HasBlockingV6StateForV7Admission() ||
                    !composition.IsEligibleForAdmittedV7Initialization(this))
                    return F10V7SessionStartResult.Rejected(
                        "No hay una admisión V7 privada, vigente y sin un dueño de gameplay previo.");

                F10V7AdmissionContext context = _f10V7AdmissionContext;
                if (!IsValidAdmittedV7Opening(context, out ProfileSaveEnvelopeV7 envelope,
                        out ExpeditionRunConfiguration run, out F10V7ExpeditionReadProjection projection,
                        out ExpeditionSaveData checkpoint, out string validationError))
                    return F10V7SessionStartResult.Rejected(validationError);

                if (verifiedInputs != null &&
                    !TryValidateF10V7VerifiedInputsForAdmission(context, envelope, verifiedInputs,
                        out string verifiedInputsError))
                    return F10V7SessionStartResult.Rejected(verifiedInputsError);

                if (_activeExpeditionRevision == long.MaxValue)
                    return F10V7SessionStartResult.Rejected("The expedition revision cannot advance safely.");

                binding = new F10V7SessionInitializationBinding(F10V7SessionBindingFactoryKey,
                    this, composition, context,
                    run, projection, _activeExpeditionRevision, _activeExpeditionRevision + 1,
                    verifiedInputs);

                // Validate App issuance and the empty owner first, install the exact binding,
                // then validate its retained identity before the attempt latch or any gameplay IO.
                if (!composition.TryInstallAdmittedV7InitializationBinding(binding,
                        out string installError))
                    return F10V7SessionStartResult.Rejected(installError);

                _f10V7SessionStartAttempted = true;
                _f10V7SessionStartAttemptOwner = binding;

                if (!composition.BeginAdmittedV7Initialization(binding, run, checkpoint,
                        out string compositionError))
                {
                    TryReleaseV7SessionStartAttemptAfterVerifiedCleanup(binding, composition);
                    return F10V7SessionStartResult.Rejected(compositionError);
                }
                compositionStarted = true;

                if (!IsCurrentV7SessionInitializationBinding(binding, composition) ||
                    !IsCompositionPreparedForV7(binding, composition))
                    throw new InvalidOperationException(
                        "The authenticated owner or private room/actor binding changed before finalization.");

                if (!composition.FinalizeAdmittedV7Initialization(out compositionError))
                    throw new InvalidOperationException(compositionError);

                if (!IsCurrentV7SessionInitializationBinding(binding, composition) ||
                    !IsCompositionReadyForV7(binding, composition) ||
                    ActiveRun != null || _activeExpedition != null ||
                    _activeExpeditionRevision != binding.AdmissionRevision ||
                    !ReferenceEquals(_expeditionSimulationPauseLeaseProviderOwner, composition) ||
                    _expeditionSessionMutationPublicationOwner != null ||
                    _expeditionSessionStateCapture != null || _expeditionSessionMutationPreparer != null)
                    throw new InvalidOperationException(
                        "The composition or App owner changed during synchronous finalization.");

                ExpeditionSaveData publishState = binding.Checkpoint;
                if (publishState == null || !StatesEqual(publishState, checkpoint) ||
                    !ReferenceEquals(run.Inventory.Catalog, context.ItemCatalog))
                    throw new InvalidOperationException("The final V7 projection no longer matches its admitted checkpoint.");

                ExpeditionSaveData publishedState = publishState.Clone();
                var publishedSession = new F10V7PublishedSession(binding, binding.PublishedRevision,
                    binding.AdmittedJson, binding.Projection);
                F10V7SessionStartResult success = F10V7SessionStartResult.Success(
                    "The admitted V7 opening projection is bound and published read-only; profile writes remain blocked.");

                // This is the single publication point: no user code or awaited work runs between
                // these assignments. The startup pause lease is released only after the exact
                // published session and revision are coherent for synchronous listeners.
                _activeExpedition = publishedState;
                ActiveRun = run;
                _activeExpeditionRevision = binding.PublishedRevision;
                _f10V7PublishedSession = publishedSession;
                composition.CompleteAdmittedV7Publication(binding);
                return success;
            }
            catch (Exception exception)
            {
                if (binding != null)
                {
                    composition.AbortAdmittedV7Initialization(binding,
                        "V7 session start failed closed: " + exception.Message);
                    TryReleaseV7SessionStartAttemptAfterVerifiedCleanup(binding, composition);
                }
                else if (compositionStarted)
                    composition.AbortAdmittedV7Initialization(null,
                        "V7 session start failed closed: " + exception.Message);
                return F10V7SessionStartResult.Rejected("V7 session start failed closed: " + exception.Message);
            }
            finally { _profileMutationGate.Release(); }
        }

        private bool IsValidAdmittedV7Opening(F10V7AdmissionContext context,
            out ProfileSaveEnvelopeV7 envelope, out ExpeditionRunConfiguration run,
            out F10V7ExpeditionReadProjection projection, out ExpeditionSaveData checkpoint,
            out string error)
        {
            envelope = null;
            run = null;
            projection = null;
            checkpoint = null;
            error = string.Empty;
            if (context == null || !ReferenceEquals(context.Bootstrapper, this) ||
                !ReferenceEquals(context.Repository, _saveRepository) || context.ItemCatalog == null ||
                context.Registry == null || context.SessionHandle == null ||
                !ReferenceEquals(context.SessionHandle.Repository, _saveRepository) ||
                !ReferenceEquals(context.SessionHandle.ItemCatalog, context.ItemCatalog) ||
                !ReferenceEquals(context.SessionHandle.Registry, context.Registry) ||
                !string.Equals(context.SessionHandle.ExactJson, context.ConfirmedJson, StringComparison.Ordinal) ||
                !RunId.TryParse(context.RunId, out RunId runId) ||
                !context.Repository.IsRetainedF10V7SessionHandle(context.SessionHandle,
                    context.ConfirmedJson, runId, context.ItemCatalog, context.Registry))
            {
                error = "The V7 repository handle is no longer bound to the exact admitted JSON/catalog/registry.";
                return false;
            }

            byte[] bytes;
            try { bytes = StrictV7Utf8.GetBytes(context.ConfirmedJson); }
            catch (EncoderFallbackException exception)
            {
                error = "The admitted V7 JSON is not strict UTF-8: " + exception.Message;
                return false;
            }
            if (!SaveV7JsonCodec.TryDecodeProfile(bytes, out envelope, out SaveV7Error decodeError) ||
                envelope == null || envelope.activeExpedition == null ||
                envelope.generation != context.Generation || envelope.sequence != context.Sequence ||
                !string.Equals(envelope.activeExpedition.runId, context.RunId, StringComparison.Ordinal))
            {
                error = decodeError.Code == SaveV7ErrorCode.None
                    ? "The exact admitted V7 profile identity or revision changed."
                    : decodeError.Code + ": " + decodeError.Message;
                return false;
            }

            if (!SaveV7CandidateMapper.TryRestore(envelope, context.ItemCatalog, context.Registry,
                    out run, out ExpeditionSaveDataV7 restoredState,
                    out ResolvedEncounterPlanV1 resolvedPlan, out SaveV7Error restoreError) ||
                run == null || restoredState == null || resolvedPlan == null || run.RunId != runId ||
                !ReferenceEquals(run.Inventory?.Catalog, context.ItemCatalog))
            {
                error = restoreError.Code == SaveV7ErrorCode.None
                    ? "The authenticated V7 opening did not restore one exact run/state/plan."
                    : restoreError.Code + ": " + restoreError.Message;
                return false;
            }

            ExpeditionSaveDataV7 retainedBaseline = context.SessionHandle.BaselineState;
            if (retainedBaseline == null || !StatesEqual(restoredState, retainedBaseline))
            {
                error = "The V7 state restored from exact JSON differs from the repository handle baseline.";
                return false;
            }

            if (!F10V7ExpeditionReadProjector.TryCreate(envelope, context.ItemCatalog,
                    context.Registry, out projection, out SaveV7Error projectionError) ||
                projection == null || !StatesEqual(restoredState, projection.RestoredStateV7))
            {
                error = projectionError.Code == SaveV7ErrorCode.None
                    ? "The V7 composition projection differs from the exact restored state."
                    : projectionError.Code + ": " + projectionError.Message;
                return false;
            }

            checkpoint = projection.CompositionReadOnlyView;
            if (checkpoint == null || !string.Equals(checkpoint.runId, context.RunId, StringComparison.Ordinal) ||
                checkpoint.generationVersion != run.Route?.GenerationVersion ||
                !string.Equals(checkpoint.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(checkpoint.classId, run.ClassId, StringComparison.Ordinal) ||
                !string.Equals(checkpoint.raceId, run.RaceId, StringComparison.Ordinal) ||
                run.Route == null || run.Route.Nodes.Count == 0)
            {
                error = "The composition projection does not match the exact V7 run identity and route.";
                return false;
            }
            return true;
        }

        private bool IsCurrentV7SessionInitializationBinding(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition composition)
        {
            return binding != null && composition != null && ReferenceEquals(_instance, this) &&
                ReferenceEquals(binding.Bootstrapper, this) && ReferenceEquals(binding.Composition, composition) &&
                ReferenceEquals(_f10V7AdmissionOwner, binding.AdmissionOwner) &&
                binding.MatchesAdmissionContext(_f10V7AdmissionContext) &&
                ReferenceEquals(binding.AdmissionRepository, _saveRepository) &&
                _f10V7AdmissionProfileWriteBlock &&
                binding.AdmissionRevision == binding.ContextAppRevision &&
                binding.PublishedRevision == binding.AdmissionRevision + 1 &&
                _saveRepository.IsRetainedF10V7SessionHandle(binding.SessionHandle,
                    binding.AdmittedJson, binding.Run.RunId,
                    binding.AdmissionItemCatalog, binding.AdmissionRegistry) &&
                ReferenceEquals(composition.CaptureV7InitializationBinding(), binding);
        }

        internal bool CanIssueV7SessionInitializationBinding(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition composition)
        {
            F10V7AdmissionContext context = _f10V7AdmissionContext;
            return binding != null && composition != null && ReferenceEquals(_instance, this) &&
                binding.IsOwnedBy(this, composition) && binding.MatchesAdmissionContext(context) &&
                context != null && ReferenceEquals(_f10V7AdmissionOwner, binding.AdmissionOwner) &&
                ReferenceEquals(context.Repository, _saveRepository) && _f10V7AdmissionProfileWriteBlock &&
                !_f10V7SessionStartAttempted && _f10V7SessionStartAttemptOwner == null &&
                _f10V7PublishedSession == null && ActiveRun == null && _activeExpedition == null &&
                _activeExpeditionRevision == binding.AdmissionRevision &&
                binding.AdmissionRevision == context.AppRevision &&
                binding.PublishedRevision == binding.AdmissionRevision + 1 &&
                _saveRepository != null &&
                _saveRepository.IsRetainedF10V7SessionHandle(binding.SessionHandle,
                    binding.AdmittedJson, binding.Run.RunId,
                    binding.AdmissionItemCatalog, binding.AdmissionRegistry);
        }

        internal bool CanRecordV7SessionStartCleanup(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition composition)
        {
            F10V7AdmissionContext context = _f10V7AdmissionContext;
            return binding != null && composition != null && ReferenceEquals(_instance, this) &&
                ReferenceEquals(binding.Bootstrapper, this) && ReferenceEquals(binding.Composition, composition) &&
                binding.MatchesAdmissionContext(context) && context != null &&
                ReferenceEquals(_f10V7AdmissionOwner, binding.AdmissionOwner) &&
                ReferenceEquals(context.Repository, _saveRepository) && _f10V7AdmissionProfileWriteBlock &&
                _f10V7SessionStartAttempted && ReferenceEquals(_f10V7SessionStartAttemptOwner, binding) &&
                _f10V7PublishedSession == null && ActiveRun == null && _activeExpedition == null &&
                _activeExpeditionRevision == binding.AdmissionRevision &&
                binding.AdmissionRevision == context.AppRevision && _saveRepository != null &&
                _saveRepository.IsRetainedF10V7SessionHandle(binding.SessionHandle,
                    binding.AdmittedJson, binding.Run.RunId,
                    binding.AdmissionItemCatalog, binding.AdmissionRegistry);
        }

        private bool TryReleaseV7SessionStartAttemptAfterVerifiedCleanup(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition composition)
        {
            if (!CanRecordV7SessionStartCleanup(binding, composition) ||
                !composition.ConsumeVerifiedAdmittedV7InitializationCleanup(binding) ||
                !CanRecordV7SessionStartCleanup(binding, composition))
                return false;

            _f10V7SessionStartAttempted = false;
            _f10V7SessionStartAttemptOwner = null;
            return true;
        }

        private bool IsCompositionPreparedForV7(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition composition)
        {
            ExpeditionReadinessCompositionView view = composition.CaptureReadinessCompositionView();
            ExpeditionSessionReadinessRegistration registration = view?.ReadinessRegistration;
            return view != null && ReferenceEquals(view.Application, this) &&
                ReferenceEquals(view.Owner, composition) && ReferenceEquals(view.Run, binding.Run) &&
                ReferenceEquals(registration?.V7InitializationBinding, binding) && !view.IsReady &&
                registration?.CheckpointBinding != null && registration.CurrentActivation != null &&
                registration.CurrentActivation.State == ExpeditionRoomActivationState.Bound;
        }

        private bool IsCompositionReadyForV7(
            F10V7SessionInitializationBinding binding, ExpeditionGameplayComposition composition)
        {
            ExpeditionReadinessCompositionView view = composition.CaptureReadinessCompositionView();
            ExpeditionSessionReadinessRegistration registration = view?.ReadinessRegistration;
            return view != null && view.IsReady && ReferenceEquals(view.Application, this) &&
                ReferenceEquals(view.Owner, composition) && ReferenceEquals(view.Run, binding.Run) &&
                ReferenceEquals(registration?.V7InitializationBinding, binding) &&
                registration?.CheckpointBinding != null && registration.CurrentActivation != null &&
                registration.IsV7InitializationReady(requireCompositionReady: true);
        }

        internal bool IsCurrentV7PublishedSession(
            F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration, long revision)
        {
            F10V7PublishedSession published = _f10V7PublishedSession;
            return binding != null && registration != null && published != null &&
                ReferenceEquals(published.Binding, binding) && published.Revision == revision &&
                !string.IsNullOrWhiteSpace(published.ConfirmedJson) && published.Projection != null &&
                ReferenceEquals(registration.Owner, binding.Composition) &&
                IsCurrentV7SessionInitializationBinding(binding, binding.Composition) &&
                _activeExpeditionRevision == published.Revision &&
                ReferenceEquals(ActiveRun, published.Run) &&
                published.Run != null && published.Run.RunId == binding.Run.RunId &&
                published.Run.Seed == binding.Run.Seed &&
                string.Equals(published.Run.ClassId, binding.Run.ClassId, StringComparison.Ordinal) &&
                string.Equals(published.Run.RaceId, binding.Run.RaceId, StringComparison.Ordinal) &&
                string.Equals(published.Run.Route?.CanonicalForm, binding.Run.Route?.CanonicalForm,
                    StringComparison.Ordinal) &&
                ReferenceEquals(published.Run.Inventory?.Catalog, binding.AdmissionItemCatalog) &&
                _activeExpedition != null &&
                StatesEqual(_activeExpedition, published.Projection.CompositionReadOnlyView) &&
                ReferenceEquals(_expeditionSimulationPauseLeaseProviderOwner, binding.Composition) &&
                _expeditionSessionMutationPublicationOwner == null &&
                _expeditionSessionStateCapture == null && _expeditionSessionMutationPreparer == null;
        }

        internal bool TryGetCurrentV7PublishedSnapshot(F10V7SessionInitializationBinding binding,
            out F10V7ExpeditionReadProjection projection, out string confirmedJson, out long revision)
        {
            F10V7PublishedSession published = _f10V7PublishedSession;
            if (binding != null && published != null && ReferenceEquals(published.Binding, binding) &&
                published.Projection != null && !string.IsNullOrWhiteSpace(published.ConfirmedJson))
            {
                projection = published.Projection;
                confirmedJson = published.ConfirmedJson;
                revision = published.Revision;
                return true;
            }
            projection = null;
            confirmedJson = null;
            revision = 0;
            return false;
        }

        private bool RegisterPendingAdmittedV7Publication(
            F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessPreparedPublication publication,
            long expectedRevision, out string error)
        {
            error = string.Empty;
            if (!TryGetExactPendingV7Publication(binding, registration, publication, expectedRevision,
                    out PendingF10V7TutorialAdvance pending, out F10V7SessionCommitResult commit,
                    out F10V7ExpeditionReadProjection projection, out error))
                return false;
            if (!ValidatePendingV7ConfirmedCandidate(pending, commit, projection, out error)) return false;

            if (_f10V7ReadinessReceiptPending == null)
            {
                _f10V7ReadinessReceiptPending = pending;
                _f10V7ReadinessReceiptPublication = publication;
                _f10V7ReadinessReceiptProjection = projection;
                _f10V7ReadinessReceiptConfirmedJson = commit.ConfirmedJson;
                _f10V7ReadinessReceiptBaseRevision = expectedRevision;
                _f10V7ReadinessReceiptPromoted = false;
                return true;
            }

            if (!ReferenceEquals(_f10V7ReadinessReceiptPending, pending) ||
                !ReferenceEquals(_f10V7ReadinessReceiptPublication, publication) ||
                !V7ProjectionsEqual(_f10V7ReadinessReceiptProjection, projection) ||
                _f10V7ReadinessReceiptBaseRevision != expectedRevision ||
                !string.Equals(_f10V7ReadinessReceiptConfirmedJson, commit.ConfirmedJson,
                    StringComparison.Ordinal))
            {
                error = "A different pending V7 publication already owns the readiness receipt.";
                return false;
            }
            return true;
        }

        private bool PromoteConfirmedAdmittedV7Publication(
            F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessPreparedPublication publication, out string error)
        {
            error = string.Empty;
            PendingF10V7TutorialAdvance pending = _pendingF10V7TutorialAdvance;
            long expectedRevision = pending == null ? -1 : pending.ExpectedExpeditionRevision;
            if (!TryGetExactPendingV7Publication(binding, registration, publication, expectedRevision,
                    out pending, out F10V7SessionCommitResult commit,
                    out F10V7ExpeditionReadProjection projection, out error))
                return false;
            if (!ReferenceEquals(_f10V7ReadinessReceiptPending, pending) ||
                !ReferenceEquals(_f10V7ReadinessReceiptPublication, publication) ||
                _f10V7ReadinessReceiptBaseRevision != expectedRevision ||
                !string.Equals(_f10V7ReadinessReceiptConfirmedJson, commit.ConfirmedJson,
                    StringComparison.Ordinal) || !ValidatePendingV7ConfirmedCandidate(pending, commit, projection, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                    error = "The promoted V7 state is not the exact registered pending publication.";
                return false;
            }

            long nextRevision;
            try { nextRevision = checked(expectedRevision + 1); }
            catch (OverflowException)
            {
                error = "The V7 session revision cannot advance safely.";
                return false;
            }

            F10V7PublishedSession published = _f10V7PublishedSession;
            ExpeditionSaveData nextState = projection.CompositionReadOnlyView;
            ExpeditionSaveData preparedNextState;
            try { preparedNextState = nextState.Clone(); }
            catch (Exception exception)
            {
                error = "The confirmed V7 read projection could not be cloned before promotion: " + exception.Message;
                return false;
            }

            bool appSnapshotAtNextRevision = published != null &&
                ReferenceEquals(published.Binding, binding) && published.Revision == nextRevision &&
                string.Equals(published.ConfirmedJson, commit.ConfirmedJson, StringComparison.Ordinal) &&
                StatesEqual(published.Projection?.CompositionReadOnlyView, nextState);
            if (_f10V7ReadinessReceiptPromoted)
            {
                if (appSnapshotAtNextRevision &&
                    _activeExpeditionRevision == nextRevision && StatesEqual(_activeExpedition, nextState))
                    return true;
                error = "The retained V7 publication no longer matches its promoted session snapshot.";
                return false;
            }

            if (appSnapshotAtNextRevision)
            {
                bool appStateAtBaseRevision = _activeExpeditionRevision == expectedRevision &&
                    StatesEqual(_activeExpedition, published.Projection == null
                        ? null : binding.Projection.CompositionReadOnlyView);
                bool appStateAtNextRevision = _activeExpeditionRevision == nextRevision &&
                    StatesEqual(_activeExpedition, nextState);
                if (!appStateAtBaseRevision && !appStateAtNextRevision)
                {
                    error = "The partially promoted App state no longer matches either exact V7 revision.";
                    return false;
                }

                if (appStateAtBaseRevision)
                {
                    _activeExpedition = preparedNextState;
                    _activeExpeditionRevision = nextRevision;
                }
                if (!registration.IsAdmittedV7SnapshotCurrent(binding, commit.ConfirmedJson,
                        projection, nextRevision))
                {
                    if (!registration.CanCompleteAdmittedV7SnapshotPromotion(binding,
                            commit.ConfirmedJson, projection, expectedRevision, nextRevision))
                    {
                        error = "The exact registration snapshot cannot reconcile the already promoted App state.";
                        return false;
                    }
                    registration.RecordPromotedAdmittedV7Snapshot(commit.ConfirmedJson, projection, nextRevision);
                }
                _f10V7ReadinessReceiptPromoted = true;
                return true;
            }

            if (published == null || !ReferenceEquals(published.Binding, binding) ||
                published.Revision != expectedRevision || _activeExpeditionRevision != expectedRevision ||
                _activeExpedition == null ||
                !StatesEqual(_activeExpedition, published.Projection.CompositionReadOnlyView) ||
                !registration.CanPromoteAdmittedV7Snapshot(binding, commit.ConfirmedJson,
                    projection, expectedRevision, nextRevision))
            {
                error = "The V7 opening snapshot or Tutorial binder witness changed before promotion.";
                return false;
            }

            if (!published.TryPromote(commit.ConfirmedJson, projection, nextRevision))
            {
                error = "The App could not retain the exact confirmed V7 projection.";
                return false;
            }
            _activeExpedition = preparedNextState;
            _activeExpeditionRevision = nextRevision;
            registration.RecordPromotedAdmittedV7Snapshot(commit.ConfirmedJson, projection, nextRevision);
            _f10V7ReadinessReceiptPromoted = true;
            return true;
        }

        private bool TryGetExactPendingV7Publication(
            F10V7SessionInitializationBinding binding,
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessPreparedPublication publication, long expectedRevision,
            out PendingF10V7TutorialAdvance pending, out F10V7SessionCommitResult commit,
            out F10V7ExpeditionReadProjection projection, out string error)
        {
            pending = _pendingF10V7TutorialAdvance;
            commit = pending?.CommitResult;
            projection = null;
            error = string.Empty;
            ExpeditionGameplayComposition composition = binding?.Composition;
            F10V7PublishedSession published = _f10V7PublishedSession;
            bool exactReceiptIdentity = ReferenceEquals(_f10V7ReadinessReceiptPending, pending) &&
                ReferenceEquals(_f10V7ReadinessReceiptPublication, publication) &&
                _f10V7ReadinessReceiptBaseRevision == expectedRevision &&
                _f10V7ReadinessReceiptProjection != null &&
                string.Equals(_f10V7ReadinessReceiptConfirmedJson, commit?.ConfirmedJson, StringComparison.Ordinal);
            bool exactBaseSnapshot = published != null && ReferenceEquals(published.Binding, binding) &&
                published.Revision == expectedRevision && _activeExpeditionRevision == expectedRevision &&
                StatesEqual(_activeExpedition, published.Projection?.CompositionReadOnlyView);
            bool exactPartialSnapshot = expectedRevision < long.MaxValue && exactReceiptIdentity &&
                published != null && ReferenceEquals(published.Binding, binding) &&
                published.Revision == expectedRevision + 1 &&
                string.Equals(published.ConfirmedJson, commit?.ConfirmedJson, StringComparison.Ordinal) &&
                ReferenceEquals(published.Projection, _f10V7ReadinessReceiptProjection) &&
                ((_activeExpeditionRevision == expectedRevision &&
                    StatesEqual(_activeExpedition, binding?.Projection.CompositionReadOnlyView)) ||
                 (_activeExpeditionRevision == expectedRevision + 1 &&
                    StatesEqual(_activeExpedition, _f10V7ReadinessReceiptProjection?.CompositionReadOnlyView)));
            if (pending == null || commit == null || binding == null || registration == null || publication == null ||
                !ReferenceEquals(_instance, this) || !ReferenceEquals(pending, _pendingF10V7TutorialAdvance) ||
                !ReferenceEquals(pending.Binding, binding) || !ReferenceEquals(pending.Composition, composition) ||
                !ReferenceEquals(pending.Registration, registration) || !ReferenceEquals(pending.Publication, publication) ||
                !ReferenceEquals(registration.Application, this) || !ReferenceEquals(registration.Owner, composition) ||
                !ReferenceEquals(registration.V7InitializationBinding, binding) ||
                !ReferenceEquals(publication.Registration, registration) ||
                !ReferenceEquals(publication.Activation, registration.CurrentActivation) ||
                pending.ExpectedExpeditionRevision != expectedRevision ||
                publication.PreparedAtRevision != expectedRevision || !pending.MutationGateRetained ||
                pending.OperationInFlight != 1 || pending.ActiveOperationToken == null ||
                _profileMutationGate.CurrentCount != 0 || pending.SimulationPauseLease == null ||
                !ReferenceEquals(_expeditionSimulationPauseLeaseProviderOwner, composition) ||
                !binding.IsCurrentFor(this, composition) || !_f10V7AdmissionProfileWriteBlock ||
                !ReferenceEquals(binding.AdmissionRepository, _saveRepository) ||
                !(exactBaseSnapshot || exactPartialSnapshot) ||
                !commit.Persisted || commit.Pending || !pending.RepositoryConfirmed ||
                string.IsNullOrWhiteSpace(commit.ConfirmedJson) ||
                !string.Equals(pending.ConfirmedJson, commit.ConfirmedJson, StringComparison.Ordinal) ||
                !_saveRepository.IsRetainedF10V7SessionHandle(binding.SessionHandle,
                    binding.AdmittedJson, binding.Run.RunId, binding.AdmissionItemCatalog, binding.AdmissionRegistry))
            {
                error = "No exact App-owned V7 repository readback, pending operation, lease, and publication are current.";
                return false;
            }

            if (!TryDecodeAndValidatePendingV7(binding, pending, commit,
                    out projection, out error)) return false;
            if (exactReceiptIdentity)
            {
                if (!V7ProjectionsEqual(_f10V7ReadinessReceiptProjection, projection))
                {
                    projection = null;
                    error = "The repeated repository verification changed the retained V7 projection.";
                    return false;
                }
                projection = _f10V7ReadinessReceiptProjection;
            }
            return true;
        }

        private bool ValidatePendingV7ConfirmedCandidate(PendingF10V7TutorialAdvance pending,
            F10V7SessionCommitResult commit, F10V7ExpeditionReadProjection projection, out string error)
        {
            error = string.Empty;
            if (pending == null || commit == null || projection == null || pending.Publication == null ||
                pending.Candidate == null || pending.Publication.CandidateSave == null ||
                !pending.ReceiptRegistered && _f10V7ReadinessReceiptPending != null)
            {
                error = "The private pending V7 publication does not contain its candidate and actor capture.";
                return false;
            }
            ExpeditionSaveData projected = projection.CompositionReadOnlyView;
            if (projected == null || !StatesEqual(projected, pending.Publication.CandidateSave) ||
                !StatesEqual(projection.CompositionReadOnlyView, pending.ConfirmedProjection?.CompositionReadOnlyView))
            {
                error = "The authenticated current projection differs from the exact binder candidate.";
                return false;
            }
            return true;
        }

        private bool TryDecodeAndValidatePendingV7(F10V7SessionInitializationBinding binding,
            PendingF10V7TutorialAdvance pending, F10V7SessionCommitResult commit,
            out F10V7ExpeditionReadProjection projection, out string error)
        {
            projection = null;
            error = string.Empty;
            try
            {
                if (!SaveV7JsonCodec.TryDecodeProfile(StrictV7Utf8.GetBytes(binding.AdmittedJson),
                        out ProfileSaveEnvelopeV7 prior, out SaveV7Error validationError) ||
                    !SaveV7JsonCodec.TryDecodeProfile(StrictV7Utf8.GetBytes(commit.ConfirmedJson),
                        out ProfileSaveEnvelopeV7 confirmed, out validationError))
                {
                    error = validationError.Code + ": " + validationError.Message;
                    return false;
                }
                if (prior == null || confirmed == null || prior.profile == null || confirmed.profile == null ||
                    prior.activeExpedition == null || confirmed.activeExpedition == null ||
                    confirmed.generation != checked(prior.generation + 1) ||
                    confirmed.sequence != checked(prior.sequence + 1) ||
                    !SaveV7CandidateMapper.TryValidateCommit(confirmed, binding.Run,
                        binding.AdmissionRegistry, out validationError) ||
                    !SaveV7CandidateMapper.TryValidateTransition(prior, null, confirmed, out validationError) ||
                    !SaveV7CandidateMapper.HasSameProfilePayload(prior.profile, confirmed.profile))
                {
                    error = validationError.Code == SaveV7ErrorCode.None
                        ? "The repository readback is not the single authenticated transition from the admitted V7 opening."
                        : validationError.Code + ": " + validationError.Message;
                    return false;
                }

                ProfileSaveEnvelopeV7 candidateEnvelope = pending.Candidate.CreateEnvelope(
                    confirmed.generation, confirmed.sequence, confirmed.savedAtUtc);
                if (!SaveV7CandidateMapper.HasSameProfilePayload(candidateEnvelope.profile, confirmed.profile) ||
                    !string.Equals(JsonUtility.ToJson(candidateEnvelope.activeExpedition),
                        JsonUtility.ToJson(confirmed.activeExpedition), StringComparison.Ordinal))
                {
                    error = "The repository readback does not contain the exact retained candidate and profile.";
                    return false;
                }
                if (!F10V7ExpeditionReadProjector.TryCreateCurrent(confirmed,
                        binding.AdmissionItemCatalog, binding.AdmissionRegistry,
                        out projection, out validationError) || projection == null ||
                    projection.CheckpointAuthorityV7 == null ||
                    !string.Equals(projection.SourceStateV7.currentNodeId,
                        F10V7TutorialAdvanceCandidate.C1NodeId, StringComparison.Ordinal) ||
                    !string.Equals(projection.CheckpointAuthorityV7.roomNodeId,
                        F10V7TutorialAdvanceCandidate.C1NodeId, StringComparison.Ordinal) ||
                    projection.SourceStateV7.encounterPlanPin?.entry == null ||
                    !string.Equals(projection.SourceStateV7.encounterPlanPin.entry.nodeId,
                        F10V7TutorialAdvanceCandidate.C1NodeId, StringComparison.Ordinal))
                {
                    error = validationError.Code == SaveV7ErrorCode.None
                        ? "The current authenticated V7 projection lacks its exact C1 pin or checkpoint."
                        : validationError.Code + ": " + validationError.Message;
                    return false;
                }
                return true;
            }
            catch (Exception exception) when (exception is EncoderFallbackException ||
                exception is OverflowException || exception is InvalidOperationException ||
                exception is ArgumentException)
            {
                error = "The exact V7 ConfirmedJson failed validation: " + exception.Message;
                return false;
            }
        }

        private static bool StatesEqual(ExpeditionSaveData left, ExpeditionSaveData right)
        {
            return left != null && right != null &&
                string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
        }

        private static bool StatesEqual(ExpeditionSaveDataV7 left, ExpeditionSaveDataV7 right)
        {
            return left != null && right != null &&
                string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
        }

        private static bool V7ProjectionsEqual(F10V7ExpeditionReadProjection left,
            F10V7ExpeditionReadProjection right)
        {
            return left != null && right != null &&
                StatesEqual(left.SourceStateV7, right.SourceStateV7) &&
                StatesEqual(left.RestoredStateV7, right.RestoredStateV7) &&
                StatesEqual(left.CompositionReadOnlyView, right.CompositionReadOnlyView) &&
                string.Equals(JsonUtility.ToJson(left.CheckpointAuthorityV7),
                    JsonUtility.ToJson(right.CheckpointAuthorityV7), StringComparison.Ordinal);
        }

        internal sealed class F10V7SessionInitializationBinding
        {
            private readonly ApplicationBootstrapper _bootstrapper;
            private readonly F10V7AdmissionContext _context;

            internal F10V7SessionInitializationBinding(object factoryKey,
                ApplicationBootstrapper bootstrapper, ExpeditionGameplayComposition composition,
                object context,
                ExpeditionRunConfiguration run, F10V7ExpeditionReadProjection projection,
                long admissionRevision, long publishedRevision)
                : this(factoryKey, bootstrapper, composition, context, run, projection,
                    admissionRevision, publishedRevision, null)
            {
            }

            internal F10V7SessionInitializationBinding(object factoryKey,
                ApplicationBootstrapper bootstrapper, ExpeditionGameplayComposition composition,
                object context,
                ExpeditionRunConfiguration run, F10V7ExpeditionReadProjection projection,
                long admissionRevision, long publishedRevision,
                F10V7VerifiedPhysicalInputs verifiedInputs)
            {
                if (!ReferenceEquals(factoryKey, F10V7SessionBindingFactoryKey))
                    throw new InvalidOperationException("Only the App start operation may issue an admitted V7 composition binding.");
                _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
                Composition = composition ?? throw new ArgumentNullException(nameof(composition));
                _context = context as F10V7AdmissionContext ??
                    throw new ArgumentException("The App admission context is required.", nameof(context));
                Run = run ?? throw new ArgumentNullException(nameof(run));
                Projection = projection ?? throw new ArgumentNullException(nameof(projection));
                if (publishedRevision <= admissionRevision)
                    throw new ArgumentOutOfRangeException(nameof(publishedRevision));
                AdmissionRevision = admissionRevision;
                PublishedRevision = publishedRevision;
                VerifiedPhysicalInputs = verifiedInputs;
            }

            internal ApplicationBootstrapper Bootstrapper => _bootstrapper;
            internal ExpeditionGameplayComposition Composition { get; }
            internal ExpeditionRunConfiguration Run { get; }
            internal F10V7ExpeditionReadProjection Projection { get; }
            internal F10V7VerifiedPhysicalInputs VerifiedPhysicalInputs { get; }
            internal SaveRepository.F10V7SessionHandle SessionHandle => _context.SessionHandle;
            internal long AdmissionRevision { get; }
            internal long PublishedRevision { get; }
            internal ExpeditionSaveData Checkpoint => Projection.CompositionReadOnlyView;
            internal object AdmissionOwner => _context.Owner;
            internal SaveRepository AdmissionRepository => _context.Repository;
            internal string AdmittedJson => _context.ConfirmedJson;
            internal ItemCatalog AdmissionItemCatalog => _context.ItemCatalog;
            internal EncounterCatalogRegistryV1 AdmissionRegistry => _context.Registry;
            internal long ContextAppRevision => _context.AppRevision;

            private F10V7AdmissionContext Context => _context;

            internal bool MatchesAdmissionContext(object context) => ReferenceEquals(_context, context);

            internal bool IsCurrentFor(ApplicationBootstrapper bootstrapper,
                ExpeditionGameplayComposition composition) =>
                _bootstrapper.IsCurrentV7SessionInitializationBinding(this, composition) &&
                ReferenceEquals(bootstrapper, _bootstrapper);

            internal bool IsOwnedBy(ApplicationBootstrapper bootstrapper,
                ExpeditionGameplayComposition composition) =>
                ReferenceEquals(_bootstrapper, bootstrapper) && ReferenceEquals(Composition, composition) &&
                ReferenceEquals(_context.Bootstrapper, bootstrapper);
        }

        internal sealed class F10V7PublishedSession
        {
            internal F10V7PublishedSession(F10V7SessionInitializationBinding binding, long revision,
                string confirmedJson, F10V7ExpeditionReadProjection projection)
            {
                Binding = binding ?? throw new ArgumentNullException(nameof(binding));
                Revision = revision;
                Run = binding.Run;
                ConfirmedJson = confirmedJson ?? throw new ArgumentNullException(nameof(confirmedJson));
                Projection = projection ?? throw new ArgumentNullException(nameof(projection));
            }

            internal F10V7SessionInitializationBinding Binding { get; }
            internal ExpeditionRunConfiguration Run { get; private set; }
            internal long Revision { get; private set; }
            internal string ConfirmedJson { get; private set; }
            internal F10V7ExpeditionReadProjection Projection { get; private set; }

            internal bool TryPromote(string confirmedJson,
                F10V7ExpeditionReadProjection projection, long revision)
            {
                return TryPromote(confirmedJson, projection, Run, revision);
            }

            internal bool TryPromote(string confirmedJson,
                F10V7ExpeditionReadProjection projection, ExpeditionRunConfiguration run, long revision)
            {
                if (string.IsNullOrWhiteSpace(confirmedJson) || projection == null || run == null ||
                    Binding?.Run == null || run.RunId != Binding.Run.RunId ||
                    run.Seed != Binding.Run.Seed ||
                    !string.Equals(run.ClassId, Binding.Run.ClassId, StringComparison.Ordinal) ||
                    !string.Equals(run.RaceId, Binding.Run.RaceId, StringComparison.Ordinal) ||
                    !string.Equals(run.Route?.CanonicalForm, Binding.Run.Route?.CanonicalForm,
                        StringComparison.Ordinal) ||
                    !ReferenceEquals(run.Inventory?.Catalog, Binding.AdmissionItemCatalog))
                    return false;
                if (revision == Revision + 1)
                {
                    ConfirmedJson = confirmedJson;
                    Projection = projection;
                    Run = run;
                    Revision = revision;
                    return true;
                }
                return revision == Revision &&
                    string.Equals(ConfirmedJson, confirmedJson, StringComparison.Ordinal) &&
                    ReferenceEquals(Run, run) &&
                    StatesEqual(Projection.CompositionReadOnlyView, projection.CompositionReadOnlyView);
            }
        }
    }

    internal sealed class F10V7SessionStartResult
    {
        private F10V7SessionStartResult(bool started, string message)
        {
            Started = started;
            Message = message ?? string.Empty;
        }

        internal bool Started { get; }
        internal string Message { get; }

        internal static F10V7SessionStartResult Success(string message) =>
            new F10V7SessionStartResult(true, message);

        internal static F10V7SessionStartResult Rejected(string message) =>
            new F10V7SessionStartResult(false, message);
    }
}
#endif
