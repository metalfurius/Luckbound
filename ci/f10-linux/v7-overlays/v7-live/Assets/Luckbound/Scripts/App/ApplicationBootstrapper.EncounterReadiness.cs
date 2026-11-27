using System;
using Luckbound.Content;
using Luckbound.Core.Armor;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Core.Inventory;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation;
using UnityEngine;

namespace Luckbound.App
{
    internal sealed class ExpeditionReadinessCompositionView
    {
        internal ExpeditionReadinessCompositionView(ExpeditionGameplayComposition owner,
            ApplicationBootstrapper application, ExpeditionSessionReadinessRegistration readinessRegistration,
            ExpeditionRunConfiguration run, GameObject playerObject, PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector, ExpeditionRoomTransitionController roomTransitions,
            IPlayerInputSource inputSource, ExpeditionPauseController pauseController,
            RoomCameraRig2D cameraRig, GameObject projectilePrefab, SkillCatalog skillCatalog,
            ItemCatalog itemCatalog, WeaponCatalog weaponCatalog, ArmorCatalog armorCatalog, bool isReady)
        {
            Owner = owner;
            Application = application;
            ReadinessRegistration = readinessRegistration;
            Run = run;
            PlayerObject = playerObject;
            PlayerMotor = playerMotor;
            ItemCollector = itemCollector;
            RoomTransitions = roomTransitions;
            InputSource = inputSource;
            PauseController = pauseController;
            CameraRig = cameraRig;
            ProjectilePrefab = projectilePrefab;
            SkillCatalog = skillCatalog;
            ItemCatalog = itemCatalog;
            WeaponCatalog = weaponCatalog;
            ArmorCatalog = armorCatalog;
            IsReady = isReady;
        }

        internal ExpeditionGameplayComposition Owner { get; }
        internal ApplicationBootstrapper Application { get; }
        internal ExpeditionSessionReadinessRegistration ReadinessRegistration { get; }
        internal ExpeditionRunConfiguration Run { get; }
        internal GameObject PlayerObject { get; }
        internal PlayerMotor2D PlayerMotor { get; }
        internal ItemCollector2D ItemCollector { get; }
        internal ExpeditionRoomTransitionController RoomTransitions { get; }
        internal IPlayerInputSource InputSource { get; }
        internal ExpeditionPauseController PauseController { get; }
        internal RoomCameraRig2D CameraRig { get; }
        internal GameObject ProjectilePrefab { get; }
        internal SkillCatalog SkillCatalog { get; }
        internal ItemCatalog ItemCatalog { get; }
        internal WeaponCatalog WeaponCatalog { get; }
        internal ArmorCatalog ArmorCatalog { get; }
        internal bool IsReady { get; }
    }

    internal sealed class ExpeditionReadinessTransitionView
    {
        internal ExpeditionReadinessTransitionView(ExpeditionRoomTransitionController controller,
            RoomLifetimeScope activeScope, GameObject activeRoomRoot, string activeNodeId,
            string activeTemplateId, string activeRoomInstanceId, bool isInitialized,
            bool isDisposed, bool isTransitioning, bool hasPreparedTransition)
        {
            Controller = controller;
            ActiveScope = activeScope;
            ActiveRoomRoot = activeRoomRoot;
            ActiveNodeId = activeNodeId ?? string.Empty;
            ActiveTemplateId = activeTemplateId ?? string.Empty;
            ActiveRoomInstanceId = activeRoomInstanceId ?? string.Empty;
            IsInitialized = isInitialized;
            IsDisposed = isDisposed;
            IsTransitioning = isTransitioning;
            HasPreparedTransition = hasPreparedTransition;
        }

        internal ExpeditionRoomTransitionController Controller { get; }
        internal RoomLifetimeScope ActiveScope { get; }
        internal GameObject ActiveRoomRoot { get; }
        internal string ActiveNodeId { get; }
        internal string ActiveTemplateId { get; }
        internal string ActiveRoomInstanceId { get; }
        internal bool IsInitialized { get; }
        internal bool IsDisposed { get; }
        internal bool IsTransitioning { get; }
        internal bool HasPreparedTransition { get; }
    }

    internal enum ExpeditionReadinessAuthorityState
    {
        Unavailable,
        Active,
        SessionEnded,
        ContextMismatch
    }

    internal sealed class ExpeditionReadinessAuthorityCapture
    {
        private readonly ExpeditionSaveData _activeExpedition;

        internal ExpeditionReadinessAuthorityCapture(ExpeditionReadinessAuthorityState state,
            ExpeditionRunConfiguration activeRun, ExpeditionSaveData activeExpedition,
            long activeExpeditionRevision, object simulationPauseLeaseOwner,
            object sessionMutationPublicationOwner, bool hasPendingPublication,
            bool hasPendingRoomAdvance, bool isRoomAdvanceInFlight,
            bool hasPendingSessionMutation, bool hasSimulationPauseLeaseProvider,
            bool hasSessionMutationPublication)
        {
            State = state;
            ActiveRun = activeRun;
            _activeExpedition = activeExpedition == null ? null : activeExpedition.Clone();
            ActiveExpeditionRevision = activeExpeditionRevision;
            SimulationPauseLeaseOwner = simulationPauseLeaseOwner;
            SessionMutationPublicationOwner = sessionMutationPublicationOwner;
            HasPendingPublication = hasPendingPublication;
            HasPendingRoomAdvance = hasPendingRoomAdvance;
            IsRoomAdvanceInFlight = isRoomAdvanceInFlight;
            HasPendingSessionMutation = hasPendingSessionMutation;
            HasSimulationPauseLeaseProvider = hasSimulationPauseLeaseProvider;
            HasSessionMutationPublication = hasSessionMutationPublication;
        }

        internal ExpeditionReadinessAuthorityState State { get; }
        internal ExpeditionRunConfiguration ActiveRun { get; }
        internal ExpeditionSaveData ActiveExpedition => _activeExpedition == null ? null : _activeExpedition.Clone();
        internal long ActiveExpeditionRevision { get; }
        internal object SimulationPauseLeaseOwner { get; }
        internal object SessionMutationPublicationOwner { get; }
        internal bool HasPendingPublication { get; }
        internal bool HasPendingRoomAdvance { get; }
        internal bool IsRoomAdvanceInFlight { get; }
        internal bool HasPendingSessionMutation { get; }
        internal bool HasSimulationPauseLeaseProvider { get; }
        internal bool HasSessionMutationPublication { get; }
    }

    internal enum ExpeditionReadinessPublicationStatus
    {
        NotConfirmed,
        Confirmed,
        StaleOrEnded
    }

    internal sealed class ExpeditionReadinessPublicationCheck
    {
        private readonly ExpeditionSaveData _confirmedExpedition;

        internal ExpeditionReadinessPublicationCheck(ExpeditionReadinessPublicationStatus status,
            long confirmedRevision, ExpeditionSaveData confirmedExpedition)
        {
            Status = status;
            ConfirmedRevision = confirmedRevision;
            _confirmedExpedition = confirmedExpedition == null ? null : confirmedExpedition.Clone();
        }

        internal ExpeditionReadinessPublicationStatus Status { get; }
        internal long ConfirmedRevision { get; }
        internal ExpeditionSaveData ConfirmedExpedition =>
            _confirmedExpedition == null ? null : _confirmedExpedition.Clone();
    }

    public sealed partial class ApplicationBootstrapper
    {
        internal ExpeditionReadinessAuthorityCapture CaptureEncounterReadinessAuthority(
            ExpeditionSessionReadinessRegistration registration)
        {
            if (registration == null) throw new ArgumentNullException(nameof(registration));

            long revisionBefore = _activeExpeditionRevision;
            ExpeditionRunConfiguration runBefore = ActiveRun;
            ExpeditionSaveData stateBefore = _activeExpedition;
            ExpeditionSaveData activeBefore = stateBefore == null ? null : stateBefore.Clone();
            object pauseOwnerBefore = _expeditionSimulationPauseLeaseProviderOwner;
            object publicationOwnerBefore = _expeditionSessionMutationPublicationOwner;
            bool hasPauseProvider = _expeditionSimulationPauseLeaseProvider != null;
            bool hasSessionPublication = _expeditionSessionStateCapture != null &&
                _expeditionSessionMutationPreparer != null;
            bool hasPendingPublication = _pendingExpeditionCandidate != null ||
                _pendingExpeditionSessionPublication != null;
            bool hasPendingRoomAdvance = _pendingRoomAdvance != null;
            bool roomAdvanceInFlight = IsRoomAdvanceInFlight;
            bool hasPendingSessionMutation = HasPendingSessionMutation;
#if UNITY_EDITOR
            PendingF10V7TutorialAdvance pendingV7Advance = _pendingF10V7TutorialAdvance;
            F10V7C1CombatPaymentPending pendingV7C1Payment = _pendingF10V7C1CombatPayment;
            hasPendingRoomAdvance = hasPendingRoomAdvance || pendingV7Advance != null;
            roomAdvanceInFlight = roomAdvanceInFlight ||
                pendingV7Advance != null && pendingV7Advance.OperationInFlight > 0;
            hasPendingSessionMutation = hasPendingSessionMutation ||
                pendingV7Advance != null && pendingV7Advance.MutationGateRetained;
#endif
            long revisionAfter = _activeExpeditionRevision;
            bool ownersStable = ReferenceEquals(pauseOwnerBefore, _expeditionSimulationPauseLeaseProviderOwner) &&
                ReferenceEquals(publicationOwnerBefore, _expeditionSessionMutationPublicationOwner);
            bool sessionStable = ReferenceEquals(stateBefore, _activeExpedition) &&
                ReferenceEquals(runBefore, ActiveRun);

            var flags = new AuthorityFlags(pauseOwnerBefore, publicationOwnerBefore,
                hasPendingPublication, hasPendingRoomAdvance, roomAdvanceInFlight,
                hasPendingSessionMutation, hasPauseProvider, hasSessionPublication);

#if UNITY_EDITOR
            ApplicationBootstrapper.F10V7SessionInitializationBinding v7Binding =
                registration.V7InitializationBinding;
            if (v7Binding != null)
            {
                if (!v7Binding.IsOwnedBy(this, registration.Owner) || registration.IsDisposed)
                    return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.ContextMismatch,
                        null, null, revisionAfter, flags);

                ApplicationBootstrapper.F10V7PublishedSession publishedV7 = _f10V7PublishedSession;
                if (publishedV7 == null || !ReferenceEquals(publishedV7.Binding, v7Binding))
                    return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Unavailable,
                        null, null, revisionAfter, flags);

                bool exactV7PendingAdvance = pendingV7Advance != null &&
                    ReferenceEquals(pendingV7Advance.Binding, v7Binding) &&
                    ReferenceEquals(pendingV7Advance.Composition, registration.Owner) &&
                    ReferenceEquals(pendingV7Advance.Registration, registration) &&
                    pendingV7Advance.MutationGateRetained &&
                    ReferenceEquals(pauseOwnerBefore, registration.Owner);
                bool exactV7PendingC1Payment = pendingV7C1Payment != null &&
                    IsExactF10V7C1PaymentReadinessPublication(pendingV7C1Payment,
                        pendingV7C1Payment.ActiveOperationToken, registration,
                        pendingV7C1Payment.BinderPublication);
                bool readinessCurrent = registration.IsCurrentV7PublishedBinding(publishedV7);
                ExpeditionRunConfiguration expectedV7Run = exactV7PendingC1Payment
                    ? pendingV7C1Payment.Candidate?.CandidateRun
                    : registration.PublishedRun ?? v7Binding.Run;
                if ((!readinessCurrent && !exactV7PendingAdvance && !exactV7PendingC1Payment) ||
                    revisionBefore != revisionAfter || !ownersStable || !sessionStable ||
                    expectedV7Run == null || !ReferenceEquals(runBefore, expectedV7Run) ||
                    !ExpeditionStatesEqual(activeBefore, publishedV7.Projection.CompositionReadOnlyView) ||
                    revisionAfter != publishedV7.Revision ||
                    !ReferenceEquals(pauseOwnerBefore, registration.Owner) || publicationOwnerBefore != null ||
                    !hasPauseProvider || hasSessionPublication || hasPendingPublication ||
                    _pendingRoomAdvance != null || IsRoomAdvanceInFlight ||
                    !exactV7PendingAdvance && !exactV7PendingC1Payment &&
                        (hasPendingRoomAdvance || roomAdvanceInFlight || hasPendingSessionMutation))
                    return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Unavailable,
                        null, null, revisionAfter, flags);

                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Active,
                    expectedV7Run, publishedV7.Projection.CompositionReadOnlyView,
                    publishedV7.Revision, flags);
            }
#endif

            if (!CanWriteProfile)
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Unavailable,
                    null, null, revisionAfter, flags);
            if (!ReferenceEquals(registration.Application, this))
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.ContextMismatch,
                    null, null, revisionAfter, flags);
            if (registration.IsDisposed)
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.SessionEnded,
                    null, null, revisionAfter, flags);
            if (runBefore == null || activeBefore == null)
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.SessionEnded,
                    null, null, revisionAfter, flags);
            if (runBefore.RunId != registration.RunId || runBefore.Route == null ||
                runBefore.Route.GenerationVersion != registration.RouteGenerationVersion ||
                !string.Equals(runBefore.ClassId, registration.ClassId, StringComparison.Ordinal) ||
                !string.Equals(runBefore.RaceId, registration.RaceId, StringComparison.Ordinal))
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.ContextMismatch,
                    runBefore, activeBefore, revisionAfter, flags);
            if (!ActiveSaveMatchesRun(activeBefore, runBefore))
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.ContextMismatch,
                    runBefore, activeBefore, revisionAfter, flags);
            if (revisionBefore != revisionAfter || !ownersStable || !sessionStable)
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Unavailable,
                    null, null, revisionAfter, flags);

            bool hasDifferentOwner = pauseOwnerBefore != null &&
                !ReferenceEquals(pauseOwnerBefore, registration.Owner) ||
                publicationOwnerBefore != null &&
                !ReferenceEquals(publicationOwnerBefore, registration.Owner);
            if (hasDifferentOwner)
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.ContextMismatch,
                    runBefore, activeBefore, revisionAfter, flags);

            bool hasExpectedOwners = ReferenceEquals(pauseOwnerBefore, registration.Owner) &&
                ReferenceEquals(publicationOwnerBefore, registration.Owner);
            if (!hasExpectedOwners || !hasPauseProvider || !hasSessionPublication)
                return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Unavailable,
                    runBefore, activeBefore, revisionAfter, flags);

            return CreateReadinessAuthority(ExpeditionReadinessAuthorityState.Active,
                runBefore, activeBefore, revisionAfter, flags);
        }

        internal ExpeditionReadinessPublicationCheck CheckConfirmedEncounterPublication(
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionReadinessPreparedPublication publication)
        {
            if (registration == null || publication == null)
                return PublicationCheck(ExpeditionReadinessPublicationStatus.StaleOrEnded, 0, null);
#if UNITY_EDITOR
            ApplicationBootstrapper.F10V7SessionInitializationBinding v7Binding =
                registration.V7InitializationBinding;
            if (v7Binding != null)
            {
                if (!v7Binding.IsOwnedBy(this, registration.Owner) || registration.IsDisposed)
                    return PublicationCheck(ExpeditionReadinessPublicationStatus.StaleOrEnded,
                        _activeExpeditionRevision, null);
                F10V7C1CombatPaymentPending pendingC1Payment = _pendingF10V7C1CombatPayment;
                if (pendingC1Payment != null &&
                    ReferenceEquals(pendingC1Payment.Registration, registration) &&
                    ReferenceEquals(pendingC1Payment.BinderPublication, publication))
                {
                    if (!IsExactF10V7C1PaymentReadinessPublication(pendingC1Payment,
                            pendingC1Payment.ActiveOperationToken, registration, publication) ||
                        pendingC1Payment.ConfirmedProjection == null)
                        return PublicationCheck(ExpeditionReadinessPublicationStatus.NotConfirmed,
                            _activeExpeditionRevision, null);
                    return PublicationCheck(ExpeditionReadinessPublicationStatus.Confirmed,
                        pendingC1Payment.ConfirmedRevision,
                        pendingC1Payment.ConfirmedProjection.CompositionReadOnlyView);
                }

                PendingF10V7TutorialAdvance pending = _pendingF10V7TutorialAdvance;
                F10V7ExpeditionReadProjection currentProjection;
                string currentJson;
                long currentRevision;
                bool hasCurrentSnapshot = TryGetCurrentV7PublishedSnapshot(v7Binding,
                    out currentProjection, out currentJson, out currentRevision);
                if (pending == null || !ReferenceEquals(pending.Binding, v7Binding) ||
                    !ReferenceEquals(pending.Registration, registration) ||
                    !ReferenceEquals(pending.Publication, publication) ||
                    !ReferenceEquals(_f10V7ReadinessReceiptPending, pending) ||
                    !ReferenceEquals(_f10V7ReadinessReceiptPublication, publication) ||
                    !_f10V7ReadinessReceiptPromoted || !pending.ReceiptRegistered || !pending.Promoted ||
                    !pending.MutationGateRetained ||
                    pending.OperationInFlight != 1 || pending.ActiveOperationToken == null ||
                    !pending.RepositoryConfirmed || pending.CommitResult == null ||
                    !pending.CommitResult.Persisted || pending.CommitResult.Pending ||
                    !string.Equals(pending.CommitResult.ConfirmedJson, pending.ConfirmedJson, StringComparison.Ordinal) ||
                    !string.Equals(pending.ConfirmedJson, _f10V7ReadinessReceiptConfirmedJson, StringComparison.Ordinal) ||
                    !hasCurrentSnapshot || pending.ExpectedExpeditionRevision == long.MaxValue ||
                    currentRevision != pending.ExpectedExpeditionRevision + 1 ||
                    !ReferenceEquals(currentProjection, _f10V7ReadinessReceiptProjection) ||
                    currentRevision != _activeExpeditionRevision ||
                    !string.Equals(currentJson, pending.ConfirmedJson, StringComparison.Ordinal) ||
                    !StatesEqual(_activeExpedition, currentProjection.CompositionReadOnlyView) ||
                    !ValidatePendingV7ConfirmedCandidate(pending, pending.CommitResult,
                        currentProjection, out _))
                    return PublicationCheck(ExpeditionReadinessPublicationStatus.NotConfirmed,
                        _activeExpeditionRevision, null);

                return PublicationCheck(ExpeditionReadinessPublicationStatus.Confirmed,
                    currentRevision, currentProjection.CompositionReadOnlyView);
            }
#endif
            if (!ReferenceEquals(registration.Application, this) ||
                !ReferenceEquals(publication.Registration, registration) || registration.IsDisposed ||
                !registration.IsCurrentActivation(publication.Activation))
                return PublicationCheck(ExpeditionReadinessPublicationStatus.StaleOrEnded,
                    _activeExpeditionRevision, null);

            ExpeditionReadinessAuthorityCapture authority = CaptureEncounterReadinessAuthority(registration);
            if (authority.State == ExpeditionReadinessAuthorityState.SessionEnded ||
                authority.State == ExpeditionReadinessAuthorityState.ContextMismatch)
                return PublicationCheck(ExpeditionReadinessPublicationStatus.StaleOrEnded,
                    authority.ActiveExpeditionRevision, null);
            if (authority.State != ExpeditionReadinessAuthorityState.Active)
                return PublicationCheck(ExpeditionReadinessPublicationStatus.NotConfirmed,
                    authority.ActiveExpeditionRevision, null);

            long revisionBefore = _activeExpeditionRevision;
            ExpeditionSaveData confirmedReference = _activeExpedition;
            ExpeditionSaveData confirmed = confirmedReference == null ? null : confirmedReference.Clone();
            ExpeditionRunConfiguration activeRun = ActiveRun;
            object pauseOwner = _expeditionSimulationPauseLeaseProviderOwner;
            object publicationOwner = _expeditionSessionMutationPublicationOwner;
            bool delegatesCurrent = _expeditionSimulationPauseLeaseProvider != null &&
                _expeditionSessionStateCapture != null && _expeditionSessionMutationPreparer != null;
            long revisionAfter = _activeExpeditionRevision;

            if (revisionBefore != revisionAfter || !ReferenceEquals(confirmedReference, _activeExpedition) ||
                !ReferenceEquals(activeRun, ActiveRun) ||
                !ReferenceEquals(pauseOwner, _expeditionSimulationPauseLeaseProviderOwner) ||
                !ReferenceEquals(publicationOwner, _expeditionSessionMutationPublicationOwner))
                return PublicationCheck(ExpeditionReadinessPublicationStatus.NotConfirmed,
                    revisionAfter, null);
            if (activeRun == null || activeRun.RunId != publication.Run.RunId ||
                !ReferenceEquals(activeRun, publication.Run) ||
                !ReferenceEquals(pauseOwner, registration.Owner) ||
                !ReferenceEquals(publicationOwner, registration.Owner) || !delegatesCurrent)
                return PublicationCheck(ExpeditionReadinessPublicationStatus.StaleOrEnded,
                    revisionAfter, null);
            if (confirmed == null || publication.CandidateSave == null ||
                revisionAfter <= publication.PreparedAtRevision ||
                !ExpeditionStatesEqual(confirmed, publication.CandidateSave))
                return PublicationCheck(ExpeditionReadinessPublicationStatus.NotConfirmed,
                    revisionAfter, null);

            return PublicationCheck(ExpeditionReadinessPublicationStatus.Confirmed,
                revisionAfter, confirmed);
        }

        private ExpeditionReadinessAuthorityCapture CreateReadinessAuthority(
            ExpeditionReadinessAuthorityState state, ExpeditionRunConfiguration run,
            ExpeditionSaveData expedition, long revision, AuthorityFlags flags)
        {
            return new ExpeditionReadinessAuthorityCapture(state, run, expedition, revision,
                flags.PauseOwner, flags.PublicationOwner, flags.HasPendingPublication,
                flags.HasPendingRoomAdvance, flags.IsRoomAdvanceInFlight,
                flags.HasPendingSessionMutation, flags.HasPauseProvider, flags.HasSessionPublication);
        }

        private static ExpeditionReadinessPublicationCheck PublicationCheck(
            ExpeditionReadinessPublicationStatus status, long revision, ExpeditionSaveData expedition)
        {
            return new ExpeditionReadinessPublicationCheck(status, revision, expedition);
        }

        private static bool ActiveSaveMatchesRun(ExpeditionSaveData expedition,
            ExpeditionRunConfiguration run)
        {
            if (expedition == null || run == null || run.Route == null ||
                !string.Equals(expedition.runId, run.RunId.ToString(), StringComparison.Ordinal) ||
                expedition.seed != run.Seed || expedition.generationVersion != run.Route.GenerationVersion ||
                !string.Equals(expedition.classId, run.ClassId, StringComparison.Ordinal) ||
                !string.Equals(expedition.raceId, run.RaceId, StringComparison.Ordinal))
                return false;
            foreach (ExpeditionRouteNode node in run.Route.Nodes)
            {
                if (string.Equals(node.NodeId, expedition.currentNodeId, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private readonly struct AuthorityFlags
        {
            internal AuthorityFlags(object pauseOwner, object publicationOwner,
                bool hasPendingPublication, bool hasPendingRoomAdvance,
                bool isRoomAdvanceInFlight, bool hasPendingSessionMutation,
                bool hasPauseProvider, bool hasSessionPublication)
            {
                PauseOwner = pauseOwner;
                PublicationOwner = publicationOwner;
                HasPendingPublication = hasPendingPublication;
                HasPendingRoomAdvance = hasPendingRoomAdvance;
                IsRoomAdvanceInFlight = isRoomAdvanceInFlight;
                HasPendingSessionMutation = hasPendingSessionMutation;
                HasPauseProvider = hasPauseProvider;
                HasSessionPublication = hasSessionPublication;
            }

            internal object PauseOwner { get; }
            internal object PublicationOwner { get; }
            internal bool HasPendingPublication { get; }
            internal bool HasPendingRoomAdvance { get; }
            internal bool IsRoomAdvanceInFlight { get; }
            internal bool HasPendingSessionMutation { get; }
            internal bool HasPauseProvider { get; }
            internal bool HasSessionPublication { get; }
        }
    }
}
