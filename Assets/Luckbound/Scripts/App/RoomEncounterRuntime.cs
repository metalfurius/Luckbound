using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using UnityEngine;

namespace Luckbound.App
{
    internal enum RoomEncounterRuntimeStatus
    {
        NotReady,
        WaitingForSession,
        Armed,
        Cleared,
        Faulted,
        SessionEnded,
        Disposed
    }

    /// <summary>
    /// Immutable, room-local identity for one activation. Its generation is only an in-memory callback epoch;
    /// it is not a spawn key, result ID, save credential, or durable ticket.
    /// </summary>
    internal sealed class RoomEncounterRuntimeContext
    {
        private static long _nextGeneration;

        private RoomEncounterRuntimeContext(RunId runId, string nodeId, string templateId,
            RoomInstanceId roomInstanceId, RoomLifetimeScope scope,
            ExpeditionRoomTransitionController controller, PlayerMotor2D player,
            RoomEncounterRuntimePlan plan, int routeGenerationVersion, long activationGeneration)
        {
            RunId = runId;
            NodeId = nodeId;
            TemplateId = templateId;
            RoomInstanceId = roomInstanceId;
            Scope = scope;
            Controller = controller;
            Player = player;
            Plan = plan;
            RouteGenerationVersion = routeGenerationVersion;
            ActivationGeneration = activationGeneration;
        }

        internal RunId RunId { get; }
        internal string NodeId { get; }
        internal string TemplateId { get; }
        internal RoomInstanceId RoomInstanceId { get; }
        internal RoomLifetimeScope Scope { get; }
        internal ExpeditionRoomTransitionController Controller { get; }
        internal PlayerMotor2D Player { get; }
        internal RoomEncounterRuntimePlan Plan { get; }
        internal int RouteGenerationVersion { get; }
        internal long ActivationGeneration { get; }

        internal static RoomEncounterRuntimeContext Create(RunId runId, string nodeId, string templateId,
            RoomInstanceId roomInstanceId, RoomLifetimeScope scope,
            ExpeditionRoomTransitionController controller, PlayerMotor2D player,
            RoomEncounterRuntimePlan plan, int routeGenerationVersion)
        {
            if (!runId.IsValid) throw new ArgumentException("An encounter context requires a valid RunId.", nameof(runId));
            if (string.IsNullOrWhiteSpace(nodeId)) throw new ArgumentException("An encounter context requires a route node.", nameof(nodeId));
            if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("An encounter context requires a room template.", nameof(templateId));
            if (!roomInstanceId.IsValid) throw new ArgumentException("An encounter context requires a logical RoomInstanceId.", nameof(roomInstanceId));
            if (scope == null) throw new ArgumentNullException(nameof(scope));
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (routeGenerationVersion < 1) throw new ArgumentOutOfRangeException(nameof(routeGenerationVersion));

            RoomInstanceId expectedRoomId = ExpeditionRoomIdentity.ForNode(runId, nodeId);
            if (roomInstanceId != expectedRoomId || scope.RoomInstanceId != roomInstanceId ||
                !string.Equals(scope.TemplateId, templateId, StringComparison.Ordinal) ||
                !ReferenceEquals(controller.ActiveScope, scope) || controller.IsDisposed ||
                !controller.IsCurrentRoom(roomInstanceId) ||
                !string.Equals(controller.CurrentNodeId, nodeId, StringComparison.Ordinal) ||
                !scope.isActiveAndEnabled || scope.IsDisposed)
                throw new InvalidOperationException("The encounter context must identify the exact active room scope and route cursor.");

            long generation = NextGeneration();
            return new RoomEncounterRuntimeContext(runId, nodeId, templateId, roomInstanceId,
                scope, controller, player, plan, routeGenerationVersion, generation);
        }

        internal static long NextGeneration()
        {
            long generation = Interlocked.Increment(ref _nextGeneration);
            if (generation <= 0) throw new InvalidOperationException("Encounter activation generation was exhausted.");
            return generation;
        }

        internal bool IsCurrentRoomScope()
        {
            RoomLifetimeScope scope = Scope;
            ExpeditionRoomTransitionController controller = Controller;
            if (scope == null || controller == null ||
                scope.IsDisposed || !scope.isActiveAndEnabled || controller.IsDisposed)
                return false;

            return ReferenceEquals(controller.ActiveScope, scope) &&
                controller.IsCurrentRoom(RoomInstanceId) &&
                controller.CurrentRoomId == RoomInstanceId &&
                string.Equals(controller.CurrentNodeId, NodeId, StringComparison.Ordinal) &&
                scope.RoomInstanceId == RoomInstanceId &&
                string.Equals(scope.TemplateId, TemplateId, StringComparison.Ordinal) &&
                ExpeditionRoomIdentity.ForNode(RunId, NodeId) == RoomInstanceId;
        }
    }

    internal sealed class RoomEncounterActorDefinition
    {
        internal RoomEncounterActorDefinition(string spawnKey, GameObject slimePrefab,
            LayerMask groundLayers, Vector3 roomLocalPosition, Quaternion roomLocalRotation)
        {
            SpawnKey = spawnKey ?? string.Empty;
            SlimePrefab = slimePrefab;
            GroundLayers = groundLayers;
            RoomLocalPosition = roomLocalPosition;
            RoomLocalRotation = roomLocalRotation;
        }

        internal string SpawnKey { get; }
        internal GameObject SlimePrefab { get; }
        internal LayerMask GroundLayers { get; }
        internal Vector3 RoomLocalPosition { get; }
        internal Quaternion RoomLocalRotation { get; }

        internal bool SupportsConcreteSlimeAdapter
        {
            get
            {
                if (SlimePrefab == null || GroundLayers.value == 0) return false;
                Slime2D[] slimes = SlimePrefab.GetComponents<Slime2D>();
                return slimes.Length == 1 && SlimePrefab.GetComponent<Rigidbody2D>() != null &&
                    SlimePrefab.GetComponent<Collider2D>() != null;
            }
        }
    }

    internal sealed class RoomEncounterWaveActorDefinition
    {
        private readonly IReadOnlyList<RoomEncounterActorDefinition> _actors;

        internal RoomEncounterWaveActorDefinition(int waveNumber, IEnumerable<RoomEncounterActorDefinition> actors)
        {
            WaveNumber = waveNumber;
            _actors = Array.AsReadOnly(actors == null
                ? Array.Empty<RoomEncounterActorDefinition>()
                : actors.ToArray());
        }

        internal int WaveNumber { get; }
        internal IReadOnlyList<RoomEncounterActorDefinition> Actors => _actors;
    }

    /// <summary>
    /// A complete structural roster plus exact actor definitions. No production map/provider is present in this
    /// slice; fixtures must use explicitly named fixture keys and never stand in for C08 or D06 content.
    /// </summary>
    internal sealed class RoomEncounterRuntimePlan
    {
        private readonly IReadOnlyList<RoomEncounterWaveActorDefinition> _waves;
        private readonly Dictionary<string, RoomEncounterActorDefinition> _actorsByKey;

        internal RoomEncounterRuntimePlan(ExpeditionEncounterRosterPlan roster,
            IEnumerable<RoomEncounterWaveActorDefinition> waves)
        {
            Roster = roster;
            RoomEncounterWaveActorDefinition[] copied = waves == null
                ? Array.Empty<RoomEncounterWaveActorDefinition>()
                : waves.ToArray();
            _waves = Array.AsReadOnly(copied);
            _actorsByKey = new Dictionary<string, RoomEncounterActorDefinition>(StringComparer.Ordinal);
            HasCompleteCoverage = ValidateCoverage();
        }

        internal ExpeditionEncounterRosterPlan Roster { get; }
        internal IReadOnlyList<RoomEncounterWaveActorDefinition> Waves => _waves;
        internal bool HasCompleteCoverage { get; }

        internal bool HasOnlySupportedSlimes
        {
            get
            {
                if (!HasCompleteCoverage || Roster == null) return false;
                foreach (RoomEncounterWaveActorDefinition wave in _waves)
                {
                    foreach (RoomEncounterActorDefinition actor in wave.Actors)
                    {
                        if (actor == null || !actor.SupportsConcreteSlimeAdapter) return false;
                    }
                }

                return true;
            }
        }

        internal RoomEncounterWaveActorDefinition GetWave(int waveNumber)
        {
            if (waveNumber < 1 || waveNumber > _waves.Count) return null;
            RoomEncounterWaveActorDefinition wave = _waves[waveNumber - 1];
            return wave != null && wave.WaveNumber == waveNumber ? wave : null;
        }

        internal bool TryGetActor(string spawnKey, out RoomEncounterActorDefinition actor)
        {
            return _actorsByKey.TryGetValue(spawnKey ?? string.Empty, out actor);
        }

        private bool ValidateCoverage()
        {
            _actorsByKey.Clear();
            if (Roster == null || _waves.Count != Roster.Waves.Count) return false;
            for (int waveIndex = 0; waveIndex < _waves.Count; waveIndex++)
            {
                RoomEncounterWaveActorDefinition actorWave = _waves[waveIndex];
                ExpeditionEncounterWavePlan rosterWave = Roster.Waves[waveIndex];
                if (actorWave == null || rosterWave == null || actorWave.WaveNumber != waveIndex + 1 ||
                    actorWave.Actors.Count != rosterWave.SpawnKeys.Count)
                    return false;

                for (int actorIndex = 0; actorIndex < actorWave.Actors.Count; actorIndex++)
                {
                    RoomEncounterActorDefinition actor = actorWave.Actors[actorIndex];
                    if (actor == null || !string.Equals(actor.SpawnKey, rosterWave.SpawnKeys[actorIndex], StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(actor.SpawnKey) || _actorsByKey.ContainsKey(actor.SpawnKey))
                        return false;
                    _actorsByKey.Add(actor.SpawnKey, actor);
                }
            }

            return true;
        }
    }

    /// <summary>Typed bridge for the future coordinator; a synthetic test snapshot is not a production producer.</summary>
    internal interface IRoomEncounterRuntimeSessionSnapshotSource
    {
        bool TryCapture(RoomEncounterRuntimeContext context, out RoomEncounterRuntimeSessionSnapshot snapshot);
    }

    /// <summary>
    /// Immutable dynamic session view tied to one exact activation. The future composition must build it from
    /// real checkpoint/binding/commit/lease state. This package deliberately supplies no production producer.
    /// </summary>
    internal readonly struct RoomEncounterRuntimeSessionSnapshot
    {
        internal RoomEncounterRuntimeSessionSnapshot(RunId activeRunId, string activeNodeId,
            string activeTemplateId, int activeRouteGenerationVersion, RoomInstanceId activeRoomInstanceId,
            RoomLifetimeScope activeScope, ExpeditionRoomTransitionController activeController,
            PlayerMotor2D activePlayer, long activationGeneration, bool isSessionActive,
            bool hasBoundPlayer, bool hasCheckpointBinding, bool hasPublishedSession,
            bool hasRequiredGameplayBindings, bool roomCommitAndActivationComplete,
            bool hasPendingSessionMutation, bool isRoomAdvanceInFlight,
            bool hasPendingCommitOrActivation, bool hasPendingSimulationLeaseOrGate,
            bool isSimulationPaused)
        {
            ActiveRunId = activeRunId;
            ActiveNodeId = activeNodeId ?? string.Empty;
            ActiveTemplateId = activeTemplateId ?? string.Empty;
            ActiveRouteGenerationVersion = activeRouteGenerationVersion;
            ActiveRoomInstanceId = activeRoomInstanceId;
            ActiveScope = activeScope;
            ActiveController = activeController;
            ActivePlayer = activePlayer;
            ActivationGeneration = activationGeneration;
            IsSessionActive = isSessionActive;
            HasBoundPlayer = hasBoundPlayer;
            HasCheckpointBinding = hasCheckpointBinding;
            HasPublishedSession = hasPublishedSession;
            HasRequiredGameplayBindings = hasRequiredGameplayBindings;
            RoomCommitAndActivationComplete = roomCommitAndActivationComplete;
            HasPendingSessionMutation = hasPendingSessionMutation;
            IsRoomAdvanceInFlight = isRoomAdvanceInFlight;
            HasPendingCommitOrActivation = hasPendingCommitOrActivation;
            HasPendingSimulationLeaseOrGate = hasPendingSimulationLeaseOrGate;
            IsSimulationPaused = isSimulationPaused;
            IsInitialized = true;
        }

        internal RunId ActiveRunId { get; }
        internal string ActiveNodeId { get; }
        internal string ActiveTemplateId { get; }
        internal int ActiveRouteGenerationVersion { get; }
        internal RoomInstanceId ActiveRoomInstanceId { get; }
        internal RoomLifetimeScope ActiveScope { get; }
        internal ExpeditionRoomTransitionController ActiveController { get; }
        internal PlayerMotor2D ActivePlayer { get; }
        internal long ActivationGeneration { get; }
        internal bool IsSessionActive { get; }
        internal bool HasBoundPlayer { get; }
        internal bool HasCheckpointBinding { get; }
        internal bool HasPublishedSession { get; }
        internal bool HasRequiredGameplayBindings { get; }
        internal bool RoomCommitAndActivationComplete { get; }
        internal bool HasPendingSessionMutation { get; }
        internal bool IsRoomAdvanceInFlight { get; }
        internal bool HasPendingCommitOrActivation { get; }
        internal bool HasPendingSimulationLeaseOrGate { get; }
        internal bool IsSimulationPaused { get; }
        internal bool IsInitialized { get; }

        internal bool Matches(RoomEncounterRuntimeContext context)
        {
            return IsInitialized && context != null && ActiveRunId == context.RunId &&
                string.Equals(ActiveNodeId, context.NodeId, StringComparison.Ordinal) &&
                string.Equals(ActiveTemplateId, context.TemplateId, StringComparison.Ordinal) &&
                ActiveRouteGenerationVersion == context.RouteGenerationVersion &&
                ActiveRoomInstanceId == context.RoomInstanceId &&
                ReferenceEquals(ActiveScope, context.Scope) &&
                ReferenceEquals(ActiveController, context.Controller) &&
                ReferenceEquals(ActivePlayer, context.Player) &&
                ActivationGeneration == context.ActivationGeneration;
        }

        internal bool CanArmOrDrain => IsSessionActive && HasBoundPlayer && HasCheckpointBinding &&
            HasPublishedSession && HasRequiredGameplayBindings && RoomCommitAndActivationComplete &&
            !HasPendingSessionMutation && !IsRoomAdvanceInFlight && !HasPendingCommitOrActivation &&
            !HasPendingSimulationLeaseOrGate && !IsSimulationPaused;
    }

    internal readonly struct RoomEncounterDefeatedActorObservation
    {
        internal RoomEncounterDefeatedActorObservation(string spawnKey, int waveNumber, ActorId actorId,
            long capturedSequence, long processedDrainSequence, int capturedFrame,
            int processedDrainFrame, double capturedFixedTime, double processedFixedTime)
        {
            SpawnKey = spawnKey;
            WaveNumber = waveNumber;
            ActorId = actorId;
            CapturedSequence = capturedSequence;
            ProcessedDrainSequence = processedDrainSequence;
            CapturedFrame = capturedFrame;
            ProcessedDrainFrame = processedDrainFrame;
            CapturedFixedTime = capturedFixedTime;
            ProcessedFixedTime = processedFixedTime;
        }

        internal string SpawnKey { get; }
        internal int WaveNumber { get; }
        internal ActorId ActorId { get; }
        internal long CapturedSequence { get; }
        internal long ProcessedDrainSequence { get; }
        internal int CapturedFrame { get; }
        internal int ProcessedDrainFrame { get; }
        internal double CapturedFixedTime { get; }
        internal double ProcessedFixedTime { get; }
    }

    /// <summary>Local technical observation only. It carries no payout, save, route, or objective command.</summary>
    internal sealed class RoomEncounterClearObservation
    {
        private readonly IReadOnlyList<RoomEncounterDefeatedActorObservation> _actors;

        internal RoomEncounterClearObservation(RoomEncounterRuntimeContext context,
            IEnumerable<RoomEncounterDefeatedActorObservation> actors)
        {
            RunId = context.RunId;
            NodeId = context.NodeId;
            TemplateId = context.TemplateId;
            RoomInstanceId = context.RoomInstanceId;
            Scope = context.Scope;
            ActivationGeneration = context.ActivationGeneration;
            _actors = Array.AsReadOnly(actors.ToArray());
        }

        internal RunId RunId { get; }
        internal string NodeId { get; }
        internal string TemplateId { get; }
        internal RoomInstanceId RoomInstanceId { get; }
        internal RoomLifetimeScope Scope { get; }
        internal long ActivationGeneration { get; }
        internal IReadOnlyList<RoomEncounterDefeatedActorObservation> Actors => _actors;
    }

    internal interface IRoomEncounterClearObserver
    {
        void OnRoomEncounterCleared(RoomEncounterClearObservation observation);
    }

    /// <summary>
    /// Scope-owned, main-thread encounter adapter. It only records verified physical Slime defeats and emits one
    /// local observation. It does not resolve rewards, complete objectives, save, or advance the route.
    /// </summary>
    internal sealed class RoomEncounterRuntime : IDisposable
    {
        private enum FactKind
        {
            VerifiedDefeat,
            LiveActorUnavailable,
            InvalidSignal
        }

        private readonly struct RuntimeFact
        {
            internal RuntimeFact(FactKind kind, RoomEncounterActorBinding binding, ActorId actorId,
                long generation, int waveNumber, long capturedSequence, int capturedFrame, double capturedFixedTime)
            {
                Kind = kind;
                Binding = binding;
                ActorId = actorId;
                Generation = generation;
                WaveNumber = waveNumber;
                CapturedSequence = capturedSequence;
                CapturedFrame = capturedFrame;
                CapturedFixedTime = capturedFixedTime;
            }

            internal FactKind Kind { get; }
            internal RoomEncounterActorBinding Binding { get; }
            internal ActorId ActorId { get; }
            internal long Generation { get; }
            internal int WaveNumber { get; }
            internal long CapturedSequence { get; }
            internal int CapturedFrame { get; }
            internal double CapturedFixedTime { get; }
        }

        private RoomEncounterRuntimeContext _context;
        private IRoomEncounterRuntimeSessionSnapshotSource _sessionSource;
        private IRoomEncounterClearObserver _clearObserver;
        private RoomEncounterDriver2D _driver;
        private ExpeditionEncounterRosterState _roster;
        private readonly Dictionary<string, RoomEncounterActorBinding> _bindingsByKey =
            new Dictionary<string, RoomEncounterActorBinding>(StringComparer.Ordinal);
        private readonly List<RoomEncounterActorBinding> _bindings = new List<RoomEncounterActorBinding>();
        private readonly HashSet<UnityEngine.EntityId> _actorInstanceIds =
            new HashSet<UnityEngine.EntityId>();
        private readonly Queue<RuntimeFact> _facts = new Queue<RuntimeFact>();
        private RoomEncounterRuntimeStatus _status;
        private RoomEncounterClearObservation _lastClearObservation;
        private long _callbackGeneration;
        private long _sequence;
        private int _mainThreadId;
        private bool _isDraining;
        private bool _clearNotificationIssued;
        private bool _planSupported;
        private string _faultReason = string.Empty;

        private RoomEncounterRuntime(RoomEncounterRuntimeContext context,
            IRoomEncounterRuntimeSessionSnapshotSource sessionSource,
            IRoomEncounterClearObserver clearObserver)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _sessionSource = sessionSource;
            _clearObserver = clearObserver;
            _callbackGeneration = context.ActivationGeneration;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _status = RoomEncounterRuntimeStatus.NotReady;
        }

        internal RoomEncounterRuntimeStatus Status => _status;
        internal string FaultReason => _faultReason;
        internal bool ClearNotificationIssued => _clearNotificationIssued;
        internal bool IsDisposed => _status == RoomEncounterRuntimeStatus.Disposed;
        internal int PendingFactCount => _facts.Count;
        internal int CurrentWaveNumber => _roster == null ? 0 : _roster.CurrentWaveNumber;
        internal int CompletedWaveCount => _roster == null ? 0 : _roster.CompletedWaveCount;
        internal RoomEncounterClearObservation LastClearObservation => _lastClearObservation;
        internal long CallbackGeneration => _callbackGeneration;
        internal IReadOnlyList<RoomEncounterActorBinding> RegisteredBindings => _bindings.ToArray();

        /// <summary>Creates and registers the exact runtime instance as an IDisposable on the exact active scope.</summary>
        internal static RoomEncounterRuntime Prepare(RoomEncounterRuntimeContext context,
            IRoomEncounterRuntimeSessionSnapshotSource sessionSource,
            IRoomEncounterClearObserver clearObserver)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (sessionSource == null) throw new ArgumentNullException(nameof(sessionSource));
            if (clearObserver == null) throw new ArgumentNullException(nameof(clearObserver));
            if (!context.IsCurrentRoomScope())
                throw new InvalidOperationException("Encounter runtime can only attach to its exact active room scope.");

            var runtime = new RoomEncounterRuntime(context, sessionSource, clearObserver);
            try
            {
                runtime.PrepareRuntime();
                RoomEncounterRuntime owned = context.Scope.OwnResource(runtime);
                if (!ReferenceEquals(owned, runtime))
                    throw new InvalidOperationException("RoomLifetimeScope did not retain the exact encounter runtime.");
                return runtime;
            }
            catch
            {
                runtime.Dispose();
                throw;
            }
        }

        private void PrepareRuntime()
        {
            RoomEncounterRuntimeContext context = _context;
            if (context == null || !context.IsCurrentRoomScope())
            {
                Fault("The room context became stale during runtime preparation.");
                return;
            }

            _planSupported = context.Plan != null && context.Plan.HasOnlySupportedSlimes;
            if (!_planSupported)
            {
                _status = RoomEncounterRuntimeStatus.NotReady;
            }
            else
            {
                _roster = new ExpeditionEncounterRosterState(context.Plan.Roster);
                _status = RoomEncounterRuntimeStatus.WaitingForSession;
            }

            RoomEncounterDriver2D driver = context.Scope.GetComponent<RoomEncounterDriver2D>();
            if (driver != null)
                throw new InvalidOperationException("A room scope can own only one RoomEncounterRuntime driver.");
            _driver = context.Scope.gameObject.AddComponent<RoomEncounterDriver2D>();
            _driver.Initialize(this);
        }

        /// <summary>
        /// Called by the future coordinator after it has observed real checkpoint binding and settled room/session
        /// gates. This method consumes a typed snapshot, never a UI-selected boolean or arbitrary callback.
        /// </summary>
        internal bool ConfirmSessionReadyForScope()
        {
            if (!IsOnMainThread() || _status == RoomEncounterRuntimeStatus.Disposed ||
                _status == RoomEncounterRuntimeStatus.Faulted || _status == RoomEncounterRuntimeStatus.SessionEnded ||
                _status == RoomEncounterRuntimeStatus.Cleared)
                return false;
            bool alreadyArmed = _status == RoomEncounterRuntimeStatus.Armed;
            if (!_planSupported || _roster == null || _context == null)
            {
                if (!alreadyArmed) _status = RoomEncounterRuntimeStatus.NotReady;
                return false;
            }

            SnapshotReadResult read;
            RoomEncounterRuntimeSessionSnapshot snapshot;
            try
            {
                read = TryReadSnapshot(out snapshot);
            }
            catch (Exception exception)
            {
                Fault("The typed session snapshot could not be read: " + exception.GetType().Name + ".");
                return false;
            }
            if (read == SnapshotReadResult.ContextMismatch)
            {
                Fault("The activation snapshot no longer matches the exact run, route, player, controller, and scope.");
                return false;
            }
            if (read == SnapshotReadResult.Unavailable)
            {
                if (!alreadyArmed) _status = RoomEncounterRuntimeStatus.WaitingForSession;
                return false;
            }
            if (read == SnapshotReadResult.SessionEnded)
            {
                InhibitForEndedSession();
                return false;
            }
            if (!CanArmOrDrain(snapshot))
            {
                if (!alreadyArmed) _status = RoomEncounterRuntimeStatus.WaitingForSession;
                return false;
            }

            // Confirmation is only allowed to perform the initial arm. Once armed, the existing roster,
            // bindings, and queued facts remain owned by the active drain across temporary readiness gaps.
            if (alreadyArmed)
            {
                if (_roster.Phase != ExpeditionEncounterRosterPhase.Armed)
                {
                    Fault("The armed runtime no longer matches its roster phase.");
                    return false;
                }
                return IsPhysicsSimulationAdmitted() && _context.IsCurrentRoomScope();
            }

            return PrepareAndArmCurrentWave(snapshot);
        }

        internal void DrainAfterPhysics()
        {
            if (!IsOnMainThread() || _isDraining || _status != RoomEncounterRuntimeStatus.Armed || _context == null)
                return;

            _isDraining = true;
            try
            {
                SnapshotReadResult read = TryReadSnapshot(out RoomEncounterRuntimeSessionSnapshot snapshot);
                if (read == SnapshotReadResult.ContextMismatch)
                {
                    Fault("The post-physics drain found a stale run, route, player, controller, or room scope.");
                    return;
                }
                if (read == SnapshotReadResult.SessionEnded)
                {
                    InhibitForEndedSession();
                    return;
                }
                if (read == SnapshotReadResult.Unavailable || !CanArmOrDrain(snapshot) ||
                    !IsPhysicsSimulationAdmitted())
                    return;

                if (!AuditLiveBindings()) return;

                int factsToDrain = _facts.Count;
                long drainSequence = ++_sequence;
                for (int index = 0; index < factsToDrain; index++)
                {
                    RuntimeFact fact = _facts.Dequeue();
                    if (fact.Generation != _callbackGeneration || fact.Generation != _context.ActivationGeneration)
                        continue;
                    if (fact.Kind == FactKind.InvalidSignal)
                    {
                        Fault("A Slime adapter reported a mismatched or unverified callback.");
                        return;
                    }
                    if (!ReferenceEquals(fact.Binding, GetRegisteredBinding(fact.Binding)) ||
                        fact.Binding == null || fact.Binding.Generation != fact.Generation ||
                        !ReferenceEquals(fact.Binding.Scope, _context.Scope) || fact.WaveNumber != fact.Binding.WaveNumber)
                    {
                        Fault("A callback did not match its exact registered actor, scope, or generation.");
                        return;
                    }

                    if (fact.Kind == FactKind.LiveActorUnavailable)
                    {
                        if (fact.Binding.PhysicalDeathObserved) continue;
                        _roster.TryRecordUnexpectedLiveRemoval(fact.Binding.SpawnKey, fact.Binding.ActorId);
                        Fault("A live Slime component or root became unavailable.");
                        return;
                    }

                    if (!fact.Binding.PhysicalDeathObserved || fact.ActorId != fact.Binding.ActorId ||
                        _roster.Phase != ExpeditionEncounterRosterPhase.Armed ||
                        _roster.CurrentWaveNumber != fact.WaveNumber)
                    {
                        Fault("A queued death fact no longer matches the armed physical wave.");
                        return;
                    }

                    if (!_context.IsCurrentRoomScope())
                    {
                        Fault("The room scope changed before a captured defeat could be drained.");
                        return;
                    }

                    bool recorded = _roster.TryRecordVerifiedDefeat(fact.Binding.SpawnKey, fact.ActorId);
                    if (!recorded && _roster.Phase == ExpeditionEncounterRosterPhase.Faulted)
                    {
                        Fault("The roster rejected a physical defeat fact.");
                        return;
                    }
                    if (recorded)
                        fact.Binding.MarkDefeatAccepted(fact.CapturedSequence, drainSequence,
                            fact.CapturedFrame, Time.frameCount, fact.CapturedFixedTime, Time.fixedTimeAsDouble);
                }

                if (_status != RoomEncounterRuntimeStatus.Armed || !AuditLiveBindings()) return;

                read = TryReadSnapshot(out snapshot);
                if (read == SnapshotReadResult.ContextMismatch)
                {
                    Fault("The room context changed before the clear audit.");
                    return;
                }
                if (read == SnapshotReadResult.SessionEnded)
                {
                    InhibitForEndedSession();
                    return;
                }
                if (read != SnapshotReadResult.Available || !CanArmOrDrain(snapshot) ||
                    !IsPhysicsSimulationAdmitted())
                    return;

                if (_roster.IsCleared)
                {
                    PublishClearOnce(drainSequence, snapshot);
                    return;
                }

                if (_roster.Phase == ExpeditionEncounterRosterPhase.Preparing && _roster.CompletedWaveCount > 0)
                    PrepareAndArmCurrentWave(snapshot);
            }
            catch (Exception exception)
            {
                Fault("Encounter drain failed closed: " + exception.GetType().Name + ".");
            }
            finally
            {
                _isDraining = false;
            }
        }

        private bool PrepareAndArmCurrentWave(RoomEncounterRuntimeSessionSnapshot snapshot)
        {
            if (_status == RoomEncounterRuntimeStatus.Disposed || _status == RoomEncounterRuntimeStatus.Faulted ||
                _status == RoomEncounterRuntimeStatus.SessionEnded || _clearNotificationIssued ||
                _context == null || _roster == null || _roster.Phase != ExpeditionEncounterRosterPhase.Preparing)
                return false;
            if (!CanArmOrDrain(snapshot) || !IsPhysicsSimulationAdmitted() || !_context.IsCurrentRoomScope())
                return false;

            int waveNumber = _roster.CurrentWaveNumber;
            RoomEncounterWaveActorDefinition wave = _context.Plan.GetWave(waveNumber);
            if (wave == null || wave.Actors.Count == 0)
            {
                Fault("The current wave has no complete actor definition.");
                return false;
            }

            RoomLifetimeScope scope = _context.Scope;
            GameObject staging = new GameObject("RoomEncounterStaging." + _context.ActivationGeneration + "." + waveNumber);
            staging.SetActive(false);
            bool stagingOwned = false;
            var waveBindings = new List<RoomEncounterActorBinding>(wave.Actors.Count);
            try
            {
                if (!ReferenceEquals(scope.OwnActor(staging), staging) ||
                    !ReferenceEquals(staging.transform.parent, scope.transform) || !scope.isActiveAndEnabled || scope.IsDisposed)
                    throw new InvalidOperationException("The inactive spawn staging actor was not owned by the exact active room scope.");
                stagingOwned = true;

                // This entire register/arm/activate block is synchronous on Unity's main thread: no await, yield,
                // coroutine, or physics step can interleave between creating the first actor and arming the wave.
                foreach (RoomEncounterActorDefinition definition in wave.Actors)
                {
                    if (definition == null || !definition.SupportsConcreteSlimeAdapter ||
                        !_context.Plan.TryGetActor(definition.SpawnKey, out RoomEncounterActorDefinition planned) ||
                        !ReferenceEquals(planned, definition))
                    {
                        Fault("A planned spawn has no supported concrete Slime adapter.");
                        return false;
                    }

                    GameObject actorRoot = UnityEngine.Object.Instantiate(definition.SlimePrefab, staging.transform, false);
                    if (actorRoot == null) throw new InvalidOperationException("Could not instantiate a planned Slime actor.");
                    actorRoot.transform.localPosition = definition.RoomLocalPosition;
                    actorRoot.transform.localRotation = definition.RoomLocalRotation;
                    bool inactiveBeforeOwnership = !actorRoot.activeSelf;
                    if (!inactiveBeforeOwnership) actorRoot.SetActive(false);
                    inactiveBeforeOwnership = !actorRoot.activeSelf;
                    if (!inactiveBeforeOwnership)
                        throw new InvalidOperationException("An actor root must be inactive before RoomLifetimeScope.OwnActor.");

                    GameObject ownedRoot = scope.OwnActor(actorRoot);
                    if (!ReferenceEquals(ownedRoot, actorRoot) || !ReferenceEquals(actorRoot.transform.parent, scope.transform) ||
                        !ReferenceEquals(scope, _context.Controller.ActiveScope) || scope.IsDisposed)
                        throw new InvalidOperationException("The exact actor root was not retained by the active scope.");

                    Slime2D[] slimeComponents = actorRoot.GetComponents<Slime2D>();
                    if (slimeComponents.Length != 1 || slimeComponents[0] == null)
                        throw new InvalidOperationException("A Slime spawn must have exactly one root Slime2D component.");
                    ActorId actorId = ActorId.New();
                    slimeComponents[0].Initialize(actorId, _context.Player, definition.GroundLayers);
                    ProjectileImpactTarget2D impactTarget = actorRoot.GetComponent<ProjectileImpactTarget2D>();
                    ProjectileImpactTarget2D[] impactTargets = actorRoot.GetComponents<ProjectileImpactTarget2D>();
                    if (impactTarget == null || impactTargets.Length != 1 || !impactTarget.IsInitialized ||
                        impactTarget.ActorId != actorId || slimeComponents[0].ActorId != actorId ||
                        slimeComponents[0].IsDead || slimeComponents[0].CombatStats == null ||
                        slimeComponents[0].CurrentHealth <= 0)
                        throw new InvalidOperationException("A Slime's actor identity, initialized impact target, or health is invalid.");

                    Rigidbody2D body = actorRoot.GetComponent<Rigidbody2D>();
                    Collider2D combatCollider = actorRoot.GetComponent<Collider2D>();
                    Collider2D[] combatColliders = actorRoot.GetComponentsInChildren<Collider2D>(true)
                        .Where(collider => collider != null && collider.attachedRigidbody == body).ToArray();
                    if (body == null || !body.simulated || combatCollider == null ||
                        combatColliders.Length == 0 || !combatColliders.Contains(combatCollider) ||
                        combatColliders.Any(collider => !collider.enabled))
                        throw new InvalidOperationException("A Slime's initialized combat body or collider set is unavailable.");

                    RoomEncounterActorLifetimeRelay2D relay = actorRoot.AddComponent<RoomEncounterActorLifetimeRelay2D>();
                    var binding = new RoomEncounterActorBinding(this, definition.SpawnKey, waveNumber,
                        _callbackGeneration, scope, actorRoot, slimeComponents[0], impactTarget, relay,
                        body, combatCollider, combatColliders,
                        inactiveBeforeOwnership);
                    if (!RegisterBinding(binding) || !_roster.TryRegisterSpawn(definition.SpawnKey, actorId))
                        throw new InvalidOperationException("A spawn key, actor ID, root, or component was duplicated or rejected.");

                    binding.Subscribe();
                    waveBindings.Add(binding);
                }

                if (waveBindings.Count != wave.Actors.Count || !_roster.CanArmCurrentWave ||
                    !_roster.TryArmCurrentWave())
                    throw new InvalidOperationException("The full current wave could not be registered and armed.");

                SnapshotReadResult readinessRead = TryReadSnapshot(out RoomEncounterRuntimeSessionSnapshot latestSnapshot);
                if (readinessRead != SnapshotReadResult.Available || !CanArmOrDrain(latestSnapshot) ||
                    !IsPhysicsSimulationAdmitted() || !_context.IsCurrentRoomScope())
                    throw new InvalidOperationException("Session readiness or exact scope changed before wave activation.");

                foreach (RoomEncounterActorBinding binding in waveBindings)
                    binding.BeginActivation();
                _status = RoomEncounterRuntimeStatus.Armed;

                foreach (RoomEncounterActorBinding binding in waveBindings)
                {
                    if (!ReferenceEquals(binding.Scope, scope) || !binding.BeginActivationRoot())
                        throw new InvalidOperationException("A registered Slime could not enter its expected active phase.");
                }

                if (!AuditLiveBindings()) return false;
                return true;
            }
            catch (Exception exception)
            {
                Fault("Wave registration or activation failed closed: " + exception.GetType().Name + ".");
                return false;
            }
            finally
            {
                if (!stagingOwned && staging != null) UnityEngine.Object.Destroy(staging);
            }
        }

        private bool RegisterBinding(RoomEncounterActorBinding binding)
        {
            if (binding == null || binding.Root == null || _bindingsByKey.ContainsKey(binding.SpawnKey) ||
                _bindings.Any(existing => ReferenceEquals(existing.Root, binding.Root) ||
                    ReferenceEquals(existing.Slime, binding.Slime)))
                return false;

            // Unity 6000.6 marks Object.GetInstanceID as an error. GetEntityId is the supported unique
            // object identity value; HashSet uses EntityId.Equals/GetHashCode rather than a lossy cast.
            UnityEngine.EntityId rootEntityId = binding.Root.GetEntityId();
            if (!rootEntityId.IsValid() || !_actorInstanceIds.Add(rootEntityId)) return false;

            _bindingsByKey.Add(binding.SpawnKey, binding);
            _bindings.Add(binding);
            return true;
        }

        internal bool IsCurrentBinding(RoomEncounterActorBinding binding, long generation)
        {
            if (!IsOnMainThread() || binding == null || _status == RoomEncounterRuntimeStatus.Disposed ||
                _status == RoomEncounterRuntimeStatus.Faulted || _status == RoomEncounterRuntimeStatus.SessionEnded ||
                _status == RoomEncounterRuntimeStatus.Cleared || generation != _callbackGeneration ||
                _context == null || generation != _context.ActivationGeneration)
                return false;

            return _bindingsByKey.TryGetValue(binding.SpawnKey, out RoomEncounterActorBinding registered) &&
                ReferenceEquals(registered, binding);
        }

        internal bool IsCurrentArmedBinding(RoomEncounterActorBinding binding)
        {
            return _status == RoomEncounterRuntimeStatus.Armed && _roster != null &&
                _roster.Phase == ExpeditionEncounterRosterPhase.Armed &&
                _roster.CurrentWaveNumber == binding.WaveNumber &&
                binding.Phase == RoomEncounterActorBindingPhase.Live;
        }

        internal bool ValidateSignalContext(RoomEncounterActorBinding binding)
        {
            if (!IsCurrentBinding(binding, binding.Generation) || _context == null ||
                !_context.IsCurrentRoomScope())
                return false;
            try
            {
                SnapshotReadResult read = TryReadSnapshot(out RoomEncounterRuntimeSessionSnapshot snapshot);
                return read == SnapshotReadResult.Available && snapshot.IsSessionActive;
            }
            catch
            {
                return false;
            }
        }

        internal bool ValidateActorAtDeathSignal(RoomEncounterActorBinding binding, ActorId reportedActorId)
        {
            if (!ValidateSignalContext(binding) || !IsCurrentArmedBinding(binding) ||
                reportedActorId != binding.ActorId || !binding.IsExactActiveOwnedActor() ||
                !binding.Slime.IsDead || binding.Slime.CurrentHealth != 0 ||
                binding.Slime.CombatStats == null || !binding.ImpactTarget.IsInitialized ||
                binding.ImpactTarget.ActorId != binding.ActorId)
                return false;

            return true;
        }

        internal long EnqueueVerifiedDefeat(RoomEncounterActorBinding binding, ActorId actorId)
        {
            return EnqueueFact(FactKind.VerifiedDefeat, binding, actorId);
        }

        internal void EnqueueUnavailable(RoomEncounterActorBinding binding, ActorId actorId)
        {
            EnqueueFact(FactKind.LiveActorUnavailable, binding, actorId);
        }

        internal void EnqueueInvalidSignal(RoomEncounterActorBinding binding, ActorId actorId)
        {
            EnqueueFact(FactKind.InvalidSignal, binding, actorId);
        }

        private long EnqueueFact(FactKind kind, RoomEncounterActorBinding binding, ActorId actorId)
        {
            if (!IsOnMainThread() || binding == null || _status == RoomEncounterRuntimeStatus.Disposed ||
                _status == RoomEncounterRuntimeStatus.Faulted || _status == RoomEncounterRuntimeStatus.SessionEnded ||
                _status == RoomEncounterRuntimeStatus.Cleared || _callbackGeneration != binding.Generation)
                return 0;

            long capturedSequence = ++_sequence;
            _facts.Enqueue(new RuntimeFact(kind, binding, actorId, binding.Generation, binding.WaveNumber,
                capturedSequence, Time.frameCount, Time.fixedTimeAsDouble));
            return capturedSequence;
        }

        private RoomEncounterActorBinding GetRegisteredBinding(RoomEncounterActorBinding candidate)
        {
            if (candidate == null || !_bindingsByKey.TryGetValue(candidate.SpawnKey, out RoomEncounterActorBinding registered))
                return null;
            return registered;
        }

        private bool AuditLiveBindings()
        {
            foreach (RoomEncounterActorBinding binding in _bindings)
            {
                if (binding.Phase == RoomEncounterActorBindingPhase.DefeatObserved ||
                    binding.Phase == RoomEncounterActorBindingPhase.DefeatAccepted)
                    continue;
                if (binding.Phase == RoomEncounterActorBindingPhase.Live && !binding.IsExactLiveOwnedActor())
                {
                    _roster?.TryRecordUnexpectedLiveRemoval(binding.SpawnKey, binding.ActorId);
                    Fault("A live actor was disabled, destroyed, reparented, replaced, or lost its initialized physics identity.");
                    return false;
                }
                if (binding.Phase == RoomEncounterActorBindingPhase.Activating)
                {
                    Fault("An actor remained in an incomplete activation phase after the synchronous wave arm.");
                    return false;
                }
            }

            return true;
        }

        private bool PublishClearOnce(long drainSequence, RoomEncounterRuntimeSessionSnapshot snapshot)
        {
            if (_clearNotificationIssued || _status != RoomEncounterRuntimeStatus.Armed ||
                _roster == null || !_roster.IsCleared || _context == null)
                return false;

            if (!CanArmOrDrain(snapshot) || !IsPhysicsSimulationAdmitted() || !_context.IsCurrentRoomScope() ||
                _context.Player == null || _context.Player.IsDead || _context.Player.CurrentHealth <= 0 ||
                _bindings.Count != CountPlannedSpawns() || _bindings.Any(binding => !binding.IsDefeatAccepted))
            {
                Fault("The roster was cleared without a final exact-scope, live-session, defeated-roster audit.");
                return false;
            }

            var observations = new List<RoomEncounterDefeatedActorObservation>(_bindings.Count);
            foreach (ExpeditionEncounterWavePlan wave in _context.Plan.Roster.Waves)
            {
                foreach (string spawnKey in wave.SpawnKeys)
                {
                    if (!_bindingsByKey.TryGetValue(spawnKey, out RoomEncounterActorBinding binding) ||
                        !binding.IsDefeatAccepted)
                    {
                        Fault("A planned spawn is missing from the immutable clear observation.");
                        return false;
                    }
                    observations.Add(new RoomEncounterDefeatedActorObservation(spawnKey, wave.WaveNumber,
                        binding.ActorId, binding.CapturedSequence, binding.ProcessedDrainSequence,
                        binding.CapturedFrame, binding.ProcessedDrainFrame,
                        binding.CapturedFixedTime, binding.ProcessedFixedTime));
                }
            }

            var observation = new RoomEncounterClearObservation(_context, observations);
            _clearNotificationIssued = true;
            _lastClearObservation = observation;
            long callbackGeneration = _callbackGeneration;
            RoomEncounterRuntimeContext callbackContext = _context;
            try
            {
                _clearObserver.OnRoomEncounterCleared(observation);
                if (_status != RoomEncounterRuntimeStatus.Armed ||
                    _callbackGeneration != callbackGeneration || !ReferenceEquals(_context, callbackContext))
                    return false;
                _status = RoomEncounterRuntimeStatus.Cleared;
                return true;
            }
            catch (Exception exception)
            {
                if (_status != RoomEncounterRuntimeStatus.Armed ||
                    _callbackGeneration != callbackGeneration || !ReferenceEquals(_context, callbackContext))
                    return false;
                _status = RoomEncounterRuntimeStatus.Faulted;
                _faultReason = "The local clear observer failed after the one-shot latch: " + exception.GetType().Name + ".";
                return false;
            }
        }

        private bool CanArmOrDrain(RoomEncounterRuntimeSessionSnapshot snapshot)
        {
            RoomEncounterRuntimeContext context = _context;
            PlayerMotor2D player = context == null ? null : context.Player;
            return context != null && snapshot.Matches(context) && snapshot.CanArmOrDrain &&
                context.IsCurrentRoomScope() && player != null && player.isActiveAndEnabled &&
                player.ActorId.IsValid && player.CombatStats != null && !player.IsDead &&
                player.CurrentHealth > 0;
        }

        private bool IsPhysicsSimulationAdmitted()
        {
            return Time.timeScale > 0f && Physics2D.simulationMode == SimulationMode2D.FixedUpdate;
        }

        private SnapshotReadResult TryReadSnapshot(out RoomEncounterRuntimeSessionSnapshot snapshot)
        {
            snapshot = default;
            if (_context == null || !_context.IsCurrentRoomScope()) return SnapshotReadResult.ContextMismatch;
            IRoomEncounterRuntimeSessionSnapshotSource source = _sessionSource;
            if (source == null || !source.TryCapture(_context, out snapshot)) return SnapshotReadResult.Unavailable;
            if (!snapshot.Matches(_context)) return SnapshotReadResult.ContextMismatch;
            PlayerMotor2D player = _context.Player;
            if (!snapshot.IsSessionActive || player == null || player.IsDead)
                return SnapshotReadResult.SessionEnded;
            // Initial RoomActivated intentionally precedes BindFromCheckpoint. Zero health on an unbound
            // PlayerMotor is not evidence of death and must remain NotReady/WaitingForSession.
            if (snapshot.HasBoundPlayer && player.CurrentHealth <= 0)
                return SnapshotReadResult.SessionEnded;
            return SnapshotReadResult.Available;
        }

        private int CountPlannedSpawns()
        {
            if (_context == null || _context.Plan == null || _context.Plan.Roster == null) return 0;
            return _context.Plan.Roster.Waves.Sum(wave => wave.SpawnKeys.Count);
        }

        private void InhibitForEndedSession()
        {
            if (_status == RoomEncounterRuntimeStatus.Disposed || _status == RoomEncounterRuntimeStatus.Cleared) return;
            _status = RoomEncounterRuntimeStatus.SessionEnded;
            _faultReason = "The expedition session or player ended before local clear publication.";
            _facts.Clear();
            foreach (RoomEncounterActorBinding binding in _bindings) binding.Dispose();
        }

        private void Fault(string reason)
        {
            if (_status == RoomEncounterRuntimeStatus.Disposed || _status == RoomEncounterRuntimeStatus.Cleared ||
                _status == RoomEncounterRuntimeStatus.SessionEnded || _status == RoomEncounterRuntimeStatus.Faulted)
                return;

            _status = RoomEncounterRuntimeStatus.Faulted;
            _faultReason = string.IsNullOrWhiteSpace(reason) ? "Encounter runtime failed closed." : reason;
            _facts.Clear();
            foreach (RoomEncounterActorBinding binding in _bindings)
            {
                binding.Dispose();
                if (binding.Root != null && binding.Root.activeSelf)
                    binding.Root.SetActive(false);
            }
        }

        private bool IsOnMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
        }

        internal void NotifyDriverUnavailable(RoomEncounterDriver2D driver)
        {
            if (!IsOnMainThread() || !ReferenceEquals(driver, _driver) ||
                _status == RoomEncounterRuntimeStatus.Disposed || _status == RoomEncounterRuntimeStatus.Cleared ||
                _status == RoomEncounterRuntimeStatus.Faulted || _status == RoomEncounterRuntimeStatus.SessionEnded)
                return;
            Fault("The scope-owned post-physics driver became unavailable.");
        }

        public void Dispose()
        {
            if (_status == RoomEncounterRuntimeStatus.Disposed) return;

            // Invalidate the epoch and publication path before removing event handlers or the scope's actors.
            _status = RoomEncounterRuntimeStatus.Disposed;
            _callbackGeneration = RoomEncounterRuntimeContext.NextGeneration();
            _facts.Clear();
            RoomEncounterDriver2D driver = _driver;
            _driver = null;
            if (driver != null) driver.Detach(this);
            foreach (RoomEncounterActorBinding binding in _bindings) binding.Dispose();
            _bindings.Clear();
            _bindingsByKey.Clear();
            _actorInstanceIds.Clear();
            ExpeditionEncounterRosterState roster = _roster;
            _roster = null;
            roster?.Dispose();
            _lastClearObservation = null;
            _sessionSource = null;
            _clearObserver = null;
            _context = null;
        }

        private enum SnapshotReadResult
        {
            Available,
            Unavailable,
            ContextMismatch,
            SessionEnded
        }
    }
}
