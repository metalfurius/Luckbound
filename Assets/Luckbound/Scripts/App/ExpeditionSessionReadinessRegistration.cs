using System;
using System.Linq;
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
    internal sealed class ExpeditionCheckpointBindingArguments
    {
        private readonly ExpeditionSaveData _checkpoint;

        internal ExpeditionCheckpointBindingArguments(
            ExpeditionRunConfiguration run, ExpeditionSaveData checkpoint,
            ActorId actorId, IPlayerInputSource inputSource,
            LayerMask groundLayers, LayerMask enemyLayers, int defense,
            PlayerMotor2D playerMotor, ItemCollector2D itemCollector,
            ItemCatalog itemCatalog, WeaponCatalog weaponCatalog, SkillCatalog skillCatalog,
            ProjectilePool2D projectilePool = null, RoomProjectileScope projectileScope = null,
            RoomLifetimeScope roomLifetimeScope = null,
            LootPickupDropStager2D dropStager = null,
            IPlayerFortuneBrewRuntime fortuneBrewRuntime = null, ArmorCatalog armorCatalog = null)
        {
            Run = run;
            _checkpoint = checkpoint == null ? null : checkpoint.Clone();
            ActorId = actorId;
            InputSource = inputSource;
            GroundLayers = groundLayers;
            EnemyLayers = enemyLayers;
            Defense = defense;
            PlayerMotor = playerMotor;
            ItemCollector = itemCollector;
            ItemCatalog = itemCatalog;
            WeaponCatalog = weaponCatalog;
            SkillCatalog = skillCatalog;
            ProjectilePool = projectilePool;
            ProjectileScope = projectileScope;
            RoomLifetimeScope = roomLifetimeScope;
            DropStager = dropStager;
            FortuneBrewRuntime = fortuneBrewRuntime;
            ArmorCatalog = armorCatalog;
        }

        internal ExpeditionRunConfiguration Run { get; }
        internal ExpeditionSaveData Checkpoint => _checkpoint == null ? null : _checkpoint.Clone();
        internal ActorId ActorId { get; }
        internal IPlayerInputSource InputSource { get; }
        internal LayerMask GroundLayers { get; }
        internal LayerMask EnemyLayers { get; }
        internal int Defense { get; }
        internal PlayerMotor2D PlayerMotor { get; }
        internal ItemCollector2D ItemCollector { get; }
        internal ItemCatalog ItemCatalog { get; }
        internal WeaponCatalog WeaponCatalog { get; }
        internal SkillCatalog SkillCatalog { get; }
        internal ProjectilePool2D ProjectilePool { get; }
        internal RoomProjectileScope ProjectileScope { get; }
        internal RoomLifetimeScope RoomLifetimeScope { get; }
        internal LootPickupDropStager2D DropStager { get; }
        internal IPlayerFortuneBrewRuntime FortuneBrewRuntime { get; }
        internal ArmorCatalog ArmorCatalog { get; }
    }

    internal sealed class ExpeditionReadinessRoomBindingResult
    {
        internal ExpeditionReadinessRoomBindingResult(
            ApplicationBootstrapper application,
            ExpeditionRoomTransitionController controller,
            RoomLifetimeScope scope,
            RoomCameraRig2D cameraRig,
            Collider2D cameraBounds,
            PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector,
            ExpeditionRunConfiguration run,
            ProjectilePool2D projectilePool,
            RoomProjectileScope projectileScope)
        {
            Application = application ?? throw new ArgumentNullException(nameof(application));
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            Scope = scope ?? throw new ArgumentNullException(nameof(scope));
            CameraRig = cameraRig ?? throw new ArgumentNullException(nameof(cameraRig));
            CameraBounds = cameraBounds ?? throw new ArgumentNullException(nameof(cameraBounds));
            PlayerMotor = playerMotor ?? throw new ArgumentNullException(nameof(playerMotor));
            ItemCollector = itemCollector ?? throw new ArgumentNullException(nameof(itemCollector));
            Run = run ?? throw new ArgumentNullException(nameof(run));
            ProjectilePool = projectilePool;
            ProjectileScope = projectileScope;
        }

        internal ApplicationBootstrapper Application { get; }
        internal ExpeditionRoomTransitionController Controller { get; }
        internal RoomLifetimeScope Scope { get; }
        internal RoomCameraRig2D CameraRig { get; }
        internal Collider2D CameraBounds { get; }
        internal PlayerMotor2D PlayerMotor { get; }
        internal ItemCollector2D ItemCollector { get; }
        internal ExpeditionRunConfiguration Run { get; }
        internal ProjectilePool2D ProjectilePool { get; }
        internal RoomProjectileScope ProjectileScope { get; }
    }

    internal enum ExpeditionRoomActivationState
    {
        AwaitingCheckpoint,
        Bound,
        Faulted,
        Disposed
    }

    internal sealed class ExpeditionRoomActivationRegistration : IDisposable
    {
        private readonly ExpeditionSessionReadinessRegistration _session;
        private readonly RoomLifetimeScope _scope;
        private readonly ExpeditionRoomTransitionController _controller;
        private readonly RunId _runId;
        private readonly string _nodeId;
        private readonly string _templateId;
        private readonly RoomInstanceId _roomInstanceId;
        private ExpeditionReadinessRoomBindingResult _services;
        private ExpeditionRunConfiguration _boundRun;
        private PlayerMotor2D _playerMotor;
        private ItemCollector2D _itemCollector;
        private ProjectilePool2D _projectilePool;
        private RoomProjectileScope _projectileScope;
        private RoomEncounterRuntimeContext _context;
        private ExpeditionEncounterSessionSnapshotSource _source;
        private ExpeditionRoomActivationState _state = ExpeditionRoomActivationState.AwaitingCheckpoint;

        private ExpeditionRoomActivationRegistration(ExpeditionSessionReadinessRegistration session,
            RoomLifetimeScope scope, ExpeditionRoomTransitionController controller, RunId runId,
            string nodeId, string templateId, RoomInstanceId roomInstanceId, object issuerToken)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            if (!session.IsActivationCreationToken(issuerToken))
                throw new InvalidOperationException("Room activation registrations can only be issued by their owning session registration.");
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            if (!runId.IsValid) throw new ArgumentException("An activation requires a valid run ID.", nameof(runId));
            if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("An activation requires a route node.", nameof(nodeId));
            if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("An activation requires a template.", nameof(templateId));
            if (!roomInstanceId.IsValid) throw new ArgumentException("An activation requires a room instance ID.", nameof(roomInstanceId));
            _runId = runId;
            _nodeId = nodeId;
            _templateId = templateId;
            _roomInstanceId = roomInstanceId;
        }

        internal static ExpeditionRoomActivationRegistration CreateFromSession(
            ExpeditionSessionReadinessRegistration session, RoomLifetimeScope scope,
            ExpeditionRoomTransitionController controller, RunId runId, string nodeId,
            string templateId, RoomInstanceId roomInstanceId, object issuerToken)
        {
            return new ExpeditionRoomActivationRegistration(session, scope, controller, runId,
                nodeId, templateId, roomInstanceId, issuerToken);
        }

        internal RoomLifetimeScope Scope => _scope;
        internal ExpeditionRoomTransitionController Controller => _controller;
        internal RunId RunId => _runId;
        internal string NodeId => _nodeId;
        internal string TemplateId => _templateId;
        internal RoomInstanceId RoomInstanceId => _roomInstanceId;
        internal ExpeditionRoomActivationState State => _state;
        internal ExpeditionReadinessRoomBindingResult Services => _services;
        internal ExpeditionRunConfiguration BoundRun => _boundRun;
        internal PlayerMotor2D PlayerMotor => _playerMotor;
        internal ItemCollector2D ItemCollector => _itemCollector;
        internal ProjectilePool2D ProjectilePool => _projectilePool;
        internal RoomProjectileScope ProjectileScope => _projectileScope;
        internal RoomEncounterRuntimeContext Context => _context;
        internal ExpeditionEncounterSessionSnapshotSource Source => _source;
        internal bool IsDisposed => _state == ExpeditionRoomActivationState.Disposed;

        internal bool MatchesContext(RoomEncounterRuntimeContext context)
        {
            return _session.IsOwnerActive && context != null && ReferenceEquals(context.Scope, _scope) &&
                ReferenceEquals(context.Controller, _controller) && context.RunId == _runId &&
                string.Equals(context.NodeId, _nodeId, StringComparison.Ordinal) &&
                string.Equals(context.TemplateId, _templateId, StringComparison.Ordinal) &&
                context.RoomInstanceId == _roomInstanceId;
        }

        internal void RecordRoomServices(ExpeditionReadinessRoomBindingResult result)
        {
            EnsureLive();
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (_services != null) throw new InvalidOperationException("Room services were already recorded for this activation.");
            if (!ReferenceEquals(result.Scope, _scope) || !ReferenceEquals(result.Controller, _controller) ||
                result.Run == null || result.Run.RunId != _runId ||
                result.Run.Route.GenerationVersion != _session.RouteGenerationVersion ||
                result.PlayerMotor == null || result.ItemCollector == null)
                throw new InvalidOperationException("Room service operations returned a different activation identity.");

            _services = result;
            if (_session.CheckpointBinding == null)
                AwaitCheckpoint();
        }

        internal void AcceptInitialCheckpointBinding(ExpeditionCheckpointBindingArguments arguments)
        {
            EnsureLive();
            if (_services == null)
                throw new InvalidOperationException("The initial room services must finish before checkpoint binding is recorded.");
            if (!ReferenceEquals(arguments.RoomLifetimeScope, _scope) && RequiresProjectiles(arguments.Run))
                throw new InvalidOperationException("The initial ranged class binding must use the active room scope.");
            AcceptClassBinding(arguments.Run, arguments.PlayerMotor, arguments.ItemCollector,
                arguments.ProjectilePool, arguments.ProjectileScope);
        }

        internal void AwaitCheckpoint()
        {
            EnsureLive();
            if (_session.CheckpointBinding != null)
                throw new InvalidOperationException("A checkpoint-bound activation cannot return to its initial wait state.");
            _state = ExpeditionRoomActivationState.AwaitingCheckpoint;
        }

        internal void AcceptRoomClassBinding(ExpeditionReadinessRoomBindingResult result,
            ExpeditionRunConfiguration run)
        {
            EnsureLive();
            if (_services == null || !ReferenceEquals(_services, result) ||
                _session.CheckpointBinding == null || run == null || !ReferenceEquals(result.Run, run))
                throw new InvalidOperationException("A room class binding requires the recorded service result and checkpoint witness.");
            AcceptClassBinding(run, result.PlayerMotor, result.ItemCollector,
                result.ProjectilePool, result.ProjectileScope);
        }

        internal void AttachSourceContext(RoomEncounterRuntimeContext context,
            ExpeditionEncounterSessionSnapshotSource source)
        {
            EnsureLive();
            if (!MatchesContext(context))
                throw new InvalidOperationException("The encounter context does not belong to this exact room activation.");
            if (_source != null)
            {
                if (ReferenceEquals(_context, context) && ReferenceEquals(_source, source)) return;
                throw new InvalidOperationException("An activation can own only one encounter context and snapshot source.");
            }
            if (context.ActivationGeneration <= 0)
                throw new InvalidOperationException("The runtime context must carry its own activation generation.");
            _context = context;
            _source = source ?? throw new ArgumentNullException(nameof(source));
        }

        internal void MarkFaulted()
        {
            if (_state == ExpeditionRoomActivationState.Disposed) return;
            _state = ExpeditionRoomActivationState.Faulted;
            _source = null;
        }

        public void Dispose()
        {
            if (_state == ExpeditionRoomActivationState.Disposed) return;
            _state = ExpeditionRoomActivationState.Disposed;
            _source = null;
            _services = null;
            _boundRun = null;
            _playerMotor = null;
            _itemCollector = null;
            _projectilePool = null;
            _projectileScope = null;
        }

        private void AcceptClassBinding(ExpeditionRunConfiguration run, PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector, ProjectilePool2D projectilePool, RoomProjectileScope projectileScope)
        {
            if (run == null || run.RunId != _runId || playerMotor == null || itemCollector == null ||
                !ReferenceEquals(playerMotor, _services.PlayerMotor) || !ReferenceEquals(itemCollector, _services.ItemCollector) ||
                !playerMotor.ActorId.IsValid || playerMotor.CombatStats == null ||
                !string.Equals(playerMotor.RaceId, run.RaceId, StringComparison.Ordinal) ||
                !string.Equals(itemCollector.RaceId, run.RaceId, StringComparison.Ordinal) ||
                !ReferenceEquals(itemCollector.Inventory, run.Inventory) ||
                !ReferenceEquals(itemCollector.Catalog, run.Inventory.Catalog) ||
                !playerMotor.isActiveAndEnabled || !itemCollector.isActiveAndEnabled)
                throw new InvalidOperationException("The active player and collector do not match the bound run.");

            bool classMatches = run.ClassId == "class.bellator" && playerMotor.IsBellator && !playerMotor.IsVenator && !playerMotor.IsMagus ||
                run.ClassId == "class.venator" && playerMotor.IsVenator && !playerMotor.IsBellator && !playerMotor.IsMagus ||
                run.ClassId == "class.magus" && playerMotor.IsMagus && !playerMotor.IsBellator && !playerMotor.IsVenator;
            if (!classMatches)
                throw new InvalidOperationException("The live player class does not match the bound run.");

            if (RequiresProjectiles(run))
            {
                if (projectilePool == null || !projectilePool.IsInitialized || projectileScope == null ||
                    projectileScope.IsDisposed || !ReferenceEquals(projectileScope.Pool, projectilePool) ||
                    !IsDescendantOf(projectilePool.transform, _scope.transform) ||
                    !IsDescendantOf(projectileScope.transform, _scope.transform))
                    throw new InvalidOperationException("A ranged class requires its real pool and projectile scope in the active room.");
            }
            else if (projectilePool != null || projectileScope != null)
            {
                throw new InvalidOperationException("Bellator does not require or retain a projectile binding.");
            }

            _boundRun = run;
            _playerMotor = playerMotor;
            _itemCollector = itemCollector;
            _projectilePool = projectilePool;
            _projectileScope = projectileScope;
            _state = ExpeditionRoomActivationState.Bound;
        }

        private void EnsureLive()
        {
            if (!_session.IsOwnerActive)
                throw new InvalidOperationException("The composition owner is disabled or has been destroyed.");
            if (IsDisposed || _state == ExpeditionRoomActivationState.Faulted || _scope == null ||
                _scope.IsDisposed || !_scope.isActiveAndEnabled)
                throw new ObjectDisposedException(nameof(ExpeditionRoomActivationRegistration));
        }

        private static bool RequiresProjectiles(ExpeditionRunConfiguration run)
        {
            return run != null && (run.ClassId == "class.venator" || run.ClassId == "class.magus");
        }

        private static bool IsDescendantOf(Transform candidate, Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }
    }

    internal sealed class ExpeditionSessionReadinessRegistration : IDisposable
    {
        private readonly object _activationCreationToken = new object();
        private readonly object _preparedPublicationCreationToken = new object();
        private readonly ApplicationBootstrapper _application;
        private readonly ExpeditionGameplayComposition _owner;
        private readonly RunId _runId;
        private readonly int _routeGenerationVersion;
        private readonly string _classId;
        private readonly string _raceId;
        private ExpeditionRoomActivationRegistration _currentActivation;
        private ExpeditionCheckpointBindingWitness _checkpointBinding;
        private ExpeditionConfirmedPublicationWitness _confirmedPublication;
        private ExpeditionRunConfiguration _publishedRun;
        private ExpeditionSaveData _checkpointBindingState;
        private long _checkpointBindingRevision;
        private bool _checkpointBindAttempted;
        private bool _checkpointBinderReturned;
        private bool _checkpointBindingValidated;
        private bool _disposed;
        private bool _sourceConstructionInProgress;
        private bool _preparedPublicationConstructionInProgress;
        private bool _hasObservedActiveAuthority;

        private ExpeditionSessionReadinessRegistration(ApplicationBootstrapper application,
            ExpeditionGameplayComposition owner, ExpeditionRunConfiguration run)
        {
            _application = application;
            _owner = owner;
            _runId = run.RunId;
            _routeGenerationVersion = run.Route.GenerationVersion;
            _classId = run.ClassId;
            _raceId = run.RaceId;
        }

        internal static ExpeditionSessionReadinessRegistration Create(
            ApplicationBootstrapper application,
            ExpeditionGameplayComposition owner)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (!owner.isActiveAndEnabled)
                throw new InvalidOperationException("Readiness registration requires an active composition owner.");
            if (!ReferenceEquals(ApplicationBootstrapper.Instance, application))
                throw new InvalidOperationException("Readiness registration requires the active application singleton.");

            ExpeditionReadinessCompositionView view = owner.CaptureReadinessCompositionView();
            if (view == null || !ReferenceEquals(view.Owner, owner) || !ReferenceEquals(view.Application, application))
                throw new InvalidOperationException("The composition does not belong to the requested application.");
            if (view.ReadinessRegistration != null && !view.ReadinessRegistration.IsDisposed)
                throw new InvalidOperationException("The composition already owns a live readiness registration.");
            if (!owner.isActiveAndEnabled || view.Run == null || view.Run.Route == null ||
                application.ActiveRun == null || !ReferenceEquals(application.ActiveRun, view.Run))
                throw new InvalidOperationException("Readiness registration requires the composition's active committed run.");

            ExpeditionSaveData activeExpedition = application.ActiveExpedition;
            if (!SaveIdentityMatches(activeExpedition, view.Run))
                throw new InvalidOperationException("The active save and composition run do not identify the same expedition.");

            return new ExpeditionSessionReadinessRegistration(application, owner, view.Run);
        }

        internal ApplicationBootstrapper Application => _application;
        internal ExpeditionGameplayComposition Owner => _owner;
        internal ExpeditionRoomActivationRegistration CurrentActivation => _currentActivation;
        internal ExpeditionCheckpointBindingWitness CheckpointBinding => _checkpointBinding;
        internal ExpeditionConfirmedPublicationWitness ConfirmedPublication => _confirmedPublication;
        internal ExpeditionRunConfiguration PublishedRun => _publishedRun;
        internal bool IsDisposed => _disposed;
        internal bool IsOwnerActive => !_disposed && _owner != null && _owner.isActiveAndEnabled;
        internal RunId RunId => _runId;
        internal string ClassId => _classId;
        internal string RaceId => _raceId;
        internal int RouteGenerationVersion => _routeGenerationVersion;
        internal bool HasObservedActiveAuthority => _hasObservedActiveAuthority;

        internal ExpeditionRoomActivationRegistration BindActivatedRoom(RoomLifetimeScope scope)
        {
            EnsureLive();
            if (scope == null) throw new ArgumentNullException(nameof(scope));

            ExpeditionReadinessCompositionView composition = CaptureOwnerView();
            ExpeditionReadinessTransitionView transition = composition.RoomTransitions == null
                ? null
                : composition.RoomTransitions.CaptureReadinessTransitionView();
            if (transition == null || !ReferenceEquals(transition.Controller, composition.RoomTransitions) ||
                !ReferenceEquals(transition.ActiveScope, scope) || transition.ActiveRoomRoot == null ||
                !ReferenceEquals(scope, transition.ActiveScope) || scope.IsDisposed || !scope.isActiveAndEnabled ||
                !string.Equals(transition.ActiveRoomInstanceId, scope.RoomInstanceId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(transition.ActiveTemplateId, scope.TemplateId, StringComparison.Ordinal) ||
                !string.Equals(transition.ActiveNodeId, _application.ActiveExpedition?.currentNodeId, StringComparison.Ordinal) ||
                ExpeditionRoomIdentity.ForNode(_runId, transition.ActiveNodeId) != scope.RoomInstanceId)
                throw new InvalidOperationException("Only the exact active controller scope can receive a readiness registration.");

            if (_currentActivation != null && ReferenceEquals(_currentActivation.Scope, scope))
            {
                if (_currentActivation.State == ExpeditionRoomActivationState.Faulted)
                    throw new InvalidOperationException("A faulted activation cannot be rehabilitated by repeating its bind.");
                if (_currentActivation.IsDisposed)
                    throw new ObjectDisposedException(nameof(ExpeditionRoomActivationRegistration));
                return _currentActivation;
            }

            _currentActivation?.Dispose();
            ExpeditionRoomActivationRegistration activation = ExpeditionRoomActivationRegistration.CreateFromSession(
                this, scope, transition.Controller, _runId, transition.ActiveNodeId,
                transition.ActiveTemplateId, scope.RoomInstanceId, _activationCreationToken);
            _currentActivation = activation;
            try
            {
                scope.OwnResource(activation);
                ExpeditionReadinessRoomBindingResult result = _owner.BindReadinessRoomServices(scope);
                ValidateRoomBindingResult(result, composition, transition, scope);
                activation.RecordRoomServices(result);
                if (_checkpointBinding == null)
                {
                    activation.AwaitCheckpoint();
                }
                else
                {
                    ExpeditionRunConfiguration committedRun = _application.ActiveRun;
                    if (committedRun == null || !SaveIdentityMatches(_application.ActiveExpedition, committedRun) ||
                        !ReferenceEquals(result.Run, committedRun))
                        throw new InvalidOperationException("The active room binding did not synchronize the confirmed run.");
                    activation.AcceptRoomClassBinding(result, committedRun);
                    _publishedRun = committedRun;
                }
                return activation;
            }
            catch
            {
                activation.MarkFaulted();
                throw;
            }
        }

        internal ExpeditionCheckpointBindingWitness BindFromCheckpoint(
            ExpeditionCheckpointBindingArguments arguments)
        {
            EnsureLive();
            if (arguments == null) throw new ArgumentNullException(nameof(arguments));
            if (_checkpointBindAttempted)
                throw new InvalidOperationException("Checkpoint binding is a one-shot operation for this session.");
            _checkpointBindAttempted = true;
            ExpeditionRoomActivationRegistration activation = _currentActivation;
            try
            {
                ValidateCheckpointArguments(arguments, activation);
                ExpeditionSaveData checkpoint = arguments.Checkpoint;
                long revision = _application.ActiveExpeditionRevision;
                ExpeditionSaveData confirmedBefore = _application.ActiveExpedition;
                if (!ExpeditionStatesMatch(confirmedBefore, checkpoint) ||
                    !ReferenceEquals(_application.ActiveRun, arguments.Run))
                    throw new InvalidOperationException("The checkpoint is not the exact currently confirmed session state.");

                ExpeditionPlayerRunBinder.BindFromCheckpoint(
                    arguments.Run, checkpoint, arguments.ActorId, arguments.InputSource,
                    arguments.GroundLayers, arguments.EnemyLayers, arguments.Defense,
                    arguments.PlayerMotor, arguments.ItemCollector, arguments.ItemCatalog,
                    arguments.WeaponCatalog, arguments.SkillCatalog, arguments.ProjectilePool,
                    arguments.ProjectileScope, arguments.RoomLifetimeScope, arguments.DropStager,
                    arguments.FortuneBrewRuntime, arguments.ArmorCatalog);
                _checkpointBinderReturned = true;

                ExpeditionSaveData confirmedAfter = _application.ActiveExpedition;
                if (_application.ActiveExpeditionRevision != revision ||
                    !ExpeditionStatesMatch(confirmedBefore, confirmedAfter) ||
                    !ExpeditionStatesMatch(checkpoint, confirmedAfter) ||
                    !ActorMatchesRun(arguments.Run, arguments.ActorId, arguments.PlayerMotor, arguments.ItemCollector))
                    throw new InvalidOperationException("The binder returned but its actor or confirmed checkpoint identity changed.");

                activation.AcceptInitialCheckpointBinding(arguments);
                _checkpointBindingState = checkpoint.Clone();
                _checkpointBindingRevision = revision;
                _checkpointBindingValidated = true;
                _publishedRun = arguments.Run;
                _checkpointBinding = ExpeditionCheckpointBindingWitness.CreateAfterSuccessfulBinder(this);
                return _checkpointBinding;
            }
            catch
            {
                activation?.MarkFaulted();
                _checkpointBinding = null;
                _checkpointBindingValidated = false;
                _checkpointBindingState = null;
                _publishedRun = null;
                throw;
            }
        }

        internal ExpeditionReadinessPreparedPublication PrepareCommittedPublication(
            ExpeditionRunConfiguration run,
            ExpeditionSaveData candidateSave,
            WeaponCatalog weaponCatalog,
            ArmorCatalog armorCatalog)
        {
            EnsureLive();
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (candidateSave == null) throw new ArgumentNullException(nameof(candidateSave));
            ExpeditionReadinessCompositionView view = CaptureOwnerView();
            ExpeditionRoomActivationRegistration activation = _currentActivation;
            if (activation == null || activation.State != ExpeditionRoomActivationState.Bound ||
                !view.IsReady || !ReferenceEquals(view.PlayerMotor, activation.PlayerMotor) ||
                !ReferenceEquals(view.ItemCollector, activation.ItemCollector) ||
                !ReferenceEquals(view.WeaponCatalog, weaponCatalog) || !ReferenceEquals(view.ArmorCatalog, armorCatalog) ||
                view.PlayerMotor == null || view.ItemCollector == null || _checkpointBinding == null ||
                !run.RunId.Equals(_runId) || !string.Equals(run.ClassId, _classId, StringComparison.Ordinal) ||
                !string.Equals(run.RaceId, _raceId, StringComparison.Ordinal) ||
                !ReferenceEquals(run.Inventory.Catalog, view.ItemCatalog) ||
                !ReferenceEquals(_application.ActiveRun, run) && !run.RunId.Equals(_application.ActiveRun?.RunId ?? default))
                throw new InvalidOperationException("Only the live bound session can prepare a committed publication.");

            if (!string.Equals(candidateSave.runId, _runId.ToString(), StringComparison.Ordinal) ||
                !string.Equals(candidateSave.classId, _classId, StringComparison.Ordinal) ||
                !string.Equals(candidateSave.raceId, _raceId, StringComparison.Ordinal) ||
                candidateSave.generationVersion != _routeGenerationVersion)
                throw new InvalidOperationException("The candidate save belongs to another run or route.");

            long observedRevision = _application.ActiveExpeditionRevision;
            ExpeditionPlayerRunBinder.PreparedCommittedRunPublication prepared =
                ExpeditionPlayerRunBinder.PrepareCommittedRunPublication(
                    run, candidateSave, view.PlayerMotor, view.ItemCollector, weaponCatalog, armorCatalog);
            _preparedPublicationConstructionInProgress = true;
            try
            {
                return ExpeditionReadinessPreparedPublication.CreateFromRegistration(this, activation,
                    prepared, candidateSave, observedRevision, _preparedPublicationCreationToken);
            }
            finally
            {
                _preparedPublicationConstructionInProgress = false;
            }
        }

        internal ExpeditionEncounterSessionSnapshotSource CreateSnapshotSource(RoomEncounterRuntimeContext context)
        {
            EnsureLive();
            if (context == null) throw new ArgumentNullException(nameof(context));
            ExpeditionRoomActivationRegistration activation = _currentActivation;
            if (activation == null || !activation.MatchesContext(context) || !context.IsCurrentRoomScope())
                throw new InvalidOperationException("The context must belong to the current exact room activation.");
            if (activation.Source != null)
            {
                if (ReferenceEquals(activation.Context, context) &&
                    activation.Context.ActivationGeneration == context.ActivationGeneration)
                    return activation.Source;
                throw new InvalidOperationException("A different context or epoch is already bound to this activation.");
            }

            _sourceConstructionInProgress = true;
            try
            {
                var source = new ExpeditionEncounterSessionSnapshotSource(this, context);
                activation.AttachSourceContext(context, source);
                return source;
            }
            finally
            {
                _sourceConstructionInProgress = false;
            }
        }

        internal long ValidateSnapshotSourceConstruction(RoomEncounterRuntimeContext context)
        {
            if (!IsOwnerActive || !_sourceConstructionInProgress || _disposed || context == null || _currentActivation == null ||
                !_currentActivation.MatchesContext(context) || _currentActivation.Source != null ||
                _currentActivation.Context != null)
                throw new InvalidOperationException("Snapshot sources are created only by the active registration.");
            return context.ActivationGeneration;
        }

        internal bool IsBoundSource(ExpeditionEncounterSessionSnapshotSource source,
            RoomEncounterRuntimeContext context, long activationGeneration)
        {
            ExpeditionRoomActivationRegistration activation = _currentActivation;
            return IsOwnerActive && source != null && activation != null &&
                activation.State != ExpeditionRoomActivationState.Disposed &&
                ReferenceEquals(activation.Source, source) && ReferenceEquals(activation.Context, context) &&
                activation.MatchesContext(context) && activationGeneration == context.ActivationGeneration;
        }

        internal CheckpointBindingWitnessData GetCheckpointBindingWitnessData()
        {
            EnsureLive();
            if (!_checkpointBindAttempted || !_checkpointBinderReturned || !_checkpointBindingValidated ||
                _checkpointBindingState == null || _currentActivation == null ||
                _currentActivation.State != ExpeditionRoomActivationState.Bound)
                throw new InvalidOperationException("No successful real binder operation is available for a checkpoint witness.");
            return new CheckpointBindingWitnessData(_application, _owner, _runId, _routeGenerationVersion,
                _classId, _raceId, _checkpointBindingState.Clone(), _checkpointBindingRevision,
                _currentActivation.NodeId, _currentActivation.Scope, _currentActivation.PlayerMotor,
                _currentActivation.ItemCollector, _currentActivation.PlayerMotor.ActorId);
        }

        internal ConfirmedPublicationWitnessData GetConfirmedPublicationWitnessData(
            ExpeditionReadinessPreparedPublication publication, ExpeditionReadinessPublicationCheck check)
        {
            EnsureLive();
            if (publication == null || check == null || check.Status != ExpeditionReadinessPublicationStatus.Confirmed ||
                !ReferenceEquals(publication.Registration, this) || !ReferenceEquals(publication.Activation, _currentActivation) ||
                _disposed || _currentActivation == null || _currentActivation.State != ExpeditionRoomActivationState.Bound ||
                _checkpointBinding == null || publication.Run == null ||
                !publication.Run.RunId.Equals(_runId) || check.ConfirmedExpedition == null ||
                check.ConfirmedRevision <= publication.PreparedAtRevision)
                throw new InvalidOperationException("The confirmed publication is not owned by this live bound session.");
            return new ConfirmedPublicationWitnessData(_application, _owner, _runId, _routeGenerationVersion,
                check.ConfirmedRevision, check.ConfirmedExpedition, publication.Run,
                _currentActivation.PlayerMotor, _currentActivation.ItemCollector,
                _currentActivation.Scope, _currentActivation.NodeId);
        }

        internal void RecordConfirmedPublication(ExpeditionReadinessPreparedPublication publication,
            ExpeditionReadinessPublicationCheck check)
        {
            ConfirmedPublicationWitnessData data = GetConfirmedPublicationWitnessData(publication, check);
            _confirmedPublication = ExpeditionConfirmedPublicationWitness.CreateAfterConfirmedPublish(this, publication, check);
            _publishedRun = data.Run;
        }

        internal void RecordActiveAuthorityObserved()
        {
            EnsureLive();
            _hasObservedActiveAuthority = true;
        }

        internal bool IsActivationCreationToken(object token)
        {
            return IsOwnerActive && ReferenceEquals(_activationCreationToken, token);
        }

        internal bool IsPreparedPublicationCreationToken(object token)
        {
            return IsOwnerActive && _preparedPublicationConstructionInProgress &&
                ReferenceEquals(_preparedPublicationCreationToken, token);
        }

        internal bool IsCurrentActivation(ExpeditionRoomActivationRegistration activation)
        {
            return IsOwnerActive && activation != null && ReferenceEquals(_currentActivation, activation) &&
                activation.State != ExpeditionRoomActivationState.Faulted &&
                activation.State != ExpeditionRoomActivationState.Disposed;
        }

        internal bool IsCheckpointWitnessCurrent(ExpeditionCheckpointBindingWitness witness,
            ExpeditionReadinessCompositionView composition, ExpeditionReadinessAuthorityCapture authority)
        {
            return IsOwnerActive && witness != null && !IsDisposed && composition != null && authority != null &&
                ReferenceEquals(witness.Application, _application) &&
                ReferenceEquals(witness.Owner, _owner) && witness.RunId == _runId &&
                witness.RouteGenerationVersion == _routeGenerationVersion &&
                string.Equals(witness.ClassId, _classId, StringComparison.Ordinal) &&
                string.Equals(witness.RaceId, _raceId, StringComparison.Ordinal) &&
                ReferenceEquals(witness.PlayerMotor, composition.PlayerMotor) &&
                ReferenceEquals(witness.ItemCollector, composition.ItemCollector) &&
                composition.PlayerMotor != null && composition.PlayerMotor.ActorId == witness.ActorId &&
                authority.ActiveRun != null && authority.ActiveRun.RunId == _runId &&
                authority.ActiveExpedition != null && SaveIdentityMatches(authority.ActiveExpedition, authority.ActiveRun);
        }

        internal bool IsConfirmedPublicationCurrent(ExpeditionConfirmedPublicationWitness witness,
            ExpeditionReadinessCompositionView composition, ExpeditionReadinessAuthorityCapture authority)
        {
            return IsOwnerActive && witness != null && !IsDisposed && composition != null && authority != null &&
                ReferenceEquals(witness.Application, _application) &&
                ReferenceEquals(witness.Owner, _owner) && witness.RunId == _runId &&
                witness.RouteGenerationVersion == _routeGenerationVersion &&
                ReferenceEquals(witness.PlayerMotor, composition.PlayerMotor) &&
                ReferenceEquals(witness.ItemCollector, composition.ItemCollector) &&
                authority.ActiveRun != null && authority.ActiveRun.RunId == _runId &&
                authority.ActiveExpedition != null && witness.Revision == authority.ActiveExpeditionRevision &&
                ReferenceEquals(witness.Run, authority.ActiveRun) &&
                ExpeditionStatesMatch(witness.ConfirmedExpedition, authority.ActiveExpedition);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _checkpointBinding = null;
            _confirmedPublication = null;
            _checkpointBindingState = null;
            _publishedRun = null;
            _currentActivation?.Dispose();
        }

        private void ValidateCheckpointArguments(ExpeditionCheckpointBindingArguments arguments,
            ExpeditionRoomActivationRegistration activation)
        {
            ExpeditionReadinessCompositionView view = CaptureOwnerView();
            if (activation == null || activation.State == ExpeditionRoomActivationState.Faulted ||
                activation.State == ExpeditionRoomActivationState.Disposed || activation.Services == null ||
                arguments.Run == null || arguments.Checkpoint == null ||
                arguments.Run.RunId != _runId || arguments.Run.Route.GenerationVersion != _routeGenerationVersion ||
                !string.Equals(arguments.Run.ClassId, _classId, StringComparison.Ordinal) ||
                !string.Equals(arguments.Run.RaceId, _raceId, StringComparison.Ordinal) ||
                !ReferenceEquals(arguments.Run, view.Run) || !ReferenceEquals(arguments.Run, _application.ActiveRun) ||
                !ReferenceEquals(arguments.PlayerMotor, view.PlayerMotor) ||
                !ReferenceEquals(arguments.ItemCollector, view.ItemCollector) ||
                !ReferenceEquals(arguments.InputSource, view.InputSource) ||
                !ReferenceEquals(arguments.ItemCatalog, view.ItemCatalog) ||
                !ReferenceEquals(arguments.WeaponCatalog, view.WeaponCatalog) ||
                !ReferenceEquals(arguments.SkillCatalog, view.SkillCatalog) ||
                !ReferenceEquals(arguments.ArmorCatalog, view.ArmorCatalog) ||
                arguments.GroundLayers.value != LayerMask.GetMask("Ground") ||
                arguments.EnemyLayers.value != LayerMask.GetMask("Enemies") || arguments.Defense != 0 ||
                arguments.DropStager != null ||
                !ReferenceEquals(arguments.FortuneBrewRuntime, _application as IPlayerFortuneBrewRuntime) ||
                (arguments.Run.ClassId == "class.venator" || arguments.Run.ClassId == "class.magus") &&
                    !ReferenceEquals(arguments.RoomLifetimeScope, activation.Scope) ||
                !ReferenceEquals(arguments.PlayerMotor, activation.Services.PlayerMotor) ||
                !ReferenceEquals(arguments.ItemCollector, activation.Services.ItemCollector) ||
                !arguments.ActorId.IsValid || arguments.PlayerMotor == null || arguments.ItemCollector == null ||
                arguments.PlayerMotor.ActorId.IsValid || arguments.ItemCollector.Inventory != null ||
                view.PlayerObject == null || arguments.PlayerMotor.gameObject != view.PlayerObject ||
                arguments.ItemCollector.gameObject != view.PlayerObject)
                throw new InvalidOperationException("Checkpoint binding arguments must be the live composition's exact unbound actor references.");

            ExpeditionSaveData checkpoint = arguments.Checkpoint;
            if (!SaveIdentityMatches(checkpoint, arguments.Run) ||
                !string.Equals(checkpoint.currentNodeId, activation.NodeId, StringComparison.Ordinal) ||
                arguments.PlayerMotor.gameObject != arguments.ItemCollector.gameObject ||
                arguments.ItemCatalog == null || arguments.WeaponCatalog == null || arguments.SkillCatalog == null ||
                arguments.ArmorCatalog == null)
                throw new InvalidOperationException("The initial checkpoint and actor bindings do not match the active room/run.");

            if (arguments.Run.ClassId == "class.bellator")
            {
                if (arguments.ProjectilePool != null || arguments.ProjectileScope != null)
                    throw new InvalidOperationException("Bellator checkpoint binding must not require projectile resources.");
            }
            else if (arguments.ProjectilePool == null || !arguments.ProjectilePool.IsInitialized ||
                arguments.ProjectileScope == null || arguments.ProjectileScope.IsDisposed ||
                !ReferenceEquals(arguments.ProjectileScope.Pool, arguments.ProjectilePool) ||
                !IsDescendantOf(arguments.ProjectilePool.transform, activation.Scope.transform) ||
                !IsDescendantOf(arguments.ProjectileScope.transform, activation.Scope.transform))
                throw new InvalidOperationException("The initial ranged checkpoint bind must use real resources owned by its room.");
        }

        private ExpeditionReadinessCompositionView CaptureOwnerView()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ExpeditionSessionReadinessRegistration));
            if (_owner == null)
                throw new ObjectDisposedException(nameof(ExpeditionGameplayComposition));
            if (!_owner.isActiveAndEnabled)
                throw new InvalidOperationException("The composition owner is temporarily disabled.");
            ExpeditionReadinessCompositionView view = _owner.CaptureReadinessCompositionView();
            if (view == null || !ReferenceEquals(view.Owner, _owner) || !ReferenceEquals(view.Application, _application) ||
                !ReferenceEquals(view.ReadinessRegistration, this))
                throw new InvalidOperationException("The readiness registration is no longer the composition's active owner.");
            return view;
        }

        private void EnsureLive()
        {
            CaptureOwnerView();
        }

        private void ValidateRoomBindingResult(ExpeditionReadinessRoomBindingResult result,
            ExpeditionReadinessCompositionView composition, ExpeditionReadinessTransitionView transition,
            RoomLifetimeScope scope)
        {
            if (result == null || !ReferenceEquals(result.Application, _application) ||
                !ReferenceEquals(result.Controller, transition.Controller) || !ReferenceEquals(result.Scope, scope) ||
                !ReferenceEquals(result.CameraRig, composition.CameraRig) || result.CameraBounds == null ||
                !IsDescendantOf(result.CameraBounds.transform, transition.ActiveRoomRoot.transform) ||
                !ReferenceEquals(result.PlayerMotor, composition.PlayerMotor) ||
                !ReferenceEquals(result.ItemCollector, composition.ItemCollector) || result.Run == null ||
                result.Run.RunId != _runId || result.Run.Route.GenerationVersion != _routeGenerationVersion ||
                !ReferenceEquals(result.Run, _application.ActiveRun))
                throw new InvalidOperationException("The room services did not return evidence for the exact active composition.");

            if (_checkpointBinding != null)
                ValidateClassResources(result.Run, result.PlayerMotor, result.ItemCollector,
                    result.ProjectilePool, result.ProjectileScope, scope);
        }

        private void ValidateClassResources(ExpeditionRunConfiguration run, PlayerMotor2D playerMotor,
            ItemCollector2D itemCollector, ProjectilePool2D pool, RoomProjectileScope projectileScope,
            RoomLifetimeScope roomScope)
        {
            if (run == null || playerMotor == null || itemCollector == null ||
                !ReferenceEquals(itemCollector.Inventory, run.Inventory) ||
                !string.Equals(playerMotor.RaceId, run.RaceId, StringComparison.Ordinal) ||
                !string.Equals(itemCollector.RaceId, run.RaceId, StringComparison.Ordinal))
                throw new InvalidOperationException("The room's committed inventory/run identity did not bind to the live actor.");
            if (run.ClassId == "class.bellator")
            {
                if (!playerMotor.IsBellator || pool != null || projectileScope != null)
                    throw new InvalidOperationException("Bellator bindings must be real and must not require projectiles.");
                return;
            }
            if (run.ClassId == "class.venator" && !playerMotor.IsVenator ||
                run.ClassId == "class.magus" && !playerMotor.IsMagus ||
                (run.ClassId != "class.venator" && run.ClassId != "class.magus") ||
                pool == null || !pool.IsInitialized || projectileScope == null || projectileScope.IsDisposed ||
                !ReferenceEquals(projectileScope.Pool, pool) ||
                !IsDescendantOf(pool.transform, roomScope.transform) ||
                !IsDescendantOf(projectileScope.transform, roomScope.transform))
                throw new InvalidOperationException("Ranged bindings require the exact live class and room-owned projectile resources.");
        }

        private static bool ActorMatchesRun(ExpeditionRunConfiguration run, ActorId actorId,
            PlayerMotor2D playerMotor, ItemCollector2D itemCollector)
        {
            if (run == null || playerMotor == null || itemCollector == null ||
                !playerMotor.isActiveAndEnabled || !itemCollector.isActiveAndEnabled ||
                playerMotor.ActorId != actorId || !actorId.IsValid || playerMotor.CombatStats == null ||
                !string.Equals(playerMotor.RaceId, run.RaceId, StringComparison.Ordinal) ||
                !string.Equals(itemCollector.RaceId, run.RaceId, StringComparison.Ordinal) ||
                !ReferenceEquals(itemCollector.Inventory, run.Inventory) ||
                !ReferenceEquals(itemCollector.Catalog, run.Inventory.Catalog)) return false;
            return run.ClassId == "class.bellator" && playerMotor.IsBellator && !playerMotor.IsVenator && !playerMotor.IsMagus ||
                run.ClassId == "class.venator" && playerMotor.IsVenator && !playerMotor.IsBellator && !playerMotor.IsMagus ||
                run.ClassId == "class.magus" && playerMotor.IsMagus && !playerMotor.IsBellator && !playerMotor.IsVenator;
        }

        private static bool SaveIdentityMatches(ExpeditionSaveData save, ExpeditionRunConfiguration run)
        {
            return save != null && run != null &&
                string.Equals(save.runId, run.RunId.ToString(), StringComparison.Ordinal) &&
                save.seed == run.Seed && save.generationVersion == run.Route.GenerationVersion &&
                string.Equals(save.classId, run.ClassId, StringComparison.Ordinal) &&
                string.Equals(save.raceId, run.RaceId, StringComparison.Ordinal);
        }

        private static bool ExpeditionStatesMatch(ExpeditionSaveData left, ExpeditionSaveData right)
        {
            return left != null && right != null &&
                string.Equals(JsonUtility.ToJson(left), JsonUtility.ToJson(right), StringComparison.Ordinal);
        }

        private static bool IsDescendantOf(Transform candidate, Transform root)
        {
            return candidate != null && root != null && (candidate == root || candidate.IsChildOf(root));
        }

        internal readonly struct CheckpointBindingWitnessData
        {
            internal CheckpointBindingWitnessData(ApplicationBootstrapper application, ExpeditionGameplayComposition owner,
                RunId runId, int routeGenerationVersion, string classId, string raceId,
                ExpeditionSaveData checkpoint, long revision, string nodeId, RoomLifetimeScope scope,
                PlayerMotor2D playerMotor, ItemCollector2D itemCollector, ActorId actorId)
            {
                Application = application;
                Owner = owner;
                RunId = runId;
                RouteGenerationVersion = routeGenerationVersion;
                ClassId = classId;
                RaceId = raceId;
                Checkpoint = checkpoint;
                Revision = revision;
                NodeId = nodeId;
                Scope = scope;
                PlayerMotor = playerMotor;
                ItemCollector = itemCollector;
                ActorId = actorId;
            }
            internal ApplicationBootstrapper Application { get; }
            internal ExpeditionGameplayComposition Owner { get; }
            internal RunId RunId { get; }
            internal int RouteGenerationVersion { get; }
            internal string ClassId { get; }
            internal string RaceId { get; }
            internal ExpeditionSaveData Checkpoint { get; }
            internal long Revision { get; }
            internal string NodeId { get; }
            internal RoomLifetimeScope Scope { get; }
            internal PlayerMotor2D PlayerMotor { get; }
            internal ItemCollector2D ItemCollector { get; }
            internal ActorId ActorId { get; }
        }

        internal readonly struct ConfirmedPublicationWitnessData
        {
            internal ConfirmedPublicationWitnessData(ApplicationBootstrapper application,
                ExpeditionGameplayComposition owner, RunId runId, int routeGenerationVersion,
                long revision, ExpeditionSaveData confirmedExpedition, ExpeditionRunConfiguration run,
                PlayerMotor2D playerMotor, ItemCollector2D itemCollector,
                RoomLifetimeScope scope, string nodeId)
            {
                Application = application;
                Owner = owner;
                RunId = runId;
                RouteGenerationVersion = routeGenerationVersion;
                Revision = revision;
                ConfirmedExpedition = confirmedExpedition;
                Run = run;
                PlayerMotor = playerMotor;
                ItemCollector = itemCollector;
                Scope = scope;
                NodeId = nodeId;
            }
            internal ApplicationBootstrapper Application { get; }
            internal ExpeditionGameplayComposition Owner { get; }
            internal RunId RunId { get; }
            internal int RouteGenerationVersion { get; }
            internal long Revision { get; }
            internal ExpeditionSaveData ConfirmedExpedition { get; }
            internal ExpeditionRunConfiguration Run { get; }
            internal PlayerMotor2D PlayerMotor { get; }
            internal ItemCollector2D ItemCollector { get; }
            internal RoomLifetimeScope Scope { get; }
            internal string NodeId { get; }
        }
    }

    internal sealed class ExpeditionCheckpointBindingWitness
    {
        private readonly ExpeditionSaveData _checkpoint;

        private ExpeditionCheckpointBindingWitness(
            ExpeditionSessionReadinessRegistration.CheckpointBindingWitnessData data)
        {
            Application = data.Application;
            Owner = data.Owner;
            RunId = data.RunId;
            RouteGenerationVersion = data.RouteGenerationVersion;
            ClassId = data.ClassId;
            RaceId = data.RaceId;
            _checkpoint = data.Checkpoint == null ? null : data.Checkpoint.Clone();
            Revision = data.Revision;
            NodeId = data.NodeId;
            Scope = data.Scope;
            PlayerMotor = data.PlayerMotor;
            ItemCollector = data.ItemCollector;
            ActorId = data.ActorId;
        }

        internal ApplicationBootstrapper Application { get; }
        internal ExpeditionGameplayComposition Owner { get; }
        internal RunId RunId { get; }
        internal int RouteGenerationVersion { get; }
        internal string ClassId { get; }
        internal string RaceId { get; }
        internal ExpeditionSaveData Checkpoint => _checkpoint == null ? null : _checkpoint.Clone();
        internal long Revision { get; }
        internal string NodeId { get; }
        internal RoomLifetimeScope Scope { get; }
        internal PlayerMotor2D PlayerMotor { get; }
        internal ItemCollector2D ItemCollector { get; }
        internal ActorId ActorId { get; }

        internal static ExpeditionCheckpointBindingWitness CreateAfterSuccessfulBinder(
            ExpeditionSessionReadinessRegistration producer)
        {
            if (producer == null) throw new ArgumentNullException(nameof(producer));
            return new ExpeditionCheckpointBindingWitness(producer.GetCheckpointBindingWitnessData());
        }
    }

    internal sealed class ExpeditionConfirmedPublicationWitness
    {
        private readonly ExpeditionSaveData _confirmedExpedition;

        private ExpeditionConfirmedPublicationWitness(
            ExpeditionSessionReadinessRegistration.ConfirmedPublicationWitnessData data)
        {
            Application = data.Application;
            Owner = data.Owner;
            RunId = data.RunId;
            RouteGenerationVersion = data.RouteGenerationVersion;
            Revision = data.Revision;
            _confirmedExpedition = data.ConfirmedExpedition == null ? null : data.ConfirmedExpedition.Clone();
            Run = data.Run;
            PlayerMotor = data.PlayerMotor;
            ItemCollector = data.ItemCollector;
            Scope = data.Scope;
            NodeId = data.NodeId;
        }

        internal ApplicationBootstrapper Application { get; }
        internal ExpeditionGameplayComposition Owner { get; }
        internal RunId RunId { get; }
        internal int RouteGenerationVersion { get; }
        internal long Revision { get; }
        internal ExpeditionSaveData ConfirmedExpedition => _confirmedExpedition == null ? null : _confirmedExpedition.Clone();
        internal long ConfirmedRevision => Revision;
        internal RunId ConfirmedRunId => RunId;
        internal string ConfirmedNodeId => _confirmedExpedition?.currentNodeId ?? string.Empty;
        internal PlayerMotor2D PlayerMotor { get; }
        internal ItemCollector2D ItemCollector { get; }
        internal RoomLifetimeScope Scope { get; }
        internal string NodeId { get; }
        internal ExpeditionRunConfiguration Run { get; }

        internal static ExpeditionConfirmedPublicationWitness CreateAfterConfirmedPublish(
            ExpeditionSessionReadinessRegistration producer,
            ExpeditionReadinessPreparedPublication publication,
            ExpeditionReadinessPublicationCheck check)
        {
            if (producer == null) throw new ArgumentNullException(nameof(producer));
            return new ExpeditionConfirmedPublicationWitness(
                producer.GetConfirmedPublicationWitnessData(publication, check));
        }
    }

    internal sealed class ExpeditionReadinessPreparedPublication
    {
        private readonly ExpeditionSessionReadinessRegistration _registration;
        private readonly ExpeditionRoomActivationRegistration _activation;
        private readonly ExpeditionPlayerRunBinder.PreparedCommittedRunPublication _prepared;
        private readonly ExpeditionSaveData _candidateSave;
        private readonly long _preparedAtRevision;
        private ExpeditionConfirmedPublicationWitness _confirmedWitness;

        private ExpeditionReadinessPreparedPublication(ExpeditionSessionReadinessRegistration registration,
            ExpeditionRoomActivationRegistration activation,
            ExpeditionPlayerRunBinder.PreparedCommittedRunPublication prepared,
            ExpeditionSaveData candidateSave, long preparedAtRevision, object creationToken)
        {
            _registration = registration ?? throw new ArgumentNullException(nameof(registration));
            if (!registration.IsPreparedPublicationCreationToken(creationToken))
                throw new InvalidOperationException("Prepared publications can only be created by a completed binder preparation.");
            _activation = activation ?? throw new ArgumentNullException(nameof(activation));
            _prepared = prepared ?? throw new ArgumentNullException(nameof(prepared));
            _candidateSave = candidateSave ?? throw new ArgumentNullException(nameof(candidateSave));
            _preparedAtRevision = preparedAtRevision;
        }

        internal static ExpeditionReadinessPreparedPublication CreateFromRegistration(
            ExpeditionSessionReadinessRegistration registration,
            ExpeditionRoomActivationRegistration activation,
            ExpeditionPlayerRunBinder.PreparedCommittedRunPublication prepared,
            ExpeditionSaveData candidateSave, long preparedAtRevision, object creationToken)
        {
            return new ExpeditionReadinessPreparedPublication(registration, activation,
                prepared, candidateSave, preparedAtRevision, creationToken);
        }

        internal ExpeditionRunConfiguration Run => _prepared.Run;
        internal ExpeditionSessionReadinessRegistration Registration => _registration;
        internal ExpeditionRoomActivationRegistration Activation => _activation;
        internal ExpeditionSaveData CandidateSave => _candidateSave;
        internal long PreparedAtRevision => _preparedAtRevision;
        internal ExpeditionConfirmedPublicationWitness ConfirmedWitness => _confirmedWitness;

        internal void ApplyToSave(ExpeditionSaveData candidateSave)
        {
            EnsureCurrentOwner();
            if (!ReferenceEquals(candidateSave, _candidateSave))
                throw new InvalidOperationException("The prepared publication can only update its exact candidate object.");
            _prepared.ApplyToSave(candidateSave);
        }

        internal ExpeditionConfirmedPublicationWitness PublishConfirmed()
        {
            EnsureCurrentOwner();
            if (_confirmedWitness != null)
            {
                ExpeditionReadinessAuthorityCapture current = _registration.Application
                    .CaptureEncounterReadinessAuthority(_registration);
                ExpeditionConfirmedPublicationWitness witness = _confirmedWitness;
                if (current.State != ExpeditionReadinessAuthorityState.Active ||
                    !MatchesConfirmedReplaySnapshot(witness, _registration.ConfirmedPublication,
                        Run, current.ActiveRun, witness.Revision, current.ActiveExpeditionRevision,
                        witness.ConfirmedExpedition, current.ActiveExpedition) ||
                    !ReferenceEquals(witness.Application, _registration.Application) ||
                    !ReferenceEquals(witness.Owner, _registration.Owner) ||
                    !ReferenceEquals(witness.Run, Run) || witness.RunId != Run.RunId ||
                    witness.RouteGenerationVersion != _registration.RouteGenerationVersion ||
                    witness.ConfirmedRevision != witness.Revision ||
                    !ReferenceEquals(witness.PlayerMotor, _activation.PlayerMotor) ||
                    !ReferenceEquals(witness.ItemCollector, _activation.ItemCollector) ||
                    !ReferenceEquals(witness.Scope, _activation.Scope) ||
                    !string.Equals(witness.NodeId, _activation.NodeId, StringComparison.Ordinal))
                    throw new InvalidOperationException("The confirmed publication no longer belongs to a live session.");
                return witness;
            }

            ExpeditionReadinessPublicationCheck before = _registration.Application
                .CheckConfirmedEncounterPublication(_registration, this);
            if (before.Status != ExpeditionReadinessPublicationStatus.Confirmed ||
                before.ConfirmedRevision <= _preparedAtRevision)
                throw new InvalidOperationException("The candidate save is not yet durably confirmed by App.");

            _prepared.Publish();

            ExpeditionReadinessPublicationCheck after = _registration.Application
                .CheckConfirmedEncounterPublication(_registration, this);
            if (after.Status != ExpeditionReadinessPublicationStatus.Confirmed ||
                after.ConfirmedRevision != before.ConfirmedRevision ||
                after.ConfirmedExpedition == null || before.ConfirmedExpedition == null ||
                !string.Equals(JsonUtility.ToJson(after.ConfirmedExpedition),
                    JsonUtility.ToJson(before.ConfirmedExpedition), StringComparison.Ordinal))
                throw new InvalidOperationException("The confirmed state changed while the prepared actor publication ran.");

            _registration.RecordConfirmedPublication(this, after);
            _confirmedWitness = _registration.ConfirmedPublication;
            return _confirmedWitness;
        }

        private void EnsureCurrentOwner()
        {
            if (_registration.IsDisposed || !_registration.IsCurrentActivation(_activation) ||
                _activation.State != ExpeditionRoomActivationState.Bound)
                throw new InvalidOperationException("The prepared publication belongs to an ended or replaced activation.");
            ExpeditionReadinessCompositionView view = _registration.Owner.CaptureReadinessCompositionView();
            if (view == null || !ReferenceEquals(view.Owner, _registration.Owner) ||
                !ReferenceEquals(view.Application, _registration.Application) ||
                !ReferenceEquals(view.ReadinessRegistration, _registration) ||
                !ReferenceEquals(view.PlayerMotor, _activation.PlayerMotor) ||
                !ReferenceEquals(view.ItemCollector, _activation.ItemCollector))
                throw new InvalidOperationException("The prepared publication's live composition ownership changed.");
        }

        internal static bool MatchesConfirmedReplaySnapshot(object witness, object registeredWitness,
            object expectedRun, object activeRun, long witnessRevision, long activeRevision,
            ExpeditionSaveData witnessState, ExpeditionSaveData activeState)
        {
            return witness != null && ReferenceEquals(witness, registeredWitness) &&
                expectedRun != null && ReferenceEquals(expectedRun, activeRun) &&
                witnessRevision == activeRevision && witnessState != null && activeState != null &&
                string.Equals(JsonUtility.ToJson(witnessState), JsonUtility.ToJson(activeState), StringComparison.Ordinal);
        }
    }
}
