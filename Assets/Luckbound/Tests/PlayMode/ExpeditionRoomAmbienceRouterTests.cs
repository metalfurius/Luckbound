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
        public IEnumerator RoomTransitionsRouteAllSectorsStopForUnmappedRoomsAndUnsubscribeOnDispose()
        {
            TextAsset catalogSource = Resources.Load<TextAsset>("Luckbound/Data/Rooms/RouteTemplates");
            Assert.That(catalogSource, Is.Not.Null);
            ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(17320508u,
                ExpeditionRouteCatalogJsonLoader.Load(catalogSource.text));

            var templatePrefabs = new Dictionary<string, GameObject>();
            foreach (string templateId in route.Nodes.Select(node => node.TemplateId).Distinct())
            {
                templatePrefabs.Add(templateId, CreateRoomTemplate(templateId));
            }

            var directorHost = CreateOwnedObject("RoomAmbienceDirector");
            RuntimeAmbienceDirector director = directorHost.AddComponent<RuntimeAmbienceDirector>();
            director.CrossfadeSeconds = 0f;

            var routerHost = CreateOwnedObject("ExpeditionRoomAmbienceRouter");
            _router = routerHost.AddComponent<ExpeditionRoomAmbienceRouter>();

            var transitionHost = CreateOwnedObject("ExpeditionRoomTransitionController");
            _roomTransitions = transitionHost.AddComponent<ExpeditionRoomTransitionController>();
            GameObject sessionPlayer = CreateOwnedObject("ExpeditionSessionPlayer");

            _router.Bind(_roomTransitions, director);
            _router.Bind(_roomTransitions, director);
            _roomTransitions.Initialize(RunId.New(), route,
                templateId => templatePrefabs.TryGetValue(templateId, out GameObject prefab) ? prefab : null,
                sessionPlayer.transform);

            yield return new WaitUntil(() => director.CurrentAmbienceId == RuntimeAmbienceDirector.ForestCourtyardId);
            Assert.That(_roomTransitions.ActiveScope.TemplateId, Does.StartWith("room.patio."));
            Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(_roomTransitions.ActiveScope.TemplateId));
            Assert.That(_router.CurrentAmbienceId, Is.EqualTo(RuntimeAmbienceDirector.ForestCourtyardId));

            bool sawArchive = false;
            bool sawForjas = false;
            bool sawUnmappedRoom = false;
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
                    Assert.That(director.CurrentAmbienceId, Is.Null,
                        "A room activation after disposal must not reach the ambience director.");
                    detachedFromNextRoomEvent = true;
                    break;
                }

                string expectedAmbienceId = ExpeditionRoomAmbienceRouter.ResolveAmbienceId(targetNode.TemplateId);
                if (expectedAmbienceId == null)
                {
                    Assert.That(director.CurrentAmbienceId, Is.Null,
                        "Unmapped room " + targetNode.TemplateId + " must stop the previous ambience.");
                    Assert.That(_router.CurrentAmbienceId, Is.Null);
                    sawUnmappedRoom |= targetNode.TemplateId == "room.hub.rest";
                    continue;
                }

                yield return new WaitUntil(() => director.CurrentAmbienceId == expectedAmbienceId);
                Assert.That(_router.CurrentAmbienceId, Is.EqualTo(expectedAmbienceId));
                Assert.That(_router.CurrentRoomTemplateId, Is.EqualTo(targetNode.TemplateId));
                sawArchive |= expectedAmbienceId == RuntimeAmbienceDirector.ArchiveId;
                sawForjas |= expectedAmbienceId == RuntimeAmbienceDirector.ForgeId &&
                    targetNode.TemplateId.StartsWith("room.forjas.", System.StringComparison.Ordinal);

                if (sawForjas)
                {
                    _router.Dispose();
                    _router.Dispose();
                    Assert.That(director.CurrentAmbienceId, Is.Null,
                        "Disposing the router must stop its current ambience immediately.");
                    disposedAtForjas = true;
                }
            }

            Assert.That(sawArchive, Is.True, "The real route should activate an Archive room.");
            Assert.That(sawForjas, Is.True, "The real route should activate a Forjas room.");
            Assert.That(sawUnmappedRoom, Is.True, "The route rest room should stop Patio ambience.");
            Assert.That(detachedFromNextRoomEvent, Is.True, "The router must unsubscribe before the next room event.");
        }

        [UnityTearDown]
        public IEnumerator CleanupOwnedObjects()
        {
            if (_router != null) _router.Dispose();
            if (_roomTransitions != null) _roomTransitions.Dispose();

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

        private GameObject CreateOwnedObject(string objectName)
        {
            var gameObject = new GameObject(objectName);
            _ownedObjects.Add(gameObject);
            return gameObject;
        }
    }
}
