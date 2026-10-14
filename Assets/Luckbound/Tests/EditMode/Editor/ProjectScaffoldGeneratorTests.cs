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
