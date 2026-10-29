using System;
using System.Collections;
using System.Collections.Generic;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RoomEncounterRuntimeLifecycleTests
    {
        private static readonly List<RuntimeFixture> Fixtures = new List<RuntimeFixture>();
        private static readonly List<GameObject> ExtraObjects = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator DisposeFixturesAfterEveryTest()
        {
            for (int index = Fixtures.Count - 1; index >= 0; index--)
                Fixtures[index].Dispose(destroyPlayer: false);
            var destroyedPlayerIds = new HashSet<UnityEngine.EntityId>();
            var destroyedPlayerReferences = new List<GameObject>();
            foreach (RuntimeFixture fixture in Fixtures)
            {
                GameObject playerObject = fixture.PlayerObject;
                if (playerObject == null) continue;

                UnityEngine.EntityId playerEntityId = playerObject.GetEntityId();
                bool isFirstOccurrence;
                if (playerEntityId.IsValid())
                {
                    isFirstOccurrence = destroyedPlayerIds.Add(playerEntityId);
                }
                else
                {
                    isFirstOccurrence = true;
                    foreach (GameObject destroyedPlayer in destroyedPlayerReferences)
                    {
                        if (!ReferenceEquals(destroyedPlayer, playerObject)) continue;
                        isFirstOccurrence = false;
                        break;
                    }
                    if (isFirstOccurrence) destroyedPlayerReferences.Add(playerObject);
                }

                if (isFirstOccurrence) fixture.DestroyPlayer();
            }
            Fixtures.Clear();
            for (int index = ExtraObjects.Count - 1; index >= 0; index--)
                if (ExtraObjects[index] != null) Object.Destroy(ExtraObjects[index]);
            ExtraObjects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitialRoomActivationBeforeCheckpointBindingKeepsFixtureActorsInactive()
        {
            RuntimeFixture fixture = CreateFixture("F09_RuntimeInitialBind", playerInitialized: false, waves: 1);
            RoomEncounterRuntimeContext context = fixture.CreateContext();
            var observer = new RecordingObserver();
            var source = new MutableSnapshotSource
            {
                Current = CreateSnapshot(context, ready: false)
            };

            fixture.Runtime = RoomEncounterRuntime.Prepare(context, source, observer);
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.WaitingForSession));
            Assert.That(fixture.Runtime.RegisteredBindings, Is.Empty);
            Assert.That(fixture.Controller.ActiveScope.OwnedResourceCount, Is.EqualTo(1),
                "The exact runtime must be a resource of the active RoomLifetimeScope.");
            Assert.That(fixture.Controller.ActiveScope.GetComponentsInChildren<Slime2D>(true), Is.Empty,
                "RoomActivated can precede checkpoint binding; it must not create or arm actors.");
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False,
                "An unbound player/checkpoint snapshot cannot arm the first wave.");

            fixture.InitializePlayer();
            source.Current = CreateSnapshot(context, ready: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);

            IReadOnlyList<RoomEncounterActorBinding> bindings = fixture.Runtime.RegisteredBindings;
            Assert.That(bindings.Count, Is.EqualTo(1));
            Assert.That(bindings[0].Phase, Is.EqualTo(RoomEncounterActorBindingPhase.Live));
            Assert.That(bindings[0].WasInactiveBeforeOwnership, Is.True,
                "The actor root must have activeSelf=false before OwnActor can reparent it under the active scope.");
            Assert.That(bindings[0].Root.activeSelf, Is.True);
            Assert.That(observer.Count, Is.Zero);

            fixture.Dispose();
            yield return null;
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Disposed));
        }

        [UnityTest]
        public IEnumerator DisablingOnlyTheLiveSlimeComponentFailsClosedAndNeverPublishesClear()
        {
            RuntimeFixture fixture = CreateReadyFixture("F09_RuntimeDisableSlime");
            RoomEncounterActorBinding binding = fixture.Runtime.RegisteredBindings[0];
            Assert.That(binding.Slime.IsDead, Is.False);
            Assert.That(binding.Slime.CurrentHealth, Is.GreaterThan(0));

            binding.Slime.enabled = false;
            for (int frame = 0; frame < 3; frame++) yield return null;

            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero,
                "BecameUnavailable means the living actor was lost; it never means defeat.");

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReconfirmingArmedRuntimeAcrossTemporaryReadinessGapsKeepsExactActors()
        {
            RuntimeFixture fixture = CreateReadyFixture("F09_RuntimeReadinessIdempotent");
            RoomEncounterActorBinding binding = fixture.Runtime.RegisteredBindings[0];
            GameObject root = binding.Root;
            ActorId actorId = binding.ActorId;

            fixture.Source.Current = CreateSnapshot(fixture.Context, ready: true, paused: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            AssertArmedBindingUnchanged(fixture, binding, root, actorId);
            fixture.Source.Current = CreateSnapshot(fixture.Context, ready: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
            AssertArmedBindingUnchanged(fixture, binding, root, actorId);

            fixture.Source.Current = CreateSnapshot(fixture.Context, ready: true, gatePending: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            AssertArmedBindingUnchanged(fixture, binding, root, actorId);
            fixture.Source.Available = false;
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            AssertArmedBindingUnchanged(fixture, binding, root, actorId);

            fixture.Source.Available = true;
            fixture.Source.Current = CreateSnapshot(fixture.Context, ready: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
            AssertArmedBindingUnchanged(fixture, binding, root, actorId);
            Assert.That(fixture.Observer.Count, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotFromAnotherActivationGenerationFailsClosed()
        {
            RuntimeFixture fixture = CreateReadyFixture("F09_RuntimeReadinessWrongGeneration");
            RoomEncounterRuntimeContext otherActivation = fixture.CreateDifferentActivationContext();
            Assert.That(otherActivation.ActivationGeneration, Is.Not.EqualTo(fixture.Context.ActivationGeneration));

            fixture.Source.Current = CreateSnapshot(otherActivation, ready: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
            Assert.That(fixture.Runtime.FaultReason, Does.Contain("snapshot no longer matches"));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReparentingALiveOwnedRootFailsClosedBeforeAnyClear()
        {
            RuntimeFixture fixture = CreateReadyFixture("F09_RuntimeReparentActor");
            RoomEncounterActorBinding binding = fixture.Runtime.RegisteredBindings[0];
            var outside = new GameObject("F09_RuntimeOutsideActorParent");
            ExtraObjects.Add(outside);

            binding.Root.transform.SetParent(outside.transform, true);
            for (int frame = 0; frame < 3; frame++) yield return null;

            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero);

            Object.Destroy(outside);
            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingTheLiveRootFailsClosedWithoutTurningItsRemovalIntoDefeat()
        {
            RuntimeFixture fixture = CreateReadyFixture("F09_RuntimeDisableRoot");
            RoomEncounterActorBinding binding = fixture.Runtime.RegisteredBindings[0];
            binding.Root.SetActive(false);
            for (int frame = 0; frame < 3; frame++) yield return null;

            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyingOnlyTheLiveSlimeComponentFailsClosed()
        {
            RuntimeFixture fixture = CreateReadyFixture("F09_RuntimeDestroyComponent");
            RoomEncounterActorBinding binding = fixture.Runtime.RegisteredBindings[0];
            Object.Destroy(binding.Slime);
            for (int frame = 0; frame < 3; frame++) yield return null;

            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero);

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LateCallbackFromDisposedScopeACannotMutateScopeBWithSameLogicalRoomId()
        {
            RuntimeFixture fixtureA = CreateReadyFixture("F09_RuntimeScopeA");
            RoomEncounterActorBinding bindingA = fixtureA.Runtime.RegisteredBindings[0];
            RunId runId = fixtureA.RunId;
            RoomInstanceId logicalRoomId = fixtureA.Controller.CurrentRoomId;
            RoomLifetimeScope scopeA = fixtureA.Controller.ActiveScope;
            ExpeditionRoutePlan route = fixtureA.Route;
            GameObject roomPrefabB = CreateRoomPrefab("F09_RuntimeScopeB_RoomPrefab");
            PlayerMotor2D player = fixtureA.Player;

            fixtureA.Dispose(destroyPlayer: false);
            Assert.That(fixtureA.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Disposed));

            RuntimeFixture fixtureB = CreateFixture("F09_RuntimeScopeB", player, runId, route, roomPrefabB, waves: 1);
            fixtureB.ArmRuntime();
            RoomEncounterActorBinding bindingB = fixtureB.Runtime.RegisteredBindings[0];
            Assert.That(fixtureB.Controller.CurrentRoomId, Is.EqualTo(logicalRoomId));
            Assert.That(ReferenceEquals(fixtureB.Controller.ActiveScope, scopeA), Is.False);

            // Synthetic stale-handler invocation: this does not raise Slime2D.Defeated or claim a physical death.
            bindingA.HandleDefeated(bindingA.ActorId);

            Assert.That(fixtureB.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(fixtureB.Runtime.PendingFactCount, Is.Zero);
            Assert.That(bindingB.PhysicalDeathObserved, Is.False);
            Assert.That(fixtureB.Observer.Count, Is.Zero);
            Assert.That(fixtureB.Runtime.ClearNotificationIssued, Is.False);

            fixtureA.Dispose(destroyPlayer: false);
            fixtureB.Dispose();
            yield return null;
        }

        private static RuntimeFixture CreateReadyFixture(string name)
        {
            RuntimeFixture fixture = CreateFixture(name, playerInitialized: true, waves: 1);
            fixture.ArmRuntime();
            return fixture;
        }

        private static RuntimeFixture CreateFixture(string name, bool playerInitialized, int waves)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0));
            GameObject playerObject = CreatePlayer(name + "_Player", playerInitialized, groundLayer,
                out PlayerMotor2D player, out PlayerInputBuffer input);
            GameObject roomPrefab = CreateRoomPrefab(name + "_RoomPrefab");
            ExpeditionRoutePlan route = LoadRoute();
            RunId runId = RunId.New();
            var host = new GameObject(name + "_TransitionController");
            ExpeditionRoomTransitionController controller = host.AddComponent<ExpeditionRoomTransitionController>();
            controller.Initialize(runId, route, _ => roomPrefab, playerObject.transform);
            GameObject slimePrefab = CreateSlimePrefab(name + "_SlimePrefab");
            RoomEncounterRuntimePlan plan = CreatePlan(name, slimePrefab, 1 << groundLayer, waves);
            var fixture = new RuntimeFixture(name, host, controller, playerObject, player, input,
                roomPrefab, slimePrefab, route, runId, plan, groundLayer);
            Fixtures.Add(fixture);
            return fixture;
        }

        private static RuntimeFixture CreateFixture(string name, PlayerMotor2D existingPlayer,
            RunId runId, ExpeditionRoutePlan route, GameObject roomPrefab, int waves)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            GameObject playerObject = existingPlayer.gameObject;
            var host = new GameObject(name + "_TransitionController");
            ExpeditionRoomTransitionController controller = host.AddComponent<ExpeditionRoomTransitionController>();
            controller.Initialize(runId, route, _ => roomPrefab, playerObject.transform);
            GameObject slimePrefab = CreateSlimePrefab(name + "_SlimePrefab");
            RoomEncounterRuntimePlan plan = CreatePlan(name, slimePrefab, 1 << groundLayer, waves);
            var fixture = new RuntimeFixture(name, host, controller, playerObject, existingPlayer, null,
                roomPrefab, slimePrefab, route, runId, plan, groundLayer);
            Fixtures.Add(fixture);
            return fixture;
        }

        private static void AssertArmedBindingUnchanged(RuntimeFixture fixture,
            RoomEncounterActorBinding binding, GameObject root, ActorId actorId)
        {
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(fixture.Runtime.RegisteredBindings.Count, Is.EqualTo(1));
            Assert.That(ReferenceEquals(fixture.Runtime.RegisteredBindings[0], binding), Is.True);
            Assert.That(ReferenceEquals(binding.Root, root), Is.True);
            Assert.That(binding.ActorId, Is.EqualTo(actorId));
            Assert.That(binding.Phase, Is.EqualTo(RoomEncounterActorBindingPhase.Live));
        }

        private static GameObject CreatePlayer(string name, bool initialize, int groundLayer,
            out PlayerMotor2D motor, out PlayerInputBuffer input)
        {
            ActorId actorId = ActorId.New();
            input = new PlayerInputBuffer(actorId);
            var player = new GameObject(name);
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 3f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.75f, 1.5f);
            motor = player.AddComponent<PlayerMotor2D>();
            if (initialize) motor.Initialize(actorId, input, 1 << groundLayer);
            return player;
        }

        private static GameObject CreateRoomPrefab(string name)
        {
            var prefab = new GameObject(name);
            var markerObject = new GameObject("FixturePlayerSpawn");
            markerObject.transform.SetParent(prefab.transform, false);
            markerObject.transform.localPosition = new Vector3(20f, 1.5f, 0f);
            markerObject.AddComponent<RoomPositionMarker2D>().Configure("fixture.player", "player_spawn", 20, 2);
            return prefab;
        }

        private static GameObject CreateSlimePrefab(string name)
        {
            var prefab = new GameObject(name);
            Rigidbody2D body = prefab.AddComponent<Rigidbody2D>();
            body.gravityScale = Slime2D.GravityScale;
            CapsuleCollider2D collider = prefab.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.8f);
            prefab.AddComponent<Slime2D>();
            prefab.SetActive(false);
            return prefab;
        }

        private static RoomEncounterRuntimePlan CreatePlan(string fixtureName, GameObject slimePrefab,
            LayerMask groundLayers, int waveCount)
        {
            var rosterWaves = new List<ExpeditionEncounterWavePlan>();
            var actorWaves = new List<RoomEncounterWaveActorDefinition>();
            for (int wave = 1; wave <= waveCount; wave++)
            {
                string key = "fixture." + fixtureName + ".wave" + wave + ".slime";
                rosterWaves.Add(new ExpeditionEncounterWavePlan(wave, new[] { key }));
                actorWaves.Add(new RoomEncounterWaveActorDefinition(wave, new[]
                {
                    new RoomEncounterActorDefinition(key, slimePrefab, groundLayers,
                        new Vector3(0f, 0.4f, 0f), Quaternion.identity)
                }));
            }

            return new RoomEncounterRuntimePlan(new ExpeditionEncounterRosterPlan(rosterWaves), actorWaves);
        }

        private static ExpeditionRoutePlan LoadRoute()
        {
            TextAsset catalogSource = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
            Assert.That(catalogSource, Is.Not.Null);
            return ExpeditionRouteGenerator.Generate(17320508u,
                ExpeditionRouteCatalogJsonLoader.Load(catalogSource.text));
        }

        private static RoomEncounterRuntimeSessionSnapshot CreateSnapshot(RoomEncounterRuntimeContext context,
            bool ready, bool paused = false, bool gatePending = false)
        {
            return new RoomEncounterRuntimeSessionSnapshot(context.RunId, context.NodeId, context.TemplateId,
                context.RouteGenerationVersion, context.RoomInstanceId, context.Scope, context.Controller,
                context.Player, context.ActivationGeneration, isSessionActive: true,
                hasBoundPlayer: ready, hasCheckpointBinding: ready, hasPublishedSession: ready,
                hasRequiredGameplayBindings: ready, roomCommitAndActivationComplete: ready,
                hasPendingSessionMutation: false, isRoomAdvanceInFlight: false,
                hasPendingCommitOrActivation: false, hasPendingSimulationLeaseOrGate: gatePending,
                isSimulationPaused: paused);
        }

        internal sealed class MutableSnapshotSource : IRoomEncounterRuntimeSessionSnapshotSource
        {
            internal RoomEncounterRuntimeSessionSnapshot Current;
            internal bool Available = true;

            public bool TryCapture(RoomEncounterRuntimeContext context,
                out RoomEncounterRuntimeSessionSnapshot snapshot)
            {
                snapshot = Current;
                return Available && snapshot.IsInitialized;
            }
        }

        internal sealed class RecordingObserver : IRoomEncounterClearObserver
        {
            internal int Count { get; private set; }
            internal RoomEncounterClearObservation Last { get; private set; }

            public void OnRoomEncounterCleared(RoomEncounterClearObservation observation)
            {
                Count++;
                Last = observation;
            }
        }

        private sealed class RuntimeFixture
        {
            private readonly string _name;
            private readonly GameObject _host;
            private readonly GameObject _playerObject;
            private readonly PlayerInputBuffer _input;
            private readonly GameObject _slimePrefab;
            private readonly int _groundLayer;
            private readonly RoomEncounterRuntimePlan _plan;
            private RoomEncounterRuntimeContext _context;
            private bool _disposed;
            private bool _playerDestroyRequested;

            internal RuntimeFixture(string name, GameObject host,
                ExpeditionRoomTransitionController controller, GameObject playerObject,
                PlayerMotor2D player, PlayerInputBuffer input, GameObject roomPrefab,
                GameObject slimePrefab, ExpeditionRoutePlan route, RunId runId,
                RoomEncounterRuntimePlan plan, int groundLayer)
            {
                _name = name;
                _host = host;
                Controller = controller;
                _playerObject = playerObject;
                Player = player;
                _input = input;
                RoomPrefab = roomPrefab;
                _slimePrefab = slimePrefab;
                Route = route;
                RunId = runId;
                _plan = plan;
                _groundLayer = groundLayer;
                Observer = new RecordingObserver();
                Source = new MutableSnapshotSource();
            }

            internal ExpeditionRoomTransitionController Controller { get; }
            internal PlayerMotor2D Player { get; }
            internal GameObject PlayerObject => _playerObject;
            internal GameObject RoomPrefab { get; }
            internal ExpeditionRoutePlan Route { get; }
            internal RunId RunId { get; }
            internal RoomEncounterRuntimeContext Context => _context;
            internal RoomEncounterRuntime Runtime { get; set; }
            internal RecordingObserver Observer { get; }
            internal MutableSnapshotSource Source { get; }

            internal void InitializePlayer()
            {
                if (Player.CombatStats == null)
                    Player.Initialize(_input.ActorId, _input, 1 << _groundLayer);
            }

            internal RoomEncounterRuntimeContext CreateContext()
            {
                if (_context != null) return _context;
                string nodeId = Controller.CurrentNodeId;
                ExpeditionRouteNode node = null;
                foreach (ExpeditionRouteNode candidate in Route.Nodes)
                    if (string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal)) node = candidate;
                if (node == null) throw new InvalidOperationException("The fixture route node did not resolve.");
                _context = RoomEncounterRuntimeContext.Create(RunId, node.NodeId, node.TemplateId,
                    Controller.CurrentRoomId, Controller.ActiveScope, Controller, Player, _plan, Route.GenerationVersion);
                return _context;
            }

            internal RoomEncounterRuntimeContext CreateDifferentActivationContext()
            {
                RoomEncounterRuntimeContext current = CreateContext();
                return RoomEncounterRuntimeContext.Create(current.RunId, current.NodeId, current.TemplateId,
                    current.RoomInstanceId, current.Scope, current.Controller, current.Player,
                    current.Plan, current.RouteGenerationVersion);
            }

            internal void ArmRuntime()
            {
                RoomEncounterRuntimeContext context = CreateContext();
                Source.Current = CreateSnapshot(context, ready: true);
                Runtime = RoomEncounterRuntime.Prepare(context, Source, Observer);
                Assert.That(Runtime.ConfirmSessionReadyForScope(), Is.True);
            }

            internal void Dispose(bool destroyPlayer = true)
            {
                if (!_disposed)
                {
                    _disposed = true;
                    if (Controller != null) Controller.Dispose();
                    if (_host != null) Object.Destroy(_host);
                    if (RoomPrefab != null) Object.Destroy(RoomPrefab);
                    if (_slimePrefab != null) Object.Destroy(_slimePrefab);
                }
                if (destroyPlayer) DestroyPlayer();
            }

            internal void DestroyPlayer()
            {
                if (_playerDestroyRequested) return;
                _playerDestroyRequested = true;
                if (_playerObject != null) Object.Destroy(_playerObject);
            }
        }
    }
}
