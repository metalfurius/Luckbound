using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using Luckbound.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Luckbound.Tests.Editor
{
    public sealed class ProjectScaffoldGeneratorTests
    {
        private const float ImportedSpriteTolerance = 0.001f;
        private const string AvatarSpriteGuid = "ef0c7c5cb695aa84cad8cef6f7fd1017";
        private const string StoneSpriteGuid = "ece714fd4199c9f4481389b1367d40c9";

        private static readonly string[] ManagedFiles =
        {
            "Assets/Luckbound/Scenes/Bootstrap.unity",
            "Assets/Luckbound/Scenes/Bootstrap.unity.meta",
            "Assets/Luckbound/Scenes/Menu.unity",
            "Assets/Luckbound/Scenes/Menu.unity.meta",
            "Assets/Luckbound/Scenes/Hub.unity",
            "Assets/Luckbound/Scenes/Hub.unity.meta",
            "Assets/Luckbound/Scenes/Expedition.unity",
            "Assets/Luckbound/Scenes/Expedition.unity.meta",
            "Assets/Luckbound/Scenes/CombatArena.unity",
            "Assets/Luckbound/Scenes/CombatArena.unity.meta",
            "Assets/Luckbound/Scenes/CustodianArena.unity",
            "Assets/Luckbound/Scenes/CustodianArena.unity.meta",
            "Assets/Resources/Luckbound/PlayerVisualStates.controller",
            "Assets/Resources/Luckbound/PlayerVisualStates.controller.meta",
            "Assets/Luckbound/Art.meta",
            "Assets/Luckbound/Art/Prototypes.meta",
            "Assets/Luckbound/Art/Prototypes/MovementDemo.meta",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png.meta",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png",
            "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png.meta",
            "Assets/Luckbound/Prefabs.meta",
            "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab",
            "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab.meta",
            "Assets/Luckbound/Prefabs/CombatArena.meta",
            "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab",
            "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab.meta",
            "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab",
            "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab.meta",
            "Assets/Luckbound/Prefabs/CustodianArena.meta",
            "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab",
            "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab.meta",
            "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab",
            "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab.meta",
            "Assets/Resources/Luckbound/Data/Encounters.meta",
            "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json",
            "Assets/Resources/Luckbound/Data/Encounters/C08Arena.json.meta",
            "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json",
            "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json.meta",
            "Assets/Resources/Luckbound/Data/Rooms.meta",
            "Assets/Resources/Luckbound/Data/Rooms/D01PrototypeRoom.json",
            "Assets/Resources/Luckbound/Data/Rooms/D01PrototypeRoom.json.meta",
            "Assets/Resources/Luckbound/Data/Rooms/RouteTemplates.json",
            "Assets/Resources/Luckbound/Data/Rooms/RouteTemplates.json.meta",
            "Assets/Resources/Luckbound/Data/Items.meta",
            "Assets/Resources/Luckbound/Data/Items/Items.json",
            "Assets/Resources/Luckbound/Data/Items/Items.json.meta",
            "Assets/Resources/Luckbound/Rooms.meta",
            "Assets/Resources/Luckbound/Rooms/PrototypeFloorTile.asset",
            "Assets/Resources/Luckbound/Rooms/PrototypeFloorTile.asset.meta",
            "Assets/Resources/Luckbound/Rooms/PrototypeRoom.prefab",
            "Assets/Resources/Luckbound/Rooms/PrototypeRoom.prefab.meta",
            "ProjectSettings/EditorBuildSettings.asset",
            "ProjectSettings/ProjectSettings.asset"
        };

        [Test]
        public void StartupSceneGenerator_IsIdempotentAndLeavesNoProjectDiff()
        {
            Dictionary<string, string> before = SnapshotManagedFiles();
            string buildSettingsBefore = BuildSettingsFingerprint();

            ProjectScaffoldGenerator.GenerateStartupScenes();
            Dictionary<string, string> afterFirstRun = SnapshotManagedFiles();
            string buildSettingsAfterFirstRun = BuildSettingsFingerprint();

            ProjectScaffoldGenerator.GenerateStartupScenes();
            Dictionary<string, string> afterSecondRun = SnapshotManagedFiles();
            string buildSettingsAfterSecondRun = BuildSettingsFingerprint();

            AssertSnapshotsEqual(before, afterFirstRun, "The first reconciliation changed existing scaffold files.");
            AssertSnapshotsEqual(afterFirstRun, afterSecondRun, "The second reconciliation changed generated files.");
            Assert.That(buildSettingsAfterFirstRun, Is.EqualTo(buildSettingsBefore),
                "The generator unexpectedly changed the existing build-scene order.");
            Assert.That(buildSettingsAfterSecondRun, Is.EqualTo(buildSettingsAfterFirstRun),
                "The generator added duplicate or reordered build entries on its second run.");
        }

        [Test]
        public void DevelopmentCheckDriver_CompletesForTheGeneratedScaffold()
        {
            LogAssert.Expect(LogType.Log, "Luckbound project checks passed for Unity " + Application.unityVersion + ".");

            Assert.DoesNotThrow(() => ProjectDevelopmentChecks.RunAll());
        }

        [Test]
        public void MovementDemoCourseSprites_ResolveCanonicalStoneSpriteFromCheckout()
        {
            const string scenePath = "Assets/Luckbound/Scenes/Expedition.unity";
            string[] rendererNames =
            {
                "StoneBackdrop",
                "TrainingFloor",
                "JumpBarrierOne",
                "JumpBarrierTwo",
                "JumpBarrierThree",
                "MovementDemoPortal"
            };

            Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>(MovementDemoScaffoldGenerator.StoneSpritePath);
            Assert.That(stone, Is.Not.Null, "The canonical single StonePrototype Sprite must load.");
            AssertImportedSpriteGeometry(stone, 16, 16, new Vector2(8f, 8f), "StonePrototype");
            AssertSpriteIdentity(stone, StoneSpriteGuid, "StonePrototype");

            UnityEngine.SceneManagement.Scene previewScene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                GameObject courseRoot = previewScene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == "MovementDemoCourse");
                Assert.That(courseRoot, Is.Not.Null, "Expedition checkout scene must contain MovementDemoCourse.");
                Assert.That(courseRoot.GetComponentsInChildren<SpriteRenderer>(true).Length,
                    Is.EqualTo(rendererNames.Length), "MovementDemoCourse must contain exactly six SpriteRenderers.");

                foreach (string name in rendererNames)
                {
                    Transform child = courseRoot.transform.Find(name);
                    SpriteRenderer renderer = child == null ? null : child.GetComponent<SpriteRenderer>();
                    Assert.That(renderer, Is.Not.Null, "Missing course SpriteRenderer: " + name);
                    Assert.That(renderer.sprite, Is.SameAs(stone),
                        name + " must reference the canonical imported StonePrototype Sprite.");
                }
            }
            finally
            {
                if (previewScene.IsValid() && previewScene.isLoaded)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }
        }

        [Test]
        public void MovementDemoAvatar_ImportsBottomPivotAndKeepsConsumerBindings()
        {
            Sprite avatar = AssetDatabase.LoadAssetAtPath<Sprite>(MovementDemoScaffoldGenerator.AvatarSpritePath);
            Assert.That(avatar, Is.Not.Null, "The canonical single BellatorPrototype Sprite must load.");
            AssertImportedSpriteGeometry(avatar, 32, 48, new Vector2(16f, 0f), "BellatorPrototype");
            long avatarLocalFileId = AssertSpriteIdentity(avatar, AvatarSpriteGuid, "BellatorPrototype");

            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MovementDemoScaffoldGenerator.PlayerPrefabPath);
            Assert.That(playerPrefab, Is.Not.Null, "MovementDemoPlayer must load from the canonical prefab path.");
            AssertPlayerPrefabSprite(playerPrefab, avatar, avatarLocalFileId, "MovementDemoPlayer prefab");

            AssertPlayerCompositionBinding<Luckbound.App.MovementDemoComposition>(
                "Assets/Luckbound/Scenes/Expedition.unity", avatar, avatarLocalFileId, "movement demo");
            AssertPlayerCompositionBinding<Luckbound.App.CombatArenaComposition>(
                "Assets/Luckbound/Scenes/CombatArena.unity", avatar, avatarLocalFileId, "C08 combat arena");
            AssertPlayerCompositionBinding<Luckbound.App.CustodianArenaComposition>(
                "Assets/Luckbound/Scenes/CustodianArena.unity", avatar, avatarLocalFileId, "Custodian arena");
        }

        private static void AssertImportedSpriteGeometry(
            Sprite sprite,
            int expectedWidth,
            int expectedHeight,
            Vector2 expectedPivotPixels,
            string description)
        {
            Assert.That(sprite.rect.x, Is.EqualTo(0f).Within(ImportedSpriteTolerance), description + " rect x");
            Assert.That(sprite.rect.y, Is.EqualTo(0f).Within(ImportedSpriteTolerance), description + " rect y");
            Assert.That(sprite.rect.width, Is.EqualTo(expectedWidth).Within(ImportedSpriteTolerance), description + " rect width");
            Assert.That(sprite.rect.height, Is.EqualTo(expectedHeight).Within(ImportedSpriteTolerance), description + " rect height");
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(16f).Within(ImportedSpriteTolerance), description + " pixels per unit");
            Assert.That(sprite.pivot.x, Is.EqualTo(expectedPivotPixels.x).Within(ImportedSpriteTolerance), description + " pivot x in pixels");
            Assert.That(sprite.pivot.y, Is.EqualTo(expectedPivotPixels.y).Within(ImportedSpriteTolerance), description + " pivot y in pixels");
        }

        private static long AssertSpriteIdentity(Sprite sprite, string expectedGuid, string description)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(
                sprite, out string spriteGuid, out long localFileId), Is.True,
                description + " must expose its imported Sprite identity.");
            Assert.That(spriteGuid, Is.EqualTo(expectedGuid), description + " GUID");
            Assert.That(localFileId, Is.Not.EqualTo(0L), description + " localFileID");
            TestContext.WriteLine(description + " GUID=" + spriteGuid + "; localFileID=" + localFileId);
            return localFileId;
        }

        private static void AssertPlayerPrefabSprite(
            GameObject playerPrefab,
            Sprite expectedAvatar,
            long expectedLocalFileId,
            string description)
        {
            Assert.That(AssetDatabase.GetAssetPath(playerPrefab),
                Is.EqualTo(MovementDemoScaffoldGenerator.PlayerPrefabPath), description + " asset path");
            SpriteRenderer playerRenderer = playerPrefab.GetComponent<SpriteRenderer>();
            Assert.That(playerRenderer, Is.Not.Null, description + " SpriteRenderer");
            Assert.That(playerRenderer.sprite, Is.SameAs(expectedAvatar), description + " imported Avatar binding");
            long actualLocalFileId = AssertSpriteIdentity(playerRenderer.sprite, AvatarSpriteGuid, description + " Avatar");
            Assert.That(actualLocalFileId, Is.EqualTo(expectedLocalFileId), description + " Avatar localFileID");
        }

        private static void AssertPlayerCompositionBinding<TComposition>(
            string scenePath,
            Sprite expectedAvatar,
            long expectedAvatarLocalFileId,
            string description)
            where TComposition : Component
        {
            UnityEngine.SceneManagement.Scene previewScene =
                UnityEditor.SceneManagement.EditorSceneManager.OpenPreviewScene(scenePath);
            try
            {
                TComposition composition = previewScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<TComposition>(true))
                    .FirstOrDefault();
                Assert.That(composition, Is.Not.Null, description + " composition must load from the checkout scene.");

                var serializedComposition = new SerializedObject(composition);
                SerializedProperty playerPrefabProperty = serializedComposition.FindProperty("_playerPrefab");
                Assert.That(playerPrefabProperty, Is.Not.Null, description + " _playerPrefab property");
                GameObject playerPrefab = playerPrefabProperty.objectReferenceValue as GameObject;
                Assert.That(playerPrefab, Is.Not.Null, description + " _playerPrefab binding");
                AssertPlayerPrefabSprite(playerPrefab, expectedAvatar, expectedAvatarLocalFileId, description);
            }
            finally
            {
                if (previewScene.IsValid() && previewScene.isLoaded)
                {
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(previewScene);
                }
            }
        }

        private static Dictionary<string, string> SnapshotManagedFiles()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            var hashes = new Dictionary<string, string>();
            using (SHA256 sha256 = SHA256.Create())
            {
                foreach (string relativePath in ManagedFiles)
                {
                    string path = Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                    Assert.That(File.Exists(path), Is.True, "Expected generated file is missing: " + relativePath);
                    byte[] contents = File.ReadAllBytes(path);
                    if (Path.GetExtension(path) != ".png")
                    {
                        // Unity may resave text assets with LF on Windows; image fixtures remain byte-exact.
                        string text = Encoding.UTF8.GetString(contents);
                        contents = Encoding.UTF8.GetBytes(text.Replace("\r\n", "\n").Replace("\r", "\n"));
                    }
                    byte[] hash = sha256.ComputeHash(contents);
                    hashes.Add(relativePath, string.Concat(hash.Select(value => value.ToString("x2"))));
                }
            }

            return hashes;
        }

        private static string BuildSettingsFingerprint()
        {
            return string.Join("\n", (EditorBuildSettings.scenes ?? new EditorBuildSettingsScene[0])
                .Select(scene => scene.path + "|" + scene.enabled));
        }

        private static void AssertSnapshotsEqual(
            IReadOnlyDictionary<string, string> expected,
            IReadOnlyDictionary<string, string> actual,
            string message)
        {
            CollectionAssert.AreEquivalent(expected.Keys, actual.Keys, message);
            foreach (string path in expected.Keys)
            {
                Assert.That(actual[path], Is.EqualTo(expected[path]), message + " File: " + path);
            }
        }
    }
}
