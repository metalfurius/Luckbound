using System;
using Luckbound.Content;
using Luckbound.Gameplay.Rooms;
using NUnit.Framework;
using UnityEngine;

namespace Luckbound.Tests.PlayMode
{
    public sealed class RoomTemplateTests
    {
        private const string DefinitionResourcePath = "Luckbound/Data/Rooms/D01PrototypeRoom";
        private const string PrefabResourcePath = "Luckbound/Rooms/PrototypeRoom";

        [Test]
        public void D01JsonDefinesValidGridPortsFloorAndPlayerAndEnemyPositions()
        {
            TextAsset source = Resources.Load<TextAsset>(DefinitionResourcePath);
            Assert.That(source, Is.Not.Null);

            RoomTemplateData room = RoomTemplateJsonLoader.Load(source.text);
            Assert.That(room.templateId, Is.EqualTo("room.patio.prototype"));
            Assert.That(room.widthTiles, Is.EqualTo(24));
            Assert.That(room.heightTiles, Is.EqualTo(14));
            Assert.That(room.ports, Has.Length.EqualTo(2));
            Assert.That(room.markers, Has.Length.EqualTo(3));
            Assert.That(RoomTemplateJsonLoader.GetCell(room, 0, 0), Is.EqualTo('#'));
            Assert.That(RoomTemplateJsonLoader.GetCell(room, 23, 0), Is.EqualTo('#'));
            Assert.That(RoomTemplateJsonLoader.GetCell(room, 0, 1), Is.EqualTo('.'));
            Assert.That(room.markers[0].tileY, Is.EqualTo(1), "Player marker uses the safe floor top as its vertical origin.");
        }

        [Test]
        public void D01LoaderRejectsBlockedPortsMissingLandingFloorAndInvalidMarkerPositions()
        {
            string source = Resources.Load<TextAsset>(DefinitionResourcePath).text;

            FormatException blockedPort = Assert.Throws<FormatException>(() =>
                RoomTemplateJsonLoader.Load(ReplaceFirst(source, "\"........................\",", "\"#.......................\",")));
            Assert.That(blockedPort.Message, Does.Contain("opening clear"));

            FormatException missingLandingFloor = Assert.Throws<FormatException>(() =>
                RoomTemplateJsonLoader.Load(ReplaceFirst(source, "\"########################\",", "\".#######################\",")));
            Assert.That(missingLandingFloor.Message, Does.Contain("solid floor"));

            FormatException blockedSpawn = Assert.Throws<FormatException>(() =>
                RoomTemplateJsonLoader.Load(ReplaceFirst(source, "\"........................\",", "\"..#.....................\",")));
            Assert.That(blockedSpawn.Message, Does.Contain("open space directly above solid floor"));

            FormatException closeEnemy = Assert.Throws<FormatException>(() =>
                RoomTemplateJsonLoader.Load(ReplaceFirst(source, "\"tileX\": 12", "\"tileX\": 9")));
            Assert.That(closeEnemy.Message, Does.Contain("closer than minimumEnemyDistanceTiles"));
        }

        [Test]
        public void D01GeneratedPrefabMatchesJsonAndHasPhysicalSafeFloorAtBothPorts()
        {
            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            Assert.That(prefab, Is.Not.Null, "The Editor generator must produce the prefab under Resources for validation.");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                RoomTemplatePrefab2D template = instance.GetComponent<RoomTemplatePrefab2D>();
                Assert.That(template, Is.Not.Null);
                RoomTemplateData definition = template.ValidateGeneratedPrefab();
                Assert.That(template.GroundCollider, Is.Not.Null);
                Assert.That(template.GroundTilemap.gameObject.layer, Is.EqualTo(LayerMask.NameToLayer("Ground")));

                Physics2D.SyncTransforms();
                int groundMask = LayerMask.GetMask("Ground");
                Assert.That(groundMask, Is.Not.EqualTo(0));
                AssertGroundBelowPort(template, 0.5f, groundMask);
                AssertGroundBelowPort(template, definition.widthTiles - 0.5f, groundMask);
                Assert.That(Physics2D.OverlapBox(new Vector2(0.5f, 2.5f), new Vector2(0.8f, 0.8f), 0f, groundMask), Is.Null,
                    "The entry aperture must keep three tiles of headroom.");
                Assert.That(Physics2D.OverlapBox(new Vector2(definition.widthTiles - 0.5f, 2.5f), new Vector2(0.8f, 0.8f), 0f, groundMask), Is.Null,
                    "The exit aperture must keep three tiles of headroom.");

                RoomPositionMarker2D[] markers = instance.GetComponentsInChildren<RoomPositionMarker2D>();
                Assert.That(markers, Has.Length.EqualTo(definition.markers.Length));
                foreach (RoomPositionMarker2D marker in markers)
                {
                    AssertGroundBelowMarker(template, marker, groundMask);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AssertGroundBelowPort(RoomTemplatePrefab2D template, float worldX, int groundMask)
        {
            RaycastHit2D hit = Physics2D.Raycast(new Vector2(worldX, 2.5f), Vector2.down, 2f, groundMask);
            Assert.That(hit.collider, Is.Not.Null, "Each port must have solid floor beneath its safe landing position.");
            Assert.That(hit.collider, Is.EqualTo(template.GroundCollider));
        }

        private static void AssertGroundBelowMarker(RoomTemplatePrefab2D template, RoomPositionMarker2D marker, int groundMask)
        {
            Vector2 origin = marker.transform.position + Vector3.up * 1.5f;
            RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, 2f, groundMask);
            Assert.That(hit.collider, Is.Not.Null, marker.MarkerId + " must resolve to a physical floor position.");
            Assert.That(hit.collider, Is.EqualTo(template.GroundCollider));
        }

        private static string ReplaceFirst(string source, string search, string replacement)
        {
            int index = source.IndexOf(search, StringComparison.Ordinal);
            Assert.That(index, Is.GreaterThanOrEqualTo(0), "Test fixture text was not found: " + search);
            return source.Substring(0, index) + replacement + source.Substring(index + search.Length);
        }
    }
}
