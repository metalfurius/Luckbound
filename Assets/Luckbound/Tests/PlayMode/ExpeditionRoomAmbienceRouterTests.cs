using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.PlayMode
{
    public sealed class ExpeditionRoomAmbienceRouterTests
    {
        private readonly List<GameObject> _ownedObjects = new List<GameObject>();
        private readonly List<ExpeditionRoomTransitionController> _additionalTransitions =
            new List<ExpeditionRoomTransitionController>();
        private ExpeditionRoomTransitionController _roomTransitions;
        private ExpeditionRoomAmbienceRouter _router;

        [Test]
        public void StableRoomTemplatesResolveToTheirAmbienceAndUnknownTemplatesRemainUnmapped()
        {
            Assert.That(ExpeditionRoomAmbienceRouter.ResolveAmbienceId("room.patio.c1"),
                Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            Assert.That(ExpeditionRoomAmbienceRouter.ResolveAmbienceId("room.archive.guardian"),
                Is.EqualTo(RuntimeAmbienceDirector.ArchiveId));
            Assert.That(ExpeditionRoomAmbienceRouter.ResolveAmbienceId("room.archivo.guardian"),
                Is.EqualTo(RuntimeAmbienceDirector.ArchiveId));
            Assert.That(ExpeditionRoomAmbienceRouter.ResolveAmbienceId("room.forge.c1"),
                Is.EqualTo(RuntimeAmbienceDirector.ForgeId));
            Assert.That(ExpeditionRoomAmbienceRouter.ResolveAmbienceId("room.forjas.c1"),
                Is.EqualTo(RuntimeAmbienceDirector.ForgeId));
            Assert.That(ExpeditionRoomAmbienceRouter.ResolveAmbienceId("room.unknown.mystery"), Is.Null);
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator RoomTransitionsWaitForGestureAndRouteTheActiveRoomThroughTheApplicationSeam()
        {
            ExpeditionRoutePlan route = CreateRoute(out Dictionary<string, GameObject> templatePrefabs);

            var audioHost = CreateOwnedObject("RoomAmbienceApplicationAudio");
            audioHost.AddComponent<RuntimeAudioDirector>();
            RuntimeAmbienceDirector ambienceDirector = audioHost.AddComponent<RuntimeAmbienceDirector>();
            ambienceDirector.CrossfadeSeconds = 0f;
            ApplicationAudioRouter audioRouter = audioHost.AddComponent<ApplicationAudioRouter>();
            audioRouter.SetSceneContext("Expedition");

            var routerHost = CreateOwnedObject("ExpeditionRoomAmbienceRouter");
            _router = routerHost.AddComponent<ExpeditionRoomAmbienceRouter>();

            _roomTransitions = CreateTransitionController("ExpeditionRoomTransitionController", route,
                templatePrefabs, "ExpeditionSessionPlayer");

            _router.Bind(_roomTransitions, audioRouter);
            _router.Bind(_roomTransitions, audioRouter);

            Assert.That(_roomTransitions.ActiveScope.TemplateId, Does.StartWith("room.patio."));
            Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(_roomTransitions.ActiveScope.TemplateId));
            Assert.That(_router.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            Assert.That(audioRouter.UserGestureObserved, Is.False);
            Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.Null,
                "A room request must not start audio before the first user gesture.");
            Assert.That(ambienceDirector.ActiveSourceCount, Is.Zero);

            bool sawArchive = false;
            bool sawForjas = false;
            bool sawUnmappedRoom = false;
            bool unlockedAtArchive = false;
            bool disposedAtForjas = false;
            bool detachedFromNextRoomEvent = false;

            for (int nodeIndex = 1; nodeIndex < route.MandatoryNodeIds.Count; nodeIndex++)
            {
                string targetNodeId = route.MandatoryNodeIds[nodeIndex];
                ExpeditionRouteNode targetNode = route.Nodes.Single(node => node.NodeId == targetNodeId);
                Assert.That(_roomTransitions.TryTransition(_roomTransitions.CurrentRoomId, targetNodeId), Is.True,
                    "The real route transition should activate " + targetNodeId);

                RoomLifetimeScope activeScope = _roomTransitions.ActiveScope;
                if (disposedAtForjas)
                {
                    Assert.That(activeScope.TemplateId, Does.StartWith("room.forjas."));
                    Assert.That(audioRouter.HasRoomAmbienceOverride, Is.False);
                    Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
                    Assert.That(ambienceDirector.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId),
                        "A room activation after disposal must leave the scene fallback intact.");
                    detachedFromNextRoomEvent = true;
                    break;
                }

                string expectedAmbienceId = ExpeditionRoomAmbienceRouter.ResolveAmbienceId(targetNode.TemplateId);
                if (expectedAmbienceId == null)
                {
                    Assert.That(audioRouter.HasRoomAmbienceOverride, Is.True,
                        "An unmapped room must explicitly suppress the scene fallback.");
                    Assert.That(audioRouter.RequestedAmbienceId, Is.Null);
                    Assert.That(_router.CurrentAmbienceId, Is.Null);
                    Assert.That(ambienceDirector.CurrentAmbienceId, Is.Null,
                        "Unmapped room " + targetNode.TemplateId + " must not retain the previous ambience.");
                    Assert.That(ambienceDirector.ActiveSourceCount, Is.Zero);
                    sawUnmappedRoom |= targetNode.TemplateId == "room.hub.rest";
                    continue;
                }

                Assert.That(_router.CurrentAmbienceId, Is.EqualTo(expectedAmbienceId));
                Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(targetNode.TemplateId));

                if (!audioRouter.UserGestureObserved)
                {
                    Assert.That(ambienceDirector.CurrentAmbienceId, Is.Null,
                        "Mapped rooms before the first gesture must remain silent.");
                    Assert.That(ambienceDirector.ActiveSourceCount, Is.Zero);
                    if (expectedAmbienceId == RuntimeAmbienceDirector.ArchiveId)
                    {
                        audioRouter.NotifyUserGesture();
                        yield return new WaitUntil(() => ambienceDirector.CurrentAmbienceId == expectedAmbienceId);
                        unlockedAtArchive = true;

                        // Scene routing may be refreshed on the same input event. The room override wins.
                        audioRouter.SetSceneContext("Expedition");
                        yield return null;
                        Assert.That(ambienceDirector.CurrentAmbienceId, Is.EqualTo(expectedAmbienceId));
                        Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(expectedAmbienceId));
                    }
                }
                else
                {
                    yield return new WaitUntil(() => ambienceDirector.CurrentAmbienceId == expectedAmbienceId);
                }

                sawArchive |= expectedAmbienceId == RuntimeAmbienceDirector.ArchiveId;
                sawForjas |= expectedAmbienceId == RuntimeAmbienceDirector.ForgeId &&
                    targetNode.TemplateId.StartsWith("room.forjas.", System.StringComparison.Ordinal);

                if (sawForjas)
                {
                    _router.Dispose();
                    _router.Dispose();
                    Assert.That(audioRouter.HasRoomAmbienceOverride, Is.False,
                        "Disposing the router must release its room override.");
                    Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
                    yield return new WaitUntil(() => ambienceDirector.CurrentAmbienceId ==
                        RuntimeAmbienceDirector.ForestCourtyardId);
                    disposedAtForjas = true;
                }
            }

            Assert.That(sawArchive, Is.True, "The real route should activate an Archive room.");
            Assert.That(sawForjas, Is.True, "The real route should activate a Forjas room.");
            Assert.That(sawUnmappedRoom, Is.True, "The route rest room should stop Patio ambience.");
            Assert.That(unlockedAtArchive, Is.True,
                "The active non-Patio room should be selected when the first gesture unlocks playback.");
            Assert.That(detachedFromNextRoomEvent, Is.True, "The router must unsubscribe before the next room event.");
        }

        [UnityTest, Timeout(10000)]
        public IEnumerator RoomlessOverrideStopsPlaybackAndClearingItRestoresSceneFallback()
        {
            var audioHost = CreateOwnedObject("RoomlessOverrideApplicationAudio");
            audioHost.AddComponent<RuntimeAudioDirector>();
            RuntimeAmbienceDirector ambienceDirector = audioHost.AddComponent<RuntimeAmbienceDirector>();
            ambienceDirector.CrossfadeSeconds = 0f;
            ApplicationAudioRouter audioRouter = audioHost.AddComponent<ApplicationAudioRouter>();
            audioRouter.SetSceneContext("Expedition");
            audioRouter.NotifyUserGesture();

            yield return new WaitUntil(() => ambienceDirector.ActiveSourceCount == 1);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));

            audioRouter.SetRoomAmbienceOverride(null);
            Assert.That(audioRouter.HasRoomAmbienceOverride, Is.True);
            Assert.That(audioRouter.RequestedAmbienceId, Is.Null);
            yield return new WaitUntil(() => ambienceDirector.ActiveSourceCount == 0);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.Null,
                "A room without mapped ambience must stop the active scene fallback.");

            audioRouter.ClearRoomAmbienceOverride();
            Assert.That(audioRouter.HasRoomAmbienceOverride, Is.False);
            Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            yield return new WaitUntil(() => ambienceDirector.ActiveSourceCount == 1);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId),
                "Clearing the room override restores the current scene fallback.");
        }

        [UnityTest, Timeout(15000)]
        public IEnumerator RebindingDetachesThePreviousRoomEventsAndDisposeKeepsOneFallbackVoice()
        {
            ExpeditionRoutePlan route = CreateRoute(out Dictionary<string, GameObject> templatePrefabs);

            var audioHost = CreateOwnedObject("RoomAmbienceRebindApplicationAudio");
            audioHost.AddComponent<RuntimeAudioDirector>();
            RuntimeAmbienceDirector ambienceDirector = audioHost.AddComponent<RuntimeAmbienceDirector>();
            ambienceDirector.CrossfadeSeconds = 0f;
            ApplicationAudioRouter audioRouter = audioHost.AddComponent<ApplicationAudioRouter>();
            audioRouter.SetSceneContext("Expedition");
            audioRouter.NotifyUserGesture();

            _router = CreateOwnedObject("RoomAmbienceRebindRouter").AddComponent<ExpeditionRoomAmbienceRouter>();
            _roomTransitions = CreateTransitionController("RoomAmbienceFirstTransitions", route,
                templatePrefabs, "RoomAmbienceFirstPlayer");
            ExpeditionRoomTransitionController replacementTransitions = CreateTransitionController(
                "RoomAmbienceReplacementTransitions", route, templatePrefabs, "RoomAmbienceReplacementPlayer");
            _additionalTransitions.Add(replacementTransitions);

            _router.Bind(_roomTransitions, audioRouter);
            _router.Bind(_roomTransitions, audioRouter);
            yield return new WaitUntil(() => ambienceDirector.ActiveSourceCount == 1);

            _router.Bind(replacementTransitions, audioRouter);
            Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(replacementTransitions.ActiveScope.TemplateId));
            Assert.That(_router.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));

            yield return TransitionToAmbience(_roomTransitions, route, RuntimeAmbienceDirector.ArchiveId);
            Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(replacementTransitions.ActiveScope.TemplateId),
                "Transitions from the previously bound session must no longer update the room override.");
            Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            Assert.That(ambienceDirector.ActiveSourceCount, Is.EqualTo(1),
                "Repeated binds and stale room events must not create duplicate ambience voices.");

            yield return TransitionToAmbience(replacementTransitions, route, RuntimeAmbienceDirector.ArchiveId);
            Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(replacementTransitions.ActiveScope.TemplateId));
            Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ArchiveId));
            yield return new WaitUntil(() => ambienceDirector.CurrentAmbienceId == RuntimeAmbienceDirector.ArchiveId);

            _router.Dispose();
            Assert.That(audioRouter.HasRoomAmbienceOverride, Is.False);
            Assert.That(audioRouter.RequestedAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));
            yield return new WaitUntil(() => ambienceDirector.CurrentAmbienceId ==
                RuntimeAmbienceDirector.ForestCourtyardId);

            yield return TransitionToAmbience(replacementTransitions, route, RuntimeAmbienceDirector.ForgeId);
            Assert.That(ambienceDirector.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId),
                "Events after disposal must not replace the restored scene fallback.");
            Assert.That(ambienceDirector.ActiveSourceCount, Is.EqualTo(1));
        }

        [UnityTearDown]
        public IEnumerator CleanupOwnedObjects()
        {
            if (_router != null) _router.Dispose();
            if (_roomTransitions != null) _roomTransitions.Dispose();
            for (int index = 0; index < _additionalTransitions.Count; index++)
            {
                if (_additionalTransitions[index] != null) _additionalTransitions[index].Dispose();
            }
            _additionalTransitions.Clear();

            for (int index = _ownedObjects.Count - 1; index >= 0; index--)
            {
                if (_ownedObjects[index] != null) Object.Destroy(_ownedObjects[index]);
            }
            _ownedObjects.Clear();
            _router = null;
            _roomTransitions = null;
            yield return null;
        }

        private GameObject CreateRoomTemplate(string templateId)
        {
            GameObject prefab = CreateOwnedObject("RoomTemplate." + templateId.Replace('.', '_'));

            var entry = new GameObject("EntryPort");
            entry.transform.SetParent(prefab.transform, false);
            entry.transform.localPosition = new Vector3(0f, 1f, 0f);
            entry.AddComponent<RoomPortMarker2D>().Configure("port.entry.west", "entry", "west", 0, 1, 3);

            var exit = new GameObject("ExitPort");
            exit.transform.SetParent(prefab.transform, false);
            exit.transform.localPosition = new Vector3(18f, 1f, 0f);
            exit.AddComponent<RoomPortMarker2D>().Configure("port.exit.east", "exit", "east", 17, 1, 3);

            var playerSpawn = new GameObject("PlayerSpawn");
            playerSpawn.transform.SetParent(prefab.transform, false);
            playerSpawn.transform.localPosition = new Vector3(1.5f, 1f, 0f);
            playerSpawn.AddComponent<RoomPositionMarker2D>().Configure("spawn.player", "player_spawn", 1, 1);
            return prefab;
        }

        private ExpeditionRoutePlan CreateRoute(out Dictionary<string, GameObject> templatePrefabs)
        {
            TextAsset catalogSource = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
            Assert.That(catalogSource, Is.Not.Null);
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(17320508u,
                ExpeditionRouteCatalogJsonLoader.Load(catalogSource.text));

            templatePrefabs = new Dictionary<string, GameObject>();
            foreach (string templateId in route.Nodes.Select(node => node.TemplateId).Distinct())
            {
                templatePrefabs.Add(templateId, CreateRoomTemplate(templateId));
            }
            return route;
        }

        private ExpeditionRoomTransitionController CreateTransitionController(string objectName,
            ExpeditionRoutePlan route, Dictionary<string, GameObject> templatePrefabs, string playerName)
        {
            ExpeditionRoomTransitionController transitions = CreateOwnedObject(objectName)
                .AddComponent<ExpeditionRoomTransitionController>();
            GameObject sessionPlayer = CreateOwnedObject(playerName);
            transitions.Initialize(RunId.New(), route,
                templateId => templatePrefabs.TryGetValue(templateId, out GameObject prefab) ? prefab : null,
                sessionPlayer.transform);
            return transitions;
        }

        private IEnumerator TransitionToAmbience(ExpeditionRoomTransitionController transitions,
            ExpeditionRoutePlan route, string ambienceId)
        {
            int currentNodeIndex = -1;
            for (int nodeIndex = 0; nodeIndex < route.MandatoryNodeIds.Count; nodeIndex++)
            {
                if (route.MandatoryNodeIds[nodeIndex] == transitions.CurrentNodeId)
                {
                    currentNodeIndex = nodeIndex;
                    break;
                }
            }
            Assert.That(currentNodeIndex, Is.GreaterThanOrEqualTo(0),
                "The transition controller must be on its generated mandatory route.");
            for (int nodeIndex = currentNodeIndex + 1; nodeIndex < route.MandatoryNodeIds.Count; nodeIndex++)
            {
                string targetNodeId = route.MandatoryNodeIds[nodeIndex];
                ExpeditionRouteNode targetNode = route.Nodes.Single(node => node.NodeId == targetNodeId);
                if (ExpeditionRoomAmbienceRouter.ResolveAmbienceId(targetNode.TemplateId) == ambienceId)
                {
                    Assert.That(transitions.TryTransition(transitions.CurrentRoomId, targetNodeId), Is.True,
                        "The real route transition should activate " + targetNodeId);
                    yield break;
                }

                Assert.That(transitions.TryTransition(transitions.CurrentRoomId, targetNodeId), Is.True,
                    "The real route transition should activate " + targetNodeId);
            }

            Assert.Fail("The generated route does not include ambience " + ambienceId + ".");
        }

        private GameObject CreateOwnedObject(string objectName)
        {
            var gameObject = new GameObject(objectName);
            _ownedObjects.Add(gameObject);
            return gameObject;
        }
    }
}
