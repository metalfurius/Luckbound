using System;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Saves;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;

namespace Luckbound.App
{
    internal enum ExpeditionReadinessOwnerAvailability
    {
        Active,
        Disabled,
        Destroyed
    }

    internal sealed class ExpeditionEncounterSessionSnapshotSource : IRoomEncounterRuntimeSessionSnapshotSource
    {
        private readonly ExpeditionSessionReadinessRegistration _registration;
        private readonly RoomEncounterRuntimeContext _context;
        private readonly long _activationGeneration;

        internal ExpeditionEncounterSessionSnapshotSource(
            ExpeditionSessionReadinessRegistration registration,
            RoomEncounterRuntimeContext context)
        {
            _registration = registration ?? throw new ArgumentNullException(nameof(registration));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _activationGeneration = registration.ValidateSnapshotSourceConstruction(context);
        }

        public bool TryCapture(RoomEncounterRuntimeContext context,
            out RoomEncounterRuntimeSessionSnapshot snapshot)
        {
            if (!ReferenceEquals(context, _context) || context == null ||
                context.ActivationGeneration != _activationGeneration)
            {
                snapshot = CreateCapturedIdentitySnapshot(isSessionActive: false,
                    hasBoundPlayer: false, hasCheckpointBinding: false, hasPublishedSession: false,
                    hasRequiredGameplayBindings: false, roomCommitAndActivationComplete: false,
                    hasPendingSessionMutation: true, isRoomAdvanceInFlight: false,
                    hasPendingCommitOrActivation: true, hasPendingSimulationLeaseOrGate: true,
                    isSimulationPaused: true);
                return true;
            }

            ExpeditionReadinessOwnerAvailability ownerAvailability = GetOwnerAvailability(_registration.Owner);
            if (ownerAvailability == ExpeditionReadinessOwnerAvailability.Disabled)
            {
                // A disabled owner temporarily closes readiness without ending its session or activation.
                snapshot = default;
                return false;
            }

            if (ownerAvailability == ExpeditionReadinessOwnerAvailability.Destroyed)
            {
                snapshot = CreateCapturedIdentitySnapshot(isSessionActive: false,
                    hasBoundPlayer: false, hasCheckpointBinding: false, hasPublishedSession: false,
                    hasRequiredGameplayBindings: false, roomCommitAndActivationComplete: false,
                    hasPendingSessionMutation: true, isRoomAdvanceInFlight: false,
                    hasPendingCommitOrActivation: true, hasPendingSimulationLeaseOrGate: true,
                    isSimulationPaused: true);
                return true;
            }

            if (!_registration.IsBoundSource(this, _context, _activationGeneration))
            {
                snapshot = CreateCapturedIdentitySnapshot(isSessionActive: false,
                    hasBoundPlayer: false, hasCheckpointBinding: false, hasPublishedSession: false,
                    hasRequiredGameplayBindings: false, roomCommitAndActivationComplete: false,
                    hasPendingSessionMutation: true, isRoomAdvanceInFlight: false,
                    hasPendingCommitOrActivation: true, hasPendingSimulationLeaseOrGate: true,
                    isSimulationPaused: true);
                return true;
            }

            ExpeditionReadinessCompositionView composition = _registration.Owner.CaptureReadinessCompositionView();
            if (composition == null || !ReferenceEquals(composition.Owner, _registration.Owner) ||
                !ReferenceEquals(composition.Application, _registration.Application) ||
                !ReferenceEquals(composition.ReadinessRegistration, _registration))
            {
                snapshot = CreateCapturedIdentitySnapshot(isSessionActive: false,
                    hasBoundPlayer: false, hasCheckpointBinding: false, hasPublishedSession: false,
                    hasRequiredGameplayBindings: false, roomCommitAndActivationComplete: false,
                    hasPendingSessionMutation: true, isRoomAdvanceInFlight: false,
                    hasPendingCommitOrActivation: true, hasPendingSimulationLeaseOrGate: true,
                    isSimulationPaused: true);
                return true;
            }

            ExpeditionReadinessTransitionView transition = composition.RoomTransitions == null
                ? null
                : composition.RoomTransitions.CaptureReadinessTransitionView();
            ExpeditionReadinessAuthorityCapture authority = _registration.Application
                .CaptureEncounterReadinessAuthority(_registration);
            if (authority.State == ExpeditionReadinessAuthorityState.Active)
                _registration.RecordActiveAuthorityObserved();
            else if (authority.State == ExpeditionReadinessAuthorityState.Unavailable &&
                !_registration.HasObservedActiveAuthority && !composition.IsReady)
            {
                snapshot = default;
                return false;
            }

            ExpeditionRoomActivationRegistration activation = _registration.CurrentActivation;
            PlayerMotor2D player = composition.PlayerMotor;
            bool contextIdentityMatches = authority.ActiveRun != null && authority.ActiveExpedition != null &&
                authority.ActiveRun.RunId == _context.RunId &&
                authority.ActiveRun.Route != null &&
                authority.ActiveRun.Route.GenerationVersion == _context.RouteGenerationVersion &&
                string.Equals(authority.ActiveExpedition.currentNodeId, _context.NodeId, StringComparison.Ordinal) &&
                string.Equals(authority.ActiveExpedition.runId, _context.RunId.ToString(), StringComparison.Ordinal) &&
                transition != null && ReferenceEquals(transition.Controller, _context.Controller) &&
                ReferenceEquals(transition.ActiveScope, _context.Scope) &&
                ReferenceEquals(transition.ActiveRoomRoot, _context.Scope.gameObject) &&
                string.Equals(transition.ActiveNodeId, _context.NodeId, StringComparison.Ordinal) &&
                string.Equals(transition.ActiveTemplateId, _context.TemplateId, StringComparison.Ordinal) &&
                string.Equals(transition.ActiveRoomInstanceId, _context.RoomInstanceId.ToString(), StringComparison.Ordinal) &&
                ReferenceEquals(composition.RoomTransitions, _context.Controller) &&
                ReferenceEquals(player, _context.Player) &&
                activation != null && activation.MatchesContext(_context);

            bool authorityEnded = authority.State == ExpeditionReadinessAuthorityState.SessionEnded ||
                authority.State == ExpeditionReadinessAuthorityState.ContextMismatch;
            bool authorityUnavailableAfterActivation = authority.State == ExpeditionReadinessAuthorityState.Unavailable &&
                (_registration.HasObservedActiveAuthority || composition.IsReady);
            bool activationEnded = activation == null ||
                activation.State == ExpeditionRoomActivationState.Faulted ||
                activation.State == ExpeditionRoomActivationState.Disposed;
            bool isSessionActive = !authorityEnded && !authorityUnavailableAfterActivation &&
                !activationEnded && contextIdentityMatches;

            ExpeditionCheckpointBindingWitness checkpoint = _registration.CheckpointBinding;
            bool hasCheckpointBinding = contextIdentityMatches &&
                _registration.IsCheckpointWitnessCurrent(checkpoint, composition, authority);
            bool hasBoundPlayer = hasCheckpointBinding && player != null && player.isActiveAndEnabled &&
                player.ActorId.IsValid && player.CombatStats != null &&
                ReferenceEquals(activation.PlayerMotor, player) &&
                ReferenceEquals(activation.ItemCollector, composition.ItemCollector);

            bool isSimulationPaused = composition.PauseController == null ||
                composition.PauseController.IsSimulationPaused;
            bool hasPendingPublication = authority.HasPendingPublication;
            bool hasPendingRoomAdvance = authority.HasPendingRoomAdvance;
            bool isRoomAdvanceInFlight = authority.IsRoomAdvanceInFlight;
            bool hasPendingSessionMutation = authority.HasPendingSessionMutation;
            bool hasPendingCommitOrActivation = hasPendingPublication || hasPendingRoomAdvance ||
                isRoomAdvanceInFlight || transition == null || transition.IsTransitioning ||
                transition.HasPreparedTransition;
            bool hasPendingSimulationLeaseOrGate = hasPendingSessionMutation || isSimulationPaused;

            bool transitionReady = transition != null && transition.IsInitialized && !transition.IsDisposed &&
                ReferenceEquals(transition.ActiveScope, _context.Scope) &&
                ReferenceEquals(transition.ActiveRoomRoot, _context.Scope.gameObject) &&
                !transition.IsTransitioning && !transition.HasPreparedTransition;
            bool hasRequiredGameplayBindings = contextIdentityMatches && activation != null &&
                activation.State == ExpeditionRoomActivationState.Bound &&
                HasLiveRoomBindings(activation, composition, _context, authority.ActiveRun);
            bool roomCommitAndActivationComplete = transitionReady && hasRequiredGameplayBindings &&
                !hasPendingCommitOrActivation && !hasPendingSessionMutation;

            bool hasConfirmedPublication = _registration.IsConfirmedPublicationCurrent(
                _registration.ConfirmedPublication, composition, authority);
            bool restoredSessionIsPublished = _registration.ConfirmedPublication == null &&
                hasCheckpointBinding && authority.State == ExpeditionReadinessAuthorityState.Active &&
                _registration.PublishedRun != null && ReferenceEquals(_registration.PublishedRun, authority.ActiveRun) &&
                MatchesRestoredSessionCheckpoint(checkpoint?.Revision, checkpoint?.Checkpoint,
                    authority.ActiveExpeditionRevision, authority.ActiveExpedition);
            bool hasPublishedSession = composition.IsReady && contextIdentityMatches &&
                ReferenceEquals(composition.Run, authority.ActiveRun) &&
                (hasConfirmedPublication || restoredSessionIsPublished);

            snapshot = new RoomEncounterRuntimeSessionSnapshot(
                authority.ActiveRun == null ? _context.RunId : authority.ActiveRun.RunId,
                authority.ActiveExpedition == null ? _context.NodeId : authority.ActiveExpedition.currentNodeId,
                transition == null || string.IsNullOrEmpty(transition.ActiveTemplateId)
                    ? _context.TemplateId
                    : transition.ActiveTemplateId,
                authority.ActiveRun?.Route == null
                    ? _context.RouteGenerationVersion
                    : authority.ActiveRun.Route.GenerationVersion,
                transition?.ActiveScope == null ? _context.RoomInstanceId : transition.ActiveScope.RoomInstanceId,
                transition?.ActiveScope ?? _context.Scope,
                transition?.Controller ?? _context.Controller,
                player ?? _context.Player,
                _activationGeneration,
                isSessionActive,
                hasBoundPlayer,
                hasCheckpointBinding,
                hasPublishedSession,
                hasRequiredGameplayBindings,
                roomCommitAndActivationComplete,
                hasPendingSessionMutation,
                isRoomAdvanceInFlight,
                hasPendingCommitOrActivation,
                hasPendingSimulationLeaseOrGate,
                isSimulationPaused);
            return true;
        }

        private bool HasLiveRoomBindings(ExpeditionRoomActivationRegistration activation,
            ExpeditionReadinessCompositionView composition, RoomEncounterRuntimeContext context,
            ExpeditionRunConfiguration activeRun)
        {
            ExpeditionReadinessRoomBindingResult services = activation.Services;
            if (services == null || !ReferenceEquals(services.Application, _registration.Application) ||
                !ReferenceEquals(services.Controller, context.Controller) ||
                !ReferenceEquals(services.Scope, context.Scope) ||
                !ReferenceEquals(services.CameraRig, composition.CameraRig) || services.CameraBounds == null ||
                !IsDescendantOf(services.CameraBounds.transform, context.Scope.transform) ||
                !ReferenceEquals(services.PlayerMotor, composition.PlayerMotor) ||
                !ReferenceEquals(services.ItemCollector, composition.ItemCollector) ||
                activation.BoundRun == null || !ReferenceEquals(activation.BoundRun, services.Run) ||
                !ReferenceEquals(activation.PlayerMotor, composition.PlayerMotor) ||
                !ReferenceEquals(activation.ItemCollector, composition.ItemCollector) ||
                composition.PlayerObject == null || composition.PlayerMotor == null ||
                composition.ItemCollector == null || composition.InputSource == null ||
                composition.PauseController == null || composition.ItemCatalog == null ||
                composition.WeaponCatalog == null || composition.ArmorCatalog == null ||
                composition.SkillCatalog == null || activeRun == null ||
                activeRun.RunId != context.RunId || activeRun.Route == null ||
                activeRun.Route.GenerationVersion != context.RouteGenerationVersion ||
                !ReferenceEquals(composition.ItemCollector.Inventory, activeRun.Inventory) ||
                !ReferenceEquals(activeRun.Inventory.Catalog, composition.ItemCatalog) ||
                !composition.PlayerMotor.isActiveAndEnabled ||
                !composition.ItemCollector.isActiveAndEnabled ||
                !ReferenceEquals(composition.PlayerMotor.gameObject, composition.PlayerObject) ||
                !ReferenceEquals(composition.ItemCollector.gameObject, composition.PlayerObject))
                return false;

            if (services.Run.ClassId == "class.bellator")
                return composition.PlayerMotor.IsBellator && activation.ProjectilePool == null &&
                    activation.ProjectileScope == null;
            if (services.Run.ClassId == "class.venator")
                return composition.PlayerMotor.IsVenator && HasRoomProjectileBindings(activation, context.Scope);
            if (services.Run.ClassId == "class.magus")
                return composition.PlayerMotor.IsMagus && HasRoomProjectileBindings(activation, context.Scope);
            return false;
        }

        private static bool HasRoomProjectileBindings(ExpeditionRoomActivationRegistration activation,
            RoomLifetimeScope scope)
        {
            ProjectilePool2D pool = activation.ProjectilePool;
            RoomProjectileScope projectileScope = activation.ProjectileScope;
            return pool != null && pool.IsInitialized && projectileScope != null &&
                !projectileScope.IsDisposed && ReferenceEquals(projectileScope.Pool, pool) &&
                IsDescendantOf(pool.transform, scope.transform) &&
                IsDescendantOf(projectileScope.transform, scope.transform);
        }

        private RoomEncounterRuntimeSessionSnapshot CreateCapturedIdentitySnapshot(
            bool isSessionActive, bool hasBoundPlayer, bool hasCheckpointBinding,
            bool hasPublishedSession, bool hasRequiredGameplayBindings,
            bool roomCommitAndActivationComplete, bool hasPendingSessionMutation,
            bool isRoomAdvanceInFlight, bool hasPendingCommitOrActivation,
            bool hasPendingSimulationLeaseOrGate, bool isSimulationPaused)
        {
            return new RoomEncounterRuntimeSessionSnapshot(
                _context.RunId, _context.NodeId, _context.TemplateId,
                _context.RouteGenerationVersion, _context.RoomInstanceId,
                _context.Scope, _context.Controller, _context.Player,
                _activationGeneration, isSessionActive, hasBoundPlayer,
                hasCheckpointBinding, hasPublishedSession, hasRequiredGameplayBindings,
                roomCommitAndActivationComplete, hasPendingSessionMutation,
                isRoomAdvanceInFlight, hasPendingCommitOrActivation,
                hasPendingSimulationLeaseOrGate, isSimulationPaused);
        }

        private static bool IsDescendantOf(UnityEngine.Transform candidate, UnityEngine.Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }

        internal static ExpeditionReadinessOwnerAvailability GetOwnerAvailability(
            ExpeditionGameplayComposition owner)
        {
            if (owner == null)
                return ExpeditionReadinessOwnerAvailability.Destroyed;
            return owner.isActiveAndEnabled
                ? ExpeditionReadinessOwnerAvailability.Active
                : ExpeditionReadinessOwnerAvailability.Disabled;
        }

        internal static bool MatchesRestoredSessionCheckpoint(long? checkpointRevision,
            ExpeditionSaveData checkpoint, long activeRevision, ExpeditionSaveData activeExpedition)
        {
            return checkpointRevision.HasValue && checkpointRevision.Value == activeRevision &&
                checkpoint != null && activeExpedition != null &&
                string.Equals(UnityEngine.JsonUtility.ToJson(checkpoint),
                    UnityEngine.JsonUtility.ToJson(activeExpedition), StringComparison.Ordinal);
        }
    }
}
