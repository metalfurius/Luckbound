using System;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Gameplay.Inventory;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Luckbound.Editor
{
    /// <summary>Creates an Editor-only scene for the isolated technical F10 run.</summary>
    public static class F10DiagnosticSceneGenerator
    {
        public const string ScenePath = "Assets/Luckbound/Editor/Diagnostic/F10Diagnostic.unity";

        [MenuItem("Luckbound/Technical Diagnostic/Generate F10 Diagnostic Scene")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("The diagnostic scene can only be generated outside Play Mode.");
            string sceneFile = Path.Combine(Directory.GetParent(Application.dataPath).FullName, ScenePath);
            if (File.Exists(sceneFile) || File.Exists(sceneFile + ".meta") || Directory.Exists(sceneFile) ||
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ScenePath) != null ||
                SceneManager.GetSceneByName("F10Diagnostic").isLoaded)
                throw new IOException("The diagnostic scene destination is occupied; existing content was preserved.");

            GameObject[] rooms = F10TechnicalDiagnosticPrefabGenerator.GenerateAll().ToArray();
            if (rooms.Length != 5) throw new InvalidOperationException("The F10 route requires exactly five diagnostic rooms.");
            GameObject player = LoadOrGeneratePlayer();
            GameObject cache = LoadOrGenerateCache();
            string[] templates = { "room.sample.tutorial", "room.patio.c1", "room.patio.e1",
                "room.patio.guardian", "room.sample.extraction" };
            var entries = templates.Select((id, index) => new ExpeditionRoomPrefabEntry
                { templateId = id, prefab = rooms[index] }).ToArray();
            foreach (ExpeditionRoomPrefabEntry entry in entries)
                if (!ExpeditionRoomPrefabRegistry.TryResolve(entry.templateId, entries, out _, out string error))
                    throw new InvalidOperationException(error);

            Scene previous = SceneManager.GetActiveScene();
            bool replaceEmptyBatchScene = Application.isBatchMode && SceneManager.sceneCount == 1 && previous.IsValid() &&
                string.IsNullOrEmpty(previous.path) && previous.rootCount == 0;
            Scene diagnostic = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceEmptyBatchScene ? NewSceneMode.Single : NewSceneMode.Additive);
            try
            {
                if (!diagnostic.IsValid() || !diagnostic.isLoaded)
                    throw new InvalidOperationException("The new diagnostic scene is invalid or not loaded.");
                if (SceneManager.GetActiveScene() != diagnostic)
                    SceneManager.SetActiveScene(diagnostic);
                if (SceneManager.GetActiveScene() != diagnostic)
                    throw new InvalidOperationException("Could not activate the new diagnostic scene; active scene is '" +
                        SceneManager.GetActiveScene().name + "', expected '" + diagnostic.name + "'.");
                var runnerObject = new GameObject("F10 Diagnostic Session");
                runnerObject.AddComponent<F10DiagnosticSceneRunner>().ConfigureForEditor(player, entries, cache);
                var lightObject = new GameObject("F10 Technical Global Light");
                lightObject.AddComponent<Light2D>().lightType = Light2D.LightType.Global;
                if (!EditorSceneManager.SaveScene(diagnostic, ScenePath))
                    throw new IOException("Unity did not save the F10 diagnostic scene.");
                if (!File.Exists(sceneFile) || !File.Exists(sceneFile + ".meta"))
                    throw new IOException("The diagnostic scene or its .meta sidecar is missing.");
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                if (replaceEmptyBatchScene)
                {
                    if (diagnostic.IsValid() && diagnostic.isLoaded)
                    {
                        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                    }
                }
                else
                {
                    if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                    if (diagnostic.IsValid() && diagnostic.isLoaded) EditorSceneManager.CloseScene(diagnostic, true);
                }
            }
        }

        private static GameObject LoadOrGeneratePlayer()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabPath);
            if (player == null) return F10TechnicalDiagnosticPlayerPrefabGenerator.Generate();
            if (!string.Equals(player.name, F10TechnicalDiagnosticPlayerPrefabGenerator.PrefabName, StringComparison.Ordinal) ||
                player.GetComponent<PlayerMotor2D>() == null || player.GetComponent<PlayerVisualStatePresenter>() == null ||
                player.GetComponent<ItemCollector2D>() == null || player.GetComponent<Rigidbody2D>() == null ||
                player.GetComponent<Collider2D>() == null || player.GetComponent<SpriteRenderer>() == null ||
                player.GetComponent<Animator>() == null || player.GetComponent<Animator>().runtimeAnimatorController == null)
                throw new InvalidOperationException("The existing technical player is incompatible; it was preserved.");
            return player;
        }

        private static GameObject LoadOrGenerateCache()
        {
            GameObject cache = AssetDatabase.LoadAssetAtPath<GameObject>(F10PhysicalGoldCacheDiagnosticPrefabGenerator.PrefabPath);
            if (cache == null) return F10PhysicalGoldCacheDiagnosticPrefabGenerator.Generate();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(F10PhysicalGoldCacheDiagnosticPrefabGenerator.StonePrototypeSpritePath);
            F10PhysicalGoldCacheDiagnosticPrefabGenerator.ValidatePrefabRoot(cache, sprite,
                cache.GetComponent<ExplorationGoldCachePickup2D>());
            return cache;
        }
    }
}
