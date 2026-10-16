using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RoomTransitionLifecycleTests
    {
        [UnityTest]
        public IEnumerator D03_TwentyRouteTransitionsRetireActorsSubscriptionsAndCoroutines()
        {
            const int scenarioSeed = 17320508;
            using (var evidence = new ScenarioEvidenceRecorder("D03", "D03_TwentyRoomTransitions", scenarioSeed))
            {
                TextAsset catalogSource = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
                Assert.That(catalogSource, Is.Not.Null);
                ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate((uint)scenarioSeed,
                    ExpeditionRouteCatalogJsonLoader.Load(catalogSource.text));

                var templatePrefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                int templateOrdinal = 0;
                foreach (string templateId in route.Nodes.Select(node => node.TemplateId).Distinct(StringComparer.Ordinal))
                {
                    templatePrefabs.Add(templateId, CreateRoomTemplate(templateId, 18 + templateOrdinal++));
                }

                var signalBus = new RoomSignalBus();
                var roomActivity = new List<RoomActivity>();
                RunId expeditionRunId = RunId.New();
                GameObject transitionHost = new GameObject("D03TransitionHost");
                ExpeditionRoomTransitionController controller = transitionHost.AddComponent<ExpeditionRoomTransitionController>();
                GameObject sessionPlayer = new GameObject("SessionPlayer");
                Rigidbody2D sessionPlayerBody = sessionPlayer.AddComponent<Rigidbody2D>();
                sessionPlayerBody.linearVelocity = new Vector2(2f, 1f);
                sessionPlayerBody.angularVelocity = 1f;
                controller.RoomActivated += scope =>
                {
                    var activity = new RoomActivity(scope.RoomInstanceId);
                    roomActivity.Add(activity);
                    activity.Actor = scope.OwnActor(new GameObject("RoomScopedActor." + scope.TemplateId));
                    scope.OwnSubscription(signalBus.Subscribe(() => activity.SignalCount++));
                    scope.StartOwnedCoroutine(CountFrames(activity));
                };

                controller.Initialize(expeditionRunId, route, templateId =>
                    templatePrefabs.TryGetValue(templateId, out GameObject prefab) ? prefab : null, sessionPlayer.transform);
                bool initialRoomIdentityIsStable = controller.CurrentRoomId ==
                    ExpeditionRoomIdentity.ForNode(expeditionRunId, controller.CurrentNodeId);
                bool transitionChecksPassed = initialRoomIdentityIsStable;
                evidence.RecordAssertion("A room ID is derived from the persisted expedition and route node",
                    initialRoomIdentityIsStable, ExpeditionRoomIdentity.ForNode(expeditionRunId, controller.CurrentNodeId).ToString(),
                    controller.CurrentRoomId.ToString());

                transitionChecksPassed &= controller.ActiveRoomCount == 1 && signalBus.SubscriberCount == 1 &&
                    controller.ActiveScope.OwnedActorCount == 1 && controller.ActiveScope.OwnedResourceCount == 1 &&
                    controller.ActiveScope.ActiveCoroutineCount == 1;
                evidence.RecordAssertion("The first route node has one active room owner", transitionChecksPassed,
                    "1 room, actor, subscription and coroutine", DescribeActiveState(controller, signalBus));
                RoomPositionMarker2D initialSpawn = controller.ActiveRoomRoot
                    .GetComponentsInChildren<RoomPositionMarker2D>(true).Single(marker => marker.Kind == "player_spawn");
                bool initialPlayerSpawned = Vector3.Distance(sessionPlayer.transform.position, initialSpawn.transform.position) < 0.001f &&
                    Vector3.Distance(sessionPlayerBody.position, initialSpawn.transform.position) < 0.001f &&
                    sessionPlayerBody.linearVelocity == Vector2.zero && Mathf.Abs(sessionPlayerBody.angularVelocity) < 0.001f;
                transitionChecksPassed &= initialPlayerSpawned;
                evidence.RecordAssertion("The persistent session player starts at the room player_spawn marker",
                    initialPlayerSpawned, initialSpawn.transform.position.ToString() + " with zero velocity",
                    sessionPlayer.transform.position.ToString() + "; velocity=" + sessionPlayerBody.linearVelocity +
                    "; body=" + sessionPlayerBody.position + "; angular=" + sessionPlayerBody.angularVelocity);

                int transitionsCompleted = 0;
                for (int targetIndex = 1; targetIndex < route.MandatoryNodeIds.Count; targetIndex++)
                {
                    RoomLifetimeScope oldScope = controller.ActiveScope;
                    GameObject oldRoot = controller.ActiveRoomRoot;
                    RoomInstanceId oldRoomId = controller.CurrentRoomId;
                    RoomActivity oldActivity = roomActivity[roomActivity.Count - 1];
                    int oldSignalCount = oldActivity.SignalCount;
                    int oldFrameCount = oldActivity.FrameCount;
                    string targetNodeId = route.MandatoryNodeIds[targetIndex];
                    sessionPlayerBody.linearVelocity = new Vector2(3.5f, -1.25f);
                    sessionPlayerBody.angularVelocity = 2.5f;
                    Vector3 sourceExitPosition = FindPort(oldRoot, "port.exit.east").transform.position;
                    evidence.RecordInput(targetIndex, "RoomExit", controller.CurrentNodeId + " -> " + targetNodeId);

                    bool transitioned = controller.TryTransition(oldRoomId, targetNodeId);
                    transitionsCompleted += transitioned ? 1 : 0;
                    bool immediateCleanup = oldScope.IsDisposed && oldScope.OwnedActorCount == 0 &&
                        oldScope.OwnedResourceCount == 0 && oldScope.ActiveCoroutineCount == 0 &&
                        signalBus.SubscriberCount == 1 && controller.ActiveRoomCount == 1 &&
                        controller.CurrentNodeId == targetNodeId && controller.CurrentRoomId != oldRoomId;
                    bool staleCallbackRejected = !controller.TryTransition(oldRoomId, route.MandatoryNodeIds[
                        Math.Min(targetIndex + 1, route.MandatoryNodeIds.Count - 1)]) &&
                        !controller.TryApplyToCurrentRoom(oldRoomId, _ => { });

                    signalBus.Raise();
                    bool onlyCurrentRoomReceivesEvents = oldActivity.SignalCount == oldSignalCount &&
                        roomActivity[roomActivity.Count - 1].SignalCount == 1;
                    bool actorDoesNotAccumulate = sessionPlayer != null && sessionPlayer.activeInHierarchy &&
                        roomActivity.Count(activity => activity.Actor != null && activity.Actor.activeInHierarchy) == 1;
                    RoomPortMarker2D targetEntry = FindPort(controller.ActiveRoomRoot, "port.entry.west");
                    bool portsAligned = targetEntry != null &&
                        Vector3.Distance(sourceExitPosition, targetEntry.transform.position) < 0.001f;
                    bool playerAtEntry = targetEntry != null && Vector3.Distance(sessionPlayer.transform.position,
                        targetEntry.transform.position + controller.ActiveRoomRoot.transform.right * 0.5f) < 0.001f &&
                        Vector3.Distance(sessionPlayerBody.position,
                            targetEntry.transform.position + controller.ActiveRoomRoot.transform.right * 0.5f) < 0.001f;
                    bool playerVelocityCleared = sessionPlayerBody.linearVelocity == Vector2.zero &&
                        Mathf.Abs(sessionPlayerBody.angularVelocity) < 0.001f;
                    transitionChecksPassed &= transitioned && immediateCleanup && staleCallbackRejected &&
                        onlyCurrentRoomReceivesEvents && actorDoesNotAccumulate && portsAligned && playerAtEntry && playerVelocityCleared;
                    evidence.RecordAssertion("Transition " + targetIndex + " retires the previous room and rejects its stale events",
                        transitioned && immediateCleanup && staleCallbackRejected && onlyCurrentRoomReceivesEvents &&
                        actorDoesNotAccumulate && portsAligned && playerAtEntry && playerVelocityCleared,
                        "old scope empty; one active room/actor/listener; stale room ID rejected; ports align; player enters target at rest",
                        "changed=" + transitioned + "; retired=" + oldScope.IsDisposed + "; " + DescribeActiveState(controller, signalBus) +
                        "; staleRejected=" + staleCallbackRejected + "; portsAligned=" + portsAligned +
                        "; playerAtEntry=" + playerAtEntry + "; playerStopped=" + playerVelocityCleared + "; oldFrames=" + oldFrameCount);

                    if (!transitioned) break;
                    yield return null;
                    bool oldHierarchyDestroyed = oldRoot == null && oldActivity.Actor == null &&
                        oldActivity.FrameCount == oldFrameCount;
                    transitionChecksPassed &= oldHierarchyDestroyed;
                    evidence.RecordAssertion("Retired room hierarchy and coroutine remain gone after a frame",
                        oldHierarchyDestroyed, "destroyed hierarchy and no coroutine ticks", "destroyed=" + (oldRoot == null) +
                        "; actorDestroyed=" + (oldActivity.Actor == null) + "; previousTicks=" + oldFrameCount +
                        "; currentTicks=" + oldActivity.FrameCount);
                }

                bool allMandatoryTransitionsPassed = transitionsCompleted == 20 &&
                    controller.CurrentNodeId == route.MandatoryNodeIds[20] && controller.CurrentNodeId == "final.boss";
                transitionChecksPassed &= allMandatoryTransitionsPassed;
                evidence.RecordAssertion("Twenty directed room transitions reach the final route node",
                    allMandatoryTransitionsPassed, "20 transitions ending at final.boss",
                    transitionsCompleted + " transitions ending at " + controller.CurrentNodeId);

                RoomInstanceId finalRoomId = controller.CurrentRoomId;
                controller.Dispose();
                bool finalRoomDisposed = controller.ActiveRoomCount == 0 && signalBus.SubscriberCount == 0 &&
                    !controller.IsCurrentRoom(finalRoomId) && sessionPlayer.activeInHierarchy;
                transitionChecksPassed &= finalRoomDisposed;
                evidence.RecordAssertion("Disposing the expedition removes its final room subscriptions and preserves its session actor",
                    finalRoomDisposed, "0 active rooms/listeners; session player remains active",
                    "rooms=" + controller.ActiveRoomCount + "; listeners=" + signalBus.SubscriberCount +
                    "; sessionPlayer=" + sessionPlayer.activeInHierarchy);

                GameObject restoredTransitionHost = new GameObject("D03RestoredTransitionHost");
                ExpeditionRoomTransitionController restoredController =
                    restoredTransitionHost.AddComponent<ExpeditionRoomTransitionController>();
                restoredController.Initialize(expeditionRunId, route, templateId =>
                    templatePrefabs.TryGetValue(templateId, out GameObject prefab) ? prefab : null,
                    sessionPlayer.transform, route.MandatoryNodeIds);
                bool restoredRoomKeepsIdentity = restoredController.CurrentNodeId == "final.boss" &&
                    restoredController.CurrentRoomId == finalRoomId;
                transitionChecksPassed &= restoredRoomKeepsIdentity;
                evidence.RecordAssertion("Restoring the same expedition at the same node reuses its room identity",
                    restoredRoomKeepsIdentity, "final.boss / " + finalRoomId,
                    restoredController.CurrentNodeId + " / " + restoredController.CurrentRoomId);
                restoredController.Dispose();
                UnityEngine.Object.Destroy(restoredTransitionHost);
                evidence.Complete();

                Assert.That(transitionsCompleted, Is.EqualTo(20));
                Assert.That(transitionChecksPassed, Is.True);
                UnityEngine.Object.Destroy(sessionPlayer);
                UnityEngine.Object.Destroy(transitionHost);
                foreach (GameObject prefab in templatePrefabs.Values) UnityEngine.Object.Destroy(prefab);
            }
        }

        [Test]
        public void D03MissingTargetPrefabLeavesTheCurrentRoomAndRouteCursorUntouched()
        {
            TextAsset catalogSource = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
            Assert.That(catalogSource, Is.Not.Null);
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(17320508u,
                ExpeditionRouteCatalogJsonLoader.Load(catalogSource.text));
            var firstRoomPrefab = new GameObject("TestRoomTemplate.First");
            AddRoomMarkers(firstRoomPrefab, 18);
            var host = new GameObject("D03FailedTransitionHost");
            ExpeditionRoomTransitionController controller = host.AddComponent<ExpeditionRoomTransitionController>();
            var sessionPlayer = new GameObject("D03FailedTransitionSessionPlayer");
            controller.Initialize(RunId.New(), route,
                templateId => templateId == route.Nodes[0].TemplateId ? firstRoomPrefab : null,
                sessionPlayer.transform);
            RoomInstanceId roomId = controller.CurrentRoomId;
            GameObject roomRoot = controller.ActiveRoomRoot;
            string initialNodeId = controller.CurrentNodeId;

            bool changed = controller.TryTransition(roomId, route.MandatoryNodeIds[1]);

            Assert.That(changed, Is.False);
            Assert.That(controller.ActiveRoomRoot, Is.SameAs(roomRoot));
            Assert.That(controller.CurrentNodeId, Is.EqualTo(initialNodeId));
            Assert.That(controller.CurrentRoomId, Is.EqualTo(roomId));
            Assert.That(controller.ActiveRoomCount, Is.EqualTo(1));
            Assert.That(controller.LastTransitionError, Does.Contain("No room prefab"));

            controller.Dispose();
            UnityEngine.Object.Destroy(sessionPlayer);
            UnityEngine.Object.Destroy(host);
            UnityEngine.Object.Destroy(firstRoomPrefab);
        }

        private static GameObject CreateRoomTemplate(string templateId, int width)
        {
            var prefab = new GameObject("TestRoomTemplate." + templateId.Replace('.', '_'));
            AddRoomMarkers(prefab, width);
            return prefab;
        }

        private static void AddRoomMarkers(GameObject prefab, int width)
        {
            var entry = new GameObject("EntryPort");
            entry.transform.SetParent(prefab.transform, false);
            entry.transform.localPosition = new Vector3(0f, 1f, 0f);
            entry.AddComponent<RoomPortMarker2D>().Configure("port.entry.west", "entry", "west", 0, 1, 3);

            var exit = new GameObject("ExitPort");
            exit.transform.SetParent(prefab.transform, false);
            exit.transform.localPosition = new Vector3(width, 1f, 0f);
            exit.AddComponent<RoomPortMarker2D>().Configure("port.exit.east", "exit", "east", width - 1, 1, 3);

            var playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.SetParent(prefab.transform, false);
            playerSpawn.transform.localPosition = new Vector3(1.5f, 1f, 0f);
            playerSpawn.AddComponent<RoomPositionMarker2D>().Configure("spawn.player", "player_spawn", 1, 1);
        }

        private static RoomPortMarker2D FindPort(GameObject roomRoot, string portId)
        {
            return roomRoot.GetComponentsInChildren<RoomPortMarker2D>(true).SingleOrDefault(marker => marker.PortId == portId);
        }

        private static IEnumerator CountFrames(RoomActivity activity)
        {
            while (true)
            {
                activity.FrameCount++;
                yield return null;
            }
        }

        private static string DescribeActiveState(ExpeditionRoomTransitionController controller, RoomSignalBus bus)
        {
            return "node=" + controller.CurrentNodeId + "; rooms=" + controller.ActiveRoomCount +
                "; actors=" + (controller.ActiveScope == null ? 0 : controller.ActiveScope.OwnedActorCount) +
                "; subscriptions=" + bus.SubscriberCount + "; coroutines=" +
                (controller.ActiveScope == null ? 0 : controller.ActiveScope.ActiveCoroutineCount);
        }

        private sealed class RoomActivity
        {
            public RoomActivity(RoomInstanceId roomInstanceId)
            {
                RoomInstanceId = roomInstanceId;
            }

            public RoomInstanceId RoomInstanceId { get; }
            public GameObject Actor { get; set; }
            public int SignalCount { get; set; }
            public int FrameCount { get; set; }
        }

        private sealed class RoomSignalBus
        {
            private readonly HashSet<Action> _listeners = new HashSet<Action>();

            public int SubscriberCount => _listeners.Count;

            public IDisposable Subscribe(Action listener)
            {
                if (listener == null) throw new ArgumentNullException(nameof(listener));
                if (!_listeners.Add(listener)) throw new InvalidOperationException("Listener is already registered.");
                return new Subscription(this, listener);
            }

            public void Raise()
            {
                foreach (Action listener in _listeners.ToArray()) listener();
            }

            private sealed class Subscription : IDisposable
            {
                private readonly RoomSignalBus _owner;
                private Action _listener;

                public Subscription(RoomSignalBus owner, Action listener)
                {
                    _owner = owner;
                    _listener = listener;
                }

                public void Dispose()
                {
                    if (_listener == null) return;
                    _owner._listeners.Remove(_listener);
                    _listener = null;
                }
            }
        }
    }
}
