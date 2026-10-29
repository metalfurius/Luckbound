using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Core.Input;
using Luckbound.Gameplay.Combat;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = Luckbound.Core.Identifiers.EntityId;
using Object = UnityEngine.Object;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RoomEncounterRuntimePhysicalTests
    {
        private static readonly List<PhysicalFixture> Fixtures = new List<PhysicalFixture>();

        [UnityTearDown]
        public IEnumerator DisposeFixturesAfterEveryTest()
        {
            for (int index = Fixtures.Count - 1; index >= 0; index--)
                Fixtures[index].Dispose();
            Fixtures.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealProjectileDamageClearsTwoExplicitFixtureWavesOnlyAfterPostPhysicsDrain()
        {
            PhysicalFixture fixture = CreateFixture("F09_RuntimePhysicalTwoWaves");
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));

            RoomEncounterActorBinding first = fixture.Runtime.RegisteredBindings.Single();
            Assert.That(first.SpawnKey, Does.StartWith("fixture."));
            Assert.That(first.WaveNumber, Is.EqualTo(1));
            Assert.That(first.WasInactiveBeforeOwnership, Is.True);
            Assert.That(first.Root.activeSelf, Is.True);
            Assert.That(fixture.Controller.ActiveScope.GetComponentsInChildren<Slime2D>(true).Length, Is.EqualTo(1),
                "The complete plan includes wave two, but its actor stays unmaterialized until wave one is defeated.");
            Assert.That(fixture.Observer.Count, Is.Zero);

            RoomEncounterRuntimeContext context = fixture.Context;
            fixture.Source.Current = CreateReadySnapshot(context, isSimulationPaused: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            AssertArmedBindingUnchanged(fixture, first, first.Root, first.ActorId);
            fixture.Source.Current = CreateReadySnapshot(context);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
            AssertArmedBindingUnchanged(fixture, first, first.Root, first.ActorId);

            // Real ProjectilePool2D rentals and Physics2D collisions apply a nonlethal hit followed by the lethal hit.
            fixture.SpawnProjectile(1m);
            yield return WaitForImpact(first.Slime, expectedImpactCount: 1);
            Assert.That(first.Slime.CurrentHealth, Is.EqualTo(Slime2D.MaximumHealth - 1));
            Assert.That(first.PhysicalDeathObserved, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero);

            fixture.Source.Current = CreateReadySnapshot(context, hasPendingSimulationLeaseOrGate: true);
            fixture.SpawnProjectile(Slime2D.MaximumHealth - 1m);
            yield return WaitForPhysicalDeath(first.Slime);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(first.CombatCollider.enabled, Is.False);
            Assert.That(first.Body.simulated, Is.False,
                "Slime2D disables its combat collider and Rigidbody2D simulation before raising Defeated.");
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(fixture.Runtime.PendingFactCount, Is.EqualTo(1));
            Assert.That(fixture.Runtime.CompletedWaveCount, Is.Zero);
            Assert.That(fixture.Observer.Count, Is.Zero);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False,
                "A repeated confirmation during a pending gate must preserve Armed and the queued physical death.");
            AssertArmedBindingUnchanged(fixture, first, first.Root, first.ActorId,
                RoomEncounterActorBindingPhase.DefeatObserved);
            Assert.That(fixture.Runtime.PendingFactCount, Is.EqualTo(1));

            fixture.Source.Current = CreateReadySnapshot(context, isSimulationPaused: true);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            for (int frame = 0; frame < 2; frame++) yield return null;
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(fixture.Runtime.PendingFactCount, Is.EqualTo(1),
                "A queued death remains recoverable while simulation readiness is paused.");

            fixture.Source.Available = false;
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.False);
            for (int frame = 0; frame < 2; frame++) yield return null;
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(fixture.Runtime.PendingFactCount, Is.EqualTo(1));
            fixture.Source.Available = true;
            fixture.Source.Current = CreateReadySnapshot(context);
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
            Assert.That(fixture.Runtime.PendingFactCount, Is.EqualTo(1),
                "Readiness confirmation does not drain synchronously or replace the existing actor/queue.");
            for (int frame = 0; frame < 10 && fixture.Runtime.CompletedWaveCount < 1; frame++) yield return null;
            Assert.That(fixture.Runtime.CompletedWaveCount, Is.EqualTo(1));
            Assert.That(first.Slime.IsDead, Is.True);
            Assert.That(first.Slime.CurrentHealth, Is.Zero);
            Assert.That(first.PhysicalDeathObserved, Is.True);
            Assert.That(first.IsDefeatAccepted, Is.True);
            Assert.That(first.CombatCollider.enabled, Is.False);
            Assert.That(first.Body.simulated, Is.False);
            Assert.That(fixture.Observer.Count, Is.Zero,
                "A clear from the first wave cannot publish while a planned later wave remains.");
            Assert.That(fixture.Runtime.CurrentWaveNumber, Is.EqualTo(2));
            Assert.That(fixture.Runtime.RegisteredBindings.Count, Is.EqualTo(2));

            RoomEncounterActorBinding second = fixture.Runtime.RegisteredBindings.Single(binding => binding.WaveNumber == 2);
            Assert.That(second.ActorId, Is.Not.EqualTo(first.ActorId));
            Assert.That(second.Phase, Is.EqualTo(RoomEncounterActorBindingPhase.Live));
            Assert.That(fixture.Controller.ActiveScope.GetComponentsInChildren<Slime2D>(true).Length, Is.EqualTo(2));

            fixture.SpawnProjectile(1m);
            yield return WaitForImpact(second.Slime, expectedImpactCount: 1);
            Assert.That(second.Slime.CurrentHealth, Is.EqualTo(Slime2D.MaximumHealth - 1));
            Assert.That(fixture.Observer.Count, Is.Zero);

            fixture.SpawnProjectile(Slime2D.MaximumHealth - 1m);
            yield return WaitForDeathAndDrain(second.Slime, fixture.Runtime, expectedCompletedWaves: 2,
                clearObserver: fixture.Observer);

            Assert.That(second.Slime.IsDead, Is.True);
            Assert.That(second.IsDefeatAccepted, Is.True);
            Assert.That(second.CombatCollider.enabled, Is.False);
            Assert.That(second.Body.simulated, Is.False);
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Cleared));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.True);
            Assert.That(fixture.Observer.Count, Is.EqualTo(1));
            Assert.That(fixture.Observer.Last, Is.Not.Null);
            Assert.That(fixture.Observer.Last.RunId, Is.EqualTo(fixture.RunId));
            Assert.That(fixture.Observer.Last.RoomInstanceId, Is.EqualTo(fixture.Controller.CurrentRoomId));
            Assert.That(ReferenceEquals(fixture.Observer.Last.Scope, fixture.Controller.ActiveScope), Is.True);
            Assert.That(fixture.Observer.Last.Actors.Count, Is.EqualTo(2));
            Assert.That(fixture.Observer.Last.Actors.All(actor => actor.SpawnKey.StartsWith("fixture.", StringComparison.Ordinal)), Is.True);
            Assert.That(fixture.Observer.Last.Actors.All(actor => actor.ProcessedDrainSequence > actor.CapturedSequence), Is.True);
            Assert.That(fixture.Observer.Last.Actors.All(actor => actor.ProcessedFixedTime >= actor.CapturedFixedTime), Is.True,
                "The local observation is made by LateUpdate after the physical callback batch that captured each defeat.");

            // Synthetic duplicate adapter callbacks are rejected after acceptance; no Defeated event is invoked.
            first.HandleDefeated(first.ActorId);
            second.HandleDefeated(second.ActorId);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(fixture.Observer.Count, Is.EqualTo(1), "Repeated drains and callbacks cannot re-notify clear.");

            fixture.Dispose();
            yield return null;
        }

        [UnityTest]
        public IEnumerator LiveSlimePhysicsLossFaultsButTerminalDeathWithDisabledPhysicsIsAccepted()
        {
            foreach (bool loseCollider in new[] { true, false })
            {
                PhysicalFixture fixture = CreateFixture(loseCollider
                    ? "F09_RuntimeLiveColliderLoss" : "F09_RuntimeLiveBodySimulationLoss");
                Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
                RoomEncounterActorBinding binding = fixture.Runtime.RegisteredBindings.Single();
                if (loseCollider)
                    binding.CombatCollider.enabled = false;
                else
                    binding.Body.simulated = false;

                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
                Assert.That(fixture.Runtime.ClearNotificationIssued, Is.False);
                Assert.That(fixture.Observer.Count, Is.Zero);
                fixture.Dispose();
                yield return null;
            }

            // The two-wave test above exercises the positive terminal case through actual projectile collisions.
            yield return null;
        }

        [UnityTest]
        public IEnumerator ClearObserverDisposeReentrancyIsPreservedAndObserverFailureStaysFaulted()
        {
            foreach (RecordingObserver.Behavior behavior in new[]
            {
                RecordingObserver.Behavior.DisposeAndReturn,
                RecordingObserver.Behavior.DisposeAndThrow,
                RecordingObserver.Behavior.ThrowOnly
            })
            {
                PhysicalFixture fixture = CreateFixture("F09_RuntimeObserver_" + behavior);
                Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
                fixture.Observer.Runtime = fixture.Runtime;
                fixture.Observer.CallbackBehavior = behavior;
                fixture.Observer.ReenterDrainBeforeCallbackAction = true;

                yield return CompleteTwoWaveCombat(fixture);

                Assert.That(fixture.Observer.Count, Is.EqualTo(1));
                Assert.That(fixture.Observer.Last, Is.Not.Null);
                Assert.That(fixture.Runtime.ClearNotificationIssued, Is.True);
                if (behavior == RecordingObserver.Behavior.ThrowOnly)
                {
                    Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
                    Assert.That(fixture.Runtime.LastClearObservation, Is.Not.Null,
                        "A non-disposing observer failure keeps the one-shot observation for diagnosis.");
                    Assert.That(fixture.Runtime.FaultReason, Does.Contain("observer failed"));
                }
                else
                {
                    Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Disposed));
                    Assert.That(fixture.Runtime.IsDisposed, Is.True);
                    Assert.That(fixture.Runtime.CallbackGeneration,
                        Is.Not.EqualTo(fixture.Observer.Last.ActivationGeneration));
                    Assert.That(fixture.Runtime.LastClearObservation, Is.Null,
                        "Dispose clears runtime-owned observations after the observer has retained its immutable copy.");
                }

                for (int frame = 0; frame < 3; frame++) yield return null;
                Assert.That(fixture.Observer.Count, Is.EqualTo(1), "Reentrant and later drains cannot reissue clear.");
                fixture.Dispose();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ClearObserverDisablingDriverPreservesReentrantFault()
        {
            PhysicalFixture fixture = CreateFixture("F09_RuntimeObserver_DisableDriver");
            Assert.That(fixture.Runtime.ConfirmSessionReadyForScope(), Is.True);
            fixture.Observer.Runtime = fixture.Runtime;
            fixture.Observer.Driver = fixture.Context.Scope.GetComponent<RoomEncounterDriver2D>();
            fixture.Observer.CallbackBehavior = RecordingObserver.Behavior.DisableDriverAndReturn;

            yield return CompleteTwoWaveCombat(fixture);

            Assert.That(fixture.Observer.Count, Is.EqualTo(1));
            Assert.That(fixture.Runtime.ClearNotificationIssued, Is.True);
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Faulted));
            Assert.That(fixture.Runtime.IsDisposed, Is.False);
            Assert.That(fixture.Runtime.FaultReason, Does.Contain("driver became unavailable"));
            Assert.That(fixture.Runtime.LastClearObservation, Is.Not.Null);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(fixture.Observer.Count, Is.EqualTo(1),
                "The reentrant driver failure remains terminal and cannot publish another clear.");

            fixture.Dispose();
            yield return null;
        }

        private static IEnumerator CompleteTwoWaveCombat(PhysicalFixture fixture)
        {
            RoomEncounterActorBinding first = fixture.Runtime.RegisteredBindings.Single(binding => binding.WaveNumber == 1);
            fixture.SpawnProjectile(1m);
            yield return WaitForImpact(first.Slime, expectedImpactCount: 1);
            fixture.SpawnProjectile(Slime2D.MaximumHealth - 1m);
            yield return WaitForDeathAndDrain(first.Slime, fixture.Runtime, expectedCompletedWaves: 1);

            RoomEncounterActorBinding second = fixture.Runtime.RegisteredBindings.Single(binding => binding.WaveNumber == 2);
            fixture.SpawnProjectile(1m);
            yield return WaitForImpact(second.Slime, expectedImpactCount: 1);
            fixture.SpawnProjectile(Slime2D.MaximumHealth - 1m);
            yield return WaitForPhysicalDeath(second.Slime);
            for (int frame = 0; frame < 10 && fixture.Observer.Count == 0; frame++) yield return null;
            Assert.That(fixture.Observer.Count, Is.EqualTo(1));
        }

        private static void AssertArmedBindingUnchanged(PhysicalFixture fixture,
            RoomEncounterActorBinding binding, GameObject root, ActorId actorId,
            RoomEncounterActorBindingPhase expectedPhase = RoomEncounterActorBindingPhase.Live)
        {
            Assert.That(fixture.Runtime.Status, Is.EqualTo(RoomEncounterRuntimeStatus.Armed));
            Assert.That(fixture.Runtime.RegisteredBindings.Count, Is.EqualTo(1));
            Assert.That(ReferenceEquals(fixture.Runtime.RegisteredBindings[0], binding), Is.True);
            Assert.That(ReferenceEquals(binding.Root, root), Is.True);
            Assert.That(binding.ActorId, Is.EqualTo(actorId));
            Assert.That(binding.Phase, Is.EqualTo(expectedPhase));
        }

        private static IEnumerator WaitForImpact(Slime2D slime, int expectedImpactCount)
        {
            for (int tick = 0; tick < 60 && slime.ProjectileImpactCount < expectedImpactCount; tick++)
                yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(slime.ProjectileImpactCount, Is.GreaterThanOrEqualTo(expectedImpactCount));
        }

        private static IEnumerator WaitForDeathAndDrain(Slime2D slime, RoomEncounterRuntime runtime,
            int expectedCompletedWaves, RecordingObserver clearObserver = null)
        {
            for (int tick = 0; tick < 60 && !slime.IsDead; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(slime.IsDead, Is.True, "The actor must reach Dead from the real projectile/impact pipeline.");
            for (int frame = 0; frame < 10 && runtime.CompletedWaveCount < expectedCompletedWaves; frame++)
                yield return null;
            Assert.That(runtime.CompletedWaveCount, Is.EqualTo(expectedCompletedWaves));
            if (clearObserver != null)
            {
                for (int frame = 0; frame < 10 && clearObserver.Count == 0; frame++) yield return null;
                Assert.That(clearObserver.Count, Is.EqualTo(1));
            }
        }

        private static IEnumerator WaitForPhysicalDeath(Slime2D slime)
        {
            for (int tick = 0; tick < 60 && !slime.IsDead; tick++)
                yield return new WaitForFixedUpdate();
            Assert.That(slime.IsDead, Is.True, "The actor must reach Dead from the real projectile/impact pipeline.");
        }

        private static PhysicalFixture CreateFixture(string name)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            Assert.That(groundLayer, Is.GreaterThanOrEqualTo(0));
            GameObject floor = new GameObject(name + "_Ground");
            floor.layer = groundLayer;
            floor.transform.position = new Vector2(0f, -0.5f);
            BoxCollider2D floorCollider = floor.AddComponent<BoxCollider2D>();
            floorCollider.size = new Vector2(60f, 1f);

            ActorId playerActorId = ActorId.New();
            var input = new PlayerInputBuffer(playerActorId);
            var playerObject = new GameObject(name + "_Player");
            Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
            playerBody.gravityScale = 3f;
            playerBody.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D playerCollider = playerObject.AddComponent<CapsuleCollider2D>();
            playerCollider.direction = CapsuleDirection2D.Vertical;
            playerCollider.size = new Vector2(0.75f, 1.5f);
            PlayerMotor2D player = playerObject.AddComponent<PlayerMotor2D>();
            player.Initialize(playerActorId, input, 1 << groundLayer);

            GameObject roomPrefab = CreateRoomPrefab(name + "_RoomPrefab");
            TextAsset catalogSource = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
            Assert.That(catalogSource, Is.Not.Null);
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(17320508u,
                ExpeditionRouteCatalogJsonLoader.Load(catalogSource.text));
            RunId runId = RunId.New();
            var host = new GameObject(name + "_TransitionController");
            ExpeditionRoomTransitionController controller = host.AddComponent<ExpeditionRoomTransitionController>();
            controller.Initialize(runId, route, _ => roomPrefab, playerObject.transform);

            GameObject slimePrefab = CreateSlimePrefab(name + "_SlimePrefab");
            RoomEncounterRuntimePlan plan = CreateTwoWaveFixturePlan(name, slimePrefab, 1 << groundLayer);
            ExpeditionRouteNode currentNode = route.Nodes.Single(node => node.NodeId == controller.CurrentNodeId);
            RoomEncounterRuntimeContext context = RoomEncounterRuntimeContext.Create(runId,
                currentNode.NodeId, currentNode.TemplateId, controller.CurrentRoomId, controller.ActiveScope,
                controller, player, plan, route.GenerationVersion);
            var source = new MutableSnapshotSource
            {
                Current = CreateReadySnapshot(context)
            };
            var observer = new RecordingObserver();
            RoomEncounterRuntime runtime = RoomEncounterRuntime.Prepare(context, source, observer);
            observer.Runtime = runtime;

            ProjectileFixture projectileFixture = CreateProjectileFixture(name + "_Projectiles");
            Physics2D.SyncTransforms();
            var fixture = new PhysicalFixture(name, floor, playerObject, roomPrefab, slimePrefab,
                host, controller, player, route, runId, runtime, observer, projectileFixture, context, source);
            Fixtures.Add(fixture);
            return fixture;
        }

        private static GameObject CreateRoomPrefab(string name)
        {
            var prefab = new GameObject(name);
            var spawn = new GameObject("FixturePlayerSpawn");
            spawn.transform.SetParent(prefab.transform, false);
            spawn.transform.localPosition = new Vector3(20f, 1.5f, 0f);
            spawn.AddComponent<RoomPositionMarker2D>().Configure("fixture.player", "player_spawn", 20, 2);
            return prefab;
        }

        private static GameObject CreateSlimePrefab(string name)
        {
            var prefab = new GameObject(name);
            Rigidbody2D body = prefab.AddComponent<Rigidbody2D>();
            body.gravityScale = Slime2D.GravityScale;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            CapsuleCollider2D collider = prefab.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Vertical;
            collider.size = new Vector2(0.9f, 0.8f);
            prefab.AddComponent<Slime2D>();
            prefab.SetActive(false);
            return prefab;
        }

        private static RoomEncounterRuntimePlan CreateTwoWaveFixturePlan(string fixtureName,
            GameObject slimePrefab, LayerMask groundLayers)
        {
            string firstKey = "fixture." + fixtureName + ".wave1.slime";
            string secondKey = "fixture." + fixtureName + ".wave2.slime";
            var roster = new ExpeditionEncounterRosterPlan(new[]
            {
                new ExpeditionEncounterWavePlan(1, new[] { firstKey }),
                new ExpeditionEncounterWavePlan(2, new[] { secondKey })
            });
            var actors = new[]
            {
                new RoomEncounterWaveActorDefinition(1, new[]
                {
                    new RoomEncounterActorDefinition(firstKey, slimePrefab, groundLayers,
                        new Vector3(0f, 0.4f, 0f), Quaternion.identity)
                }),
                new RoomEncounterWaveActorDefinition(2, new[]
                {
                    new RoomEncounterActorDefinition(secondKey, slimePrefab, groundLayers,
                        new Vector3(0f, 0.4f, 0f), Quaternion.identity)
                })
            };
            return new RoomEncounterRuntimePlan(roster, actors);
        }

        private static ProjectileFixture CreateProjectileFixture(string name)
        {
            var poolObject = new GameObject(name + "_Pool");
            ProjectilePool2D pool = poolObject.AddComponent<ProjectilePool2D>();
            var prefab = new GameObject(name + "_Prefab");
            Rigidbody2D body = prefab.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
            collider.radius = 0.12f;
            prefab.AddComponent<Projectile2D>();
            prefab.SetActive(false);
            pool.Initialize(prefab);

            var scopeObject = new GameObject(name + "_RoomScope");
            RoomProjectileScope scope = scopeObject.AddComponent<RoomProjectileScope>();
            scope.Initialize(EntityId.New(), pool);
            return new ProjectileFixture(poolObject, prefab, scopeObject, pool, scope);
        }

        private static RoomEncounterRuntimeSessionSnapshot CreateReadySnapshot(RoomEncounterRuntimeContext context,
            bool isSimulationPaused = false, bool hasPendingSimulationLeaseOrGate = false)
        {
            return new RoomEncounterRuntimeSessionSnapshot(context.RunId, context.NodeId, context.TemplateId,
                context.RouteGenerationVersion, context.RoomInstanceId, context.Scope, context.Controller,
                context.Player, context.ActivationGeneration, isSessionActive: true,
                hasBoundPlayer: true, hasCheckpointBinding: true, hasPublishedSession: true,
                hasRequiredGameplayBindings: true, roomCommitAndActivationComplete: true,
                hasPendingSessionMutation: false, isRoomAdvanceInFlight: false,
                hasPendingCommitOrActivation: false,
                hasPendingSimulationLeaseOrGate: hasPendingSimulationLeaseOrGate,
                isSimulationPaused: isSimulationPaused);
        }

        private static void SpawnProjectile(ProjectileFixture fixture, ActorId sourceActorId, decimal damage)
        {
            fixture.Pool.Spawn(new ProjectileDefinition(baseDamage: damage,
                    speedUnitsPerSecond: 12m, lifetimeTicks: 120, maximumTargets: 1),
                EntityId.New(), sourceActorId, new Vector2(-2f, 0.4f), Vector2.right,
                1 << 0, fixture.Scope);
        }

        private sealed class MutableSnapshotSource : IRoomEncounterRuntimeSessionSnapshotSource
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

        private sealed class RecordingObserver : IRoomEncounterClearObserver
        {
            internal enum Behavior
            {
                None,
                DisposeAndReturn,
                DisposeAndThrow,
                ThrowOnly,
                DisableDriverAndReturn
            }

            internal int Count { get; private set; }
            internal RoomEncounterClearObservation Last { get; private set; }
            internal RoomEncounterRuntime Runtime { get; set; }
            internal RoomEncounterDriver2D Driver { get; set; }
            internal Behavior CallbackBehavior { get; set; }
            internal bool ReenterDrainBeforeCallbackAction { get; set; }

            public void OnRoomEncounterCleared(RoomEncounterClearObservation observation)
            {
                Count++;
                Last = observation;
                if (ReenterDrainBeforeCallbackAction) Runtime.DrainAfterPhysics();
                if (CallbackBehavior == Behavior.DisableDriverAndReturn)
                {
                    Driver.enabled = false;
                    return;
                }
                if (CallbackBehavior == Behavior.DisposeAndReturn || CallbackBehavior == Behavior.DisposeAndThrow)
                    Runtime.Dispose();
                if (CallbackBehavior == Behavior.DisposeAndThrow || CallbackBehavior == Behavior.ThrowOnly)
                    throw new InvalidOperationException("Synthetic observer failure for reentrancy coverage.");
            }
        }

        private sealed class ProjectileFixture
        {
            internal ProjectileFixture(GameObject poolObject, GameObject prefab, GameObject scopeObject,
                ProjectilePool2D pool, RoomProjectileScope scope)
            {
                PoolObject = poolObject;
                Prefab = prefab;
                ScopeObject = scopeObject;
                Pool = pool;
                Scope = scope;
            }

            internal GameObject PoolObject { get; }
            internal GameObject Prefab { get; }
            internal GameObject ScopeObject { get; }
            internal ProjectilePool2D Pool { get; }
            internal RoomProjectileScope Scope { get; }
            private bool _disposed;

            internal void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                Scope.Dispose();
                Object.Destroy(ScopeObject);
                Object.Destroy(PoolObject);
                Object.Destroy(Prefab);
            }
        }

        private sealed class PhysicalFixture
        {
            private readonly string _name;
            private readonly GameObject _floor;
            private readonly GameObject _playerObject;
            private readonly GameObject _roomPrefab;
            private readonly GameObject _slimePrefab;
            private readonly GameObject _host;
            private readonly ProjectileFixture _projectiles;
            private bool _disposed;

            internal PhysicalFixture(string name, GameObject floor, GameObject playerObject,
                GameObject roomPrefab, GameObject slimePrefab, GameObject host,
                ExpeditionRoomTransitionController controller, PlayerMotor2D player,
                ExpeditionRoutePlan route, RunId runId, RoomEncounterRuntime runtime,
                RecordingObserver observer, ProjectileFixture projectiles,
                RoomEncounterRuntimeContext context, MutableSnapshotSource source)
            {
                _name = name;
                _floor = floor;
                _playerObject = playerObject;
                _roomPrefab = roomPrefab;
                _slimePrefab = slimePrefab;
                _host = host;
                Controller = controller;
                Player = player;
                Route = route;
                RunId = runId;
                Runtime = runtime;
                Observer = observer;
                _projectiles = projectiles;
                Context = context;
                Source = source;
            }

            internal ExpeditionRoomTransitionController Controller { get; }
            internal PlayerMotor2D Player { get; }
            internal ExpeditionRoutePlan Route { get; }
            internal RunId RunId { get; }
            internal RoomEncounterRuntime Runtime { get; }
            internal RecordingObserver Observer { get; }
            internal RoomEncounterRuntimeContext Context { get; }
            internal MutableSnapshotSource Source { get; }

            internal void SpawnProjectile(decimal damage)
            {
                RoomEncounterRuntimePhysicalTests.SpawnProjectile(_projectiles, Player.ActorId, damage);
            }

            internal void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (Controller != null) Controller.Dispose();
                _projectiles.Dispose();
                if (_host != null) Object.Destroy(_host);
                if (_playerObject != null) Object.Destroy(_playerObject);
                if (_roomPrefab != null) Object.Destroy(_roomPrefab);
                if (_slimePrefab != null) Object.Destroy(_slimePrefab);
                if (_floor != null) Object.Destroy(_floor);
            }
        }
    }
}
