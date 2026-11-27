#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Identifiers;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class F10V7C1PhysicalAdapterContractTests
    {
        private const string PhysicalDescriptorSha256 =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string BundleManifestSha256 =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        [Test]
        public void CompositeFingerprintIsBoundToExactDescriptorPayloadAndBundleManifest()
        {
            Assert.That(F10PhysicalDescriptorTableV1.TryComputeFingerprint("room.patio.c1", 1,
                PhysicalDescriptorSha256, BundleManifestSha256,
                out F10PhysicalDescriptorFingerprintResultV1 fingerprint), Is.True);
            Assert.That(EncounterCatalogFactoryV1.TryCreateDescriptor("room.patio.c1", 1,
                EncounterCatalogDescriptorKind.Room, fingerprint.Fingerprint,
                out EncounterCatalogDescriptor descriptor, out EncounterCatalogError factoryError),
                Is.True, factoryError.Message);

            Assert.That(F10V7C1RuntimePlanAdapter.TryValidateCompositeDescriptorFingerprint(
                descriptor, PhysicalDescriptorSha256, PhysicalDescriptorSha256,
                BundleManifestSha256, out string error), Is.True, error);
            Assert.That(F10V7C1RuntimePlanAdapter.TryValidateCompositeDescriptorFingerprint(
                descriptor, "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                PhysicalDescriptorSha256, BundleManifestSha256, out _), Is.False,
                "A per-reference digest that is not the exact physical-descriptor payload must fail closed.");
            Assert.That(F10V7C1RuntimePlanAdapter.TryValidateCompositeDescriptorFingerprint(
                descriptor, "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
                BundleManifestSha256, out _), Is.False,
                "Changing the descriptor payload must invalidate its composite fingerprint.");
            Assert.That(F10V7C1RuntimePlanAdapter.TryValidateCompositeDescriptorFingerprint(
                descriptor, PhysicalDescriptorSha256, PhysicalDescriptorSha256,
                "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", out _), Is.False,
                "Changing the bundle manifest must invalidate its composite fingerprint.");
        }

        [Test]
        public void SpawnAnchorUsesItsDirectJsonIdAndRejectsDuplicateOrOverriddenMarkers()
        {
            var roomObject = new GameObject("PhysicalAdapterTestRoom");
            try
            {
                RoomTemplatePrefab2D room = roomObject.AddComponent<RoomTemplatePrefab2D>();
                var markerDefinition = new RoomPositionMarkerData
                {
                    id = "spawn.slime.01",
                    kind = "enemy_spawn",
                    tileX = 12,
                    tileY = 1
                };
                var roomData = new RoomTemplateData
                {
                    schemaVersion = 1,
                    templateId = ApplicationBootstrapper.F10V7C1TemplateId,
                    tileSize = 1f,
                    markers = new[] { markerDefinition }
                };
                var markerObject = new GameObject("spawn.slime.01");
                markerObject.transform.SetParent(room.transform, false);
                markerObject.transform.localPosition = new Vector3(12.5f, 1f, 0f);
                RoomPositionMarker2D marker = markerObject.AddComponent<RoomPositionMarker2D>();
                marker.Configure(markerDefinition.id, markerDefinition.kind,
                    markerDefinition.tileX, markerDefinition.tileY);

                Assert.That(F10V7C1RuntimePlanAdapter.TryResolveC1SpawnAnchor(room,
                    roomData, "spawn.slime.01", out RoomPositionMarker2D resolved,
                    out string error), Is.True, error);
                Assert.That(resolved, Is.SameAs(marker));
                Assert.That(F10V7C1RuntimePlanAdapter.TryResolveC1SpawnAnchor(room,
                    roomData, "spawn.slime.02", out _, out _), Is.False,
                    "The adapter must use the selected spawn's SpawnAnchorId directly.");

                roomData.markers = new[] { markerDefinition, markerDefinition };
                Assert.That(F10V7C1RuntimePlanAdapter.TryResolveC1SpawnAnchor(room,
                    roomData, "spawn.slime.01", out _, out _), Is.False,
                    "Duplicate JSON marker identities must be rejected.");
                roomData.markers = new[] { markerDefinition };

                var duplicateObject = new GameObject("DuplicateMarker");
                duplicateObject.transform.SetParent(room.transform, false);
                duplicateObject.transform.localPosition = markerObject.transform.localPosition;
                RoomPositionMarker2D duplicate = duplicateObject.AddComponent<RoomPositionMarker2D>();
                duplicate.Configure(markerDefinition.id, markerDefinition.kind,
                    markerDefinition.tileX, markerDefinition.tileY);
                Assert.That(F10V7C1RuntimePlanAdapter.TryResolveC1SpawnAnchor(room,
                    roomData, "spawn.slime.01", out _, out _), Is.False,
                    "Duplicate active child components must be rejected.");

                Object.DestroyImmediate(duplicateObject);
                markerObject.transform.localPosition = new Vector3(13.5f, 1f, 0f);
                Assert.That(F10V7C1RuntimePlanAdapter.TryResolveC1SpawnAnchor(room,
                    roomData, "spawn.slime.01", out _, out _), Is.False,
                    "A marker moved from its JSON tile pose must be rejected.");
            }
            finally
            {
                Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void TransitionControllerRetainsInstantiatedPrefabProvenanceAcrossCommitAndCleanup()
        {
            ExpeditionRunConfiguration run = ExpeditionRunConfigurationFactory.CreateSample(
                BellatorSkillLoadout.ClassId, ExpeditionRunConfiguration.HumanRaceId,
                new[] { BellatorSkillLoadout.ChargeId, BellatorSkillLoadout.CircularCutId });
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string templateId in run.Route.Nodes.Select(node => node.TemplateId).Distinct())
                prefabs.Add(templateId, CreateTransitionRoomPrefab(templateId));

            var host = new GameObject("PhysicalAdapterProvenanceHost");
            var player = new GameObject("PhysicalAdapterProvenancePlayer");
            ExpeditionRoomTransitionController controller =
                host.AddComponent<ExpeditionRoomTransitionController>();
            try
            {
                controller.Initialize(run.RunId, run.Route,
                    templateId => prefabs.TryGetValue(templateId, out GameObject prefab) ? prefab : null,
                    player.transform);

                RoomLifetimeScope initialScope = controller.ActiveScope;
                RoomTemplatePrefab2D initialRoom = initialScope.GetComponentInChildren<RoomTemplatePrefab2D>(true);
                Assert.That(controller.TryGetActiveRoomPrefabSource(initialScope, initialRoom,
                    out GameObject initialSource), Is.True);
                Assert.That(ReferenceEquals(initialSource,
                    prefabs[run.Route.Nodes.Single(node =>
                        node.NodeId == run.Route.MandatoryNodeIds[0]).TemplateId]), Is.True);

                string targetNodeId = run.Route.MandatoryNodeIds[1];
                Assert.That(controller.TryPrepareTransition(controller.CurrentRoomId, targetNodeId,
                    out ExpeditionRoomTransitionController.PreparedRoomTransition prepared), Is.True,
                    controller.LastTransitionError);
                RoomLifetimeScope preparedScope = prepared.StagedScope;
                RoomTemplatePrefab2D preparedRoom = preparedScope.GetComponentInChildren<RoomTemplatePrefab2D>(true);
                Assert.That(controller.TryGetActiveRoomPrefabSource(preparedScope, preparedRoom,
                    out _), Is.False, "A prepared room is not yet the controller's active source.");
                Assert.That(controller.TryCommitPreparedTransition(prepared), Is.True,
                    controller.LastTransitionError);

                RoomLifetimeScope committedScope = controller.ActiveScope;
                RoomTemplatePrefab2D committedRoom = committedScope.GetComponentInChildren<RoomTemplatePrefab2D>(true);
                Assert.That(controller.TryGetActiveRoomPrefabSource(committedScope, committedRoom,
                    out GameObject committedSource), Is.True);
                Assert.That(ReferenceEquals(committedSource,
                    prefabs[run.Route.Nodes.Single(node => node.NodeId == targetNodeId).TemplateId]), Is.True);

                string cancelledTargetNodeId = run.Route.MandatoryNodeIds[2];
                Assert.That(controller.TryPrepareTransition(controller.CurrentRoomId, cancelledTargetNodeId,
                    out ExpeditionRoomTransitionController.PreparedRoomTransition cancelled), Is.True,
                    controller.LastTransitionError);
                RoomLifetimeScope cancelledScope = cancelled.StagedScope;
                RoomTemplatePrefab2D cancelledRoom = cancelledScope.GetComponentInChildren<RoomTemplatePrefab2D>(true);
                cancelled.Dispose();
                Assert.That(controller.TryGetActiveRoomPrefabSource(cancelledScope, cancelledRoom,
                    out _), Is.False, "A cancelled staged room must not retain active provenance.");
                Assert.That(controller.TryGetActiveRoomPrefabSource(committedScope, committedRoom,
                    out committedSource), Is.True);
                Assert.That(ReferenceEquals(committedSource,
                    prefabs[run.Route.Nodes.Single(node => node.NodeId == targetNodeId).TemplateId]), Is.True);

                controller.Dispose();
                Assert.That(controller.TryGetActiveRoomPrefabSource(committedScope, committedRoom,
                    out _), Is.False, "Retiring the controller clears active prefab provenance.");
            }
            finally
            {
                controller.Dispose();
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(host);
                foreach (GameObject prefab in prefabs.Values) Object.DestroyImmediate(prefab);
            }
        }

        private static GameObject CreateTransitionRoomPrefab(string templateId)
        {
            var prefab = new GameObject("ProvenanceRoom." + templateId.Replace('.', '_'));
            prefab.AddComponent<RoomTemplatePrefab2D>();

            var entry = new GameObject("EntryPort");
            entry.transform.SetParent(prefab.transform, false);
            entry.transform.localPosition = new Vector3(0f, 1f, 0f);
            entry.AddComponent<RoomPortMarker2D>().Configure(
                "port.entry.west", "entry", "west", 0, 1, 3);

            var exit = new GameObject("ExitPort");
            exit.transform.SetParent(prefab.transform, false);
            exit.transform.localPosition = new Vector3(18f, 1f, 0f);
            exit.AddComponent<RoomPortMarker2D>().Configure(
                "port.exit.east", "exit", "east", 17, 1, 3);

            var spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(prefab.transform, false);
            spawn.transform.localPosition = new Vector3(1.5f, 1f, 0f);
            spawn.AddComponent<RoomPositionMarker2D>().Configure("spawn.player", "player_spawn", 1, 1);
            return prefab;
        }
    }
}
#endif
