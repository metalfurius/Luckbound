using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luckbound.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.Editor
{
    /// <summary>
    /// Creates missing startup scenes and reconciles only the build settings it owns.
    /// </summary>
    public static class ProjectScaffoldGenerator
    {
        private const string SceneDirectory = "Assets/Luckbound/Scenes";
        private static readonly string[] SceneNames = { "Bootstrap", "Menu", "Hub", "Expedition" };

        [MenuItem("Luckbound/Generate Startup Scenes")]
        public static void GenerateStartupScenes()
        {
            Directory.CreateDirectory(SceneDirectory);
            Scene previousActiveScene = SceneManager.GetActiveScene();
            int createdScenes = 0;

            try
            {
                foreach (string sceneName in SceneNames)
                {
                    string scenePath = ScenePath(sceneName);
                    if (File.Exists(scenePath))
                    {
                        continue;
                    }

                    Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                    EditorSceneManager.SetActiveScene(scene);
                    scene.name = sceneName;

                    try
                    {
                        if (sceneName == "Bootstrap")
                        {
                            CreateBootstrapScene();
                        }
                        else
                        {
                            CreateCamera("#10111B");
                            if (sceneName == "Menu")
                            {
                                CreateMenu();
                            }
                        }

                        if (!EditorSceneManager.SaveScene(scene, scenePath))
                        {
                            throw new IOException("Unity could not save scene " + scenePath);
                        }

                        createdScenes++;
                    }
                    finally
                    {
                        RestoreActiveScene(previousActiveScene);
                        if (scene.IsValid() && scene.isLoaded)
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
            }
            finally
            {
                RestoreActiveScene(previousActiveScene);
            }

            if (createdScenes > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            EditorBuildSettingsScene[] mergedBuildScenes = MergeBuildScenes(EditorBuildSettings.scenes);
            if (!BuildScenesEqual(EditorBuildSettings.scenes, mergedBuildScenes))
            {
                EditorBuildSettings.scenes = mergedBuildScenes;
            }

            bool playerSettingsChanged = false;
            if (PlayerSettings.companyName != "CodeOverdose")
            {
                PlayerSettings.companyName = "CodeOverdose";
                playerSettingsChanged = true;
            }
            if (PlayerSettings.productName != "Luckbound")
            {
                PlayerSettings.productName = "Luckbound";
                playerSettingsChanged = true;
            }
            if (PlayerSettings.bundleVersion != "0.1.0")
            {
                PlayerSettings.bundleVersion = "0.1.0";
                playerSettingsChanged = true;
            }

            if (playerSettingsChanged)
            {
                AssetDatabase.SaveAssets();
            }

            Debug.Log(
                "Luckbound startup scaffold reconciled. Created " + createdScenes +
                " missing scene(s); existing scenes and unrelated build entries were preserved.");
        }

        private static EditorBuildSettingsScene[] MergeBuildScenes(EditorBuildSettingsScene[] existing)
        {
            var managedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var merged = new List<EditorBuildSettingsScene>();

            foreach (string sceneName in SceneNames)
            {
                string path = ScenePath(sceneName);
                managedPaths.Add(NormalizePath(path));
                merged.Add(new EditorBuildSettingsScene(path, true));
            }

            foreach (EditorBuildSettingsScene scene in existing ?? Array.Empty<EditorBuildSettingsScene>())
            {
                if (!managedPaths.Contains(NormalizePath(scene.path)))
                {
                    merged.Add(scene);
                }
            }

            return merged.ToArray();
        }

        private static bool BuildScenesEqual(
            EditorBuildSettingsScene[] current,
            EditorBuildSettingsScene[] expected)
        {
            if (current == null || expected == null || current.Length != expected.Length)
            {
                return false;
            }

            for (int index = 0; index < current.Length; index++)
            {
                if (!string.Equals(current[index].path, expected[index].path, StringComparison.Ordinal) ||
                    current[index].enabled != expected[index].enabled)
                {
                    return false;
                }
            }

            return true;
        }

        private static string NormalizePath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/');
        }

        private static void RestoreActiveScene(Scene previousActiveScene)
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                EditorSceneManager.SetActiveScene(previousActiveScene);
            }
        }

        private static void CreateBootstrapScene()
        {
            new GameObject("LuckboundAppRoot", typeof(ApplicationBootstrapper));
        }

        private static void CreateCamera(string backgroundHex)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = ParseColor(backgroundHex);
        }

        private static void CreateMenu()
        {
            var canvasObject = new GameObject(
                "MenuCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0.5f;

            Image background = CreateImage("Background", canvas.transform, "#10111B");
            Stretch(background.rectTransform);

            Image topRule = CreateImage("TopRule", canvas.transform, "#D68F32");
            Place(topRule.rectTransform, new Vector2(0f, 94f), new Vector2(150f, 2f));

            Image bottomRule = CreateImage("BottomRule", canvas.transform, "#493363");
            Place(bottomRule.rectTransform, new Vector2(0f, -73f), new Vector2(88f, 1f));

            CreateText("TavernLabel", "ATRIUM GLORIAM", canvas.transform,
                new Vector2(0f, 75f), new Vector2(280f, 20f), 10, "#F0C15A", FontStyle.Bold);
            CreateText("LuckboundTitle", "LUCKBOUND", canvas.transform,
                new Vector2(0f, 26f), new Vector2(420f, 68f), 42, "#E8E3CD", FontStyle.Bold);
            CreateText("MenuTagline", "La fortuna no decide. Te ofrece el precio.", canvas.transform,
                new Vector2(0f, -22f), new Vector2(420f, 30f), 14, "#AA7DC2", FontStyle.Normal);
            CreateText("MenuStatus", "EL CASTILLO ESPERA", canvas.transform,
                new Vector2(0f, -99f), new Vector2(300f, 22f), 9, "#BDC6CC", FontStyle.Bold);
            CreateText("MenuVersion", "PREPRODUCCIÓN  ·  0.1.0", canvas.transform,
                new Vector2(0f, -126f), new Vector2(300f, 18f), 7, "#8290A5", FontStyle.Normal);
        }

        private static Image CreateImage(string name, Transform parent, string color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = ParseColor(color);
            image.raycastTarget = false;
            return image;
        }

        private static void CreateText(
            string name,
            string value,
            Transform parent,
            Vector2 position,
            Vector2 size,
            int fontSize,
            string color,
            FontStyle style)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);

            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
            {
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            if (text.font == null)
            {
                throw new InvalidOperationException("Unity's built-in UI font could not be loaded.");
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = ParseColor(color);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }

        private static void Place(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Color ParseColor(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                throw new ArgumentException("Invalid Luckbound palette color: " + hex);
            }

            return color;
        }

        private static string ScenePath(string sceneName)
        {
            return SceneDirectory + "/" + sceneName + ".unity";
        }
    }
}
