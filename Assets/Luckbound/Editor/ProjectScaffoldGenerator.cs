using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
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
        private const string PlayerVisualControllerPath = "Assets/Resources/Luckbound/PlayerVisualStates.controller";
        private static readonly string[] SceneNames = { "Bootstrap", "Menu", "Hub", "Expedition", "CombatArena" };

        [MenuItem("Luckbound/Generate Startup Scenes")]
        public static void GenerateStartupScenes()
        {
            Directory.CreateDirectory(SceneDirectory);
            Scene batchUntitledScene = SceneManager.GetActiveScene();
            if (Application.isBatchMode && batchUntitledScene.IsValid() && string.IsNullOrEmpty(batchUntitledScene.path) &&
                batchUntitledScene.rootCount == 0 && File.Exists(ScenePath("Menu")))
            {
                // Batch-mode Editors start with an unsaved empty scene, which Unity cannot keep open additively.
                // Replacing that editor-owned placeholder lets the non-destructive generators run normally.
                EditorSceneManager.OpenScene(ScenePath("Menu"), OpenSceneMode.Single);
            }
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

            EnsurePlayerVisualController();
            EnsureExpeditionCameraRig();
            MovementDemoScaffoldGenerator.EnsureGenerated();
            CombatArenaScaffoldGenerator.EnsureGenerated();

            if (createdScenes > 0)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }

            AssetDatabase.SaveAssets();

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

        private static void EnsureExpeditionCameraRig()
        {
            string scenePath = ScenePath("Expedition");
            if (!File.Exists(scenePath))
            {
                throw new FileNotFoundException("Expedition scene is required before the camera rig can be generated.", scenePath);
            }

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                GameObject cameraObject = roots.SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => transform.gameObject)
                    .FirstOrDefault(gameObject => gameObject.name == "Main Camera" && gameObject.GetComponent<Camera>() != null);
                if (cameraObject == null)
                {
                    throw new InvalidOperationException("Expedition must contain one Main Camera before B06 can configure its rig.");
                }

                bool changed = false;
                if (cameraObject.tag != "MainCamera")
                {
                    cameraObject.tag = "MainCamera";
                    changed = true;
                }
                if (cameraObject.GetComponent<AudioListener>() == null)
                {
                    cameraObject.AddComponent<AudioListener>();
                    changed = true;
                }
                if (cameraObject.GetComponent<PixelPerfectCamera>() == null)
                {
                    cameraObject.AddComponent<PixelPerfectCamera>();
                    changed = true;
                }
                if (cameraObject.GetComponent<CinemachineBrain>() == null)
                {
                    cameraObject.AddComponent<CinemachineBrain>();
                    changed = true;
                }
                RoomCameraRig2D rig = cameraObject.GetComponent<RoomCameraRig2D>();
                if (rig == null)
                {
                    rig = cameraObject.AddComponent<RoomCameraRig2D>();
                    changed = true;
                }

                if (!CameraRigIsConfigured(cameraObject))
                {
                    rig.Configure();
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, scenePath))
                    {
                        throw new IOException("Unity could not save the configured Expedition camera rig.");
                    }
                }
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

        private static bool CameraRigIsConfigured(GameObject cameraObject)
        {
            Camera output = cameraObject.GetComponent<Camera>();
            PixelPerfectCamera pixelPerfect = cameraObject.GetComponent<PixelPerfectCamera>();
            CinemachineBrain brain = cameraObject.GetComponent<CinemachineBrain>();
            RoomCameraRig2D rig = cameraObject.GetComponent<RoomCameraRig2D>();
            if (output == null || pixelPerfect == null || brain == null || rig == null ||
                cameraObject.name != "Main Camera" || cameraObject.tag != "MainCamera" || !output.orthographic ||
                !Mathf.Approximately(output.orthographicSize, RoomCameraRig2D.OrthographicSize) ||
                pixelPerfect.assetsPPU != RoomCameraRig2D.AssetsPixelsPerUnit ||
                pixelPerfect.refResolutionX != RoomCameraRig2D.ReferenceWidth ||
                pixelPerfect.refResolutionY != RoomCameraRig2D.ReferenceHeight ||
                pixelPerfect.gridSnapping != PixelPerfectCamera.GridSnapping.UpscaleRenderTexture ||
                pixelPerfect.cropFrame != PixelPerfectCamera.CropFrame.Windowbox)
            {
                return false;
            }

            CinemachineCamera virtualCamera = cameraObject.GetComponentInChildren<CinemachineCamera>(true);
            CinemachineFollow follow = virtualCamera == null ? null : virtualCamera.GetComponent<CinemachineFollow>();
            CinemachineConfiner2D confiner = virtualCamera == null ? null : virtualCamera.GetComponent<CinemachineConfiner2D>();
            if (virtualCamera == null || follow == null || confiner == null ||
                virtualCamera.gameObject.name != "Cinemachine Camera")
            {
                return false;
            }

            Vector3 damping = follow.TrackerSettings.PositionDamping;
            LensSettings lens = virtualCamera.Lens;
            return Mathf.Approximately(damping.x, RoomCameraRig2D.HorizontalDamping) &&
                Mathf.Approximately(damping.y, RoomCameraRig2D.VerticalDamping) &&
                Mathf.Approximately(confiner.Damping, 0f) &&
                Mathf.Approximately(confiner.SlowingDistance, 0f) &&
                lens.ModeOverride == LensSettings.OverrideModes.Orthographic &&
                Mathf.Approximately(lens.OrthographicSize, RoomCameraRig2D.OrthographicSize);
        }

        private static void EnsurePlayerVisualController()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(PlayerVisualControllerPath);
            bool changed = false;
            if (controller == null)
            {
                string directory = Path.GetDirectoryName(PlayerVisualControllerPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                controller = AnimatorController.CreateAnimatorControllerAtPath(PlayerVisualControllerPath);
                changed = true;
            }

            changed |= EnsureAnimatorParameter(controller, "VisualState", AnimatorControllerParameterType.Int);
            changed |= EnsureAnimatorParameter(controller, "VelocityX", AnimatorControllerParameterType.Float);
            changed |= EnsureAnimatorParameter(controller, "VelocityY", AnimatorControllerParameterType.Float);
            changed |= EnsureAnimatorParameter(controller, "FacingX", AnimatorControllerParameterType.Float);
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState[] states = new AnimatorState[7];
            string[] stateNames = { "Idle", "Moving", "Airborne", "Attacking", "Dodging", "Stunned", "Dead" };
            for (int stateId = 0; stateId < stateNames.Length; stateId++)
            {
                states[stateId] = stateMachine.states.Select(child => child.state)
                    .FirstOrDefault(state => state != null && state.name == stateNames[stateId]);
                if (states[stateId] == null)
                {
                    states[stateId] = stateMachine.AddState(stateNames[stateId]);
                    changed = true;
                }
                if (stateId == 0 && stateMachine.defaultState == null)
                {
                    stateMachine.defaultState = states[stateId];
                    changed = true;
                }

                bool transitionExists = stateMachine.anyStateTransitions.Any(transition =>
                    transition.destinationState == states[stateId] && transition.conditions.Any(condition =>
                        condition.mode == AnimatorConditionMode.Equals &&
                        condition.parameter == "VisualState" && condition.threshold == stateId));
                if (!transitionExists)
                {
                    AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(states[stateId]);
                    transition.duration = 0f;
                    transition.hasExitTime = false;
                    transition.canTransitionToSelf = false;
                    transition.AddCondition(AnimatorConditionMode.Equals, stateId, "VisualState");
                    changed = true;
                }
            }

            if (changed)
            {
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
            }
        }

        private static bool EnsureAnimatorParameter(
            AnimatorController controller,
            string name,
            AnimatorControllerParameterType type)
        {
            if (!controller.parameters.Any(parameter => parameter.name == name))
            {
                controller.AddParameter(name, type);
                return true;
            }

            return false;
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
