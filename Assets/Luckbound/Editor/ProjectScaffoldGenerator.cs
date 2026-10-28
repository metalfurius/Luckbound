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
        private static readonly string[] SceneNames = { "Bootstrap", "Menu", "Hub", "Expedition", "CombatArena", "CustodianArena" };

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
                                CreateMenu(scene);
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
            CustodianArenaScaffoldGenerator.EnsureGenerated();
            RoomTemplateScaffoldGenerator.EnsureGenerated();
            HubPreparationScaffoldGenerator.EnsureGenerated();
            ExpeditionRunSessionScaffoldGenerator.EnsureGenerated();

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

        private static void CreateMenu(Scene scene)
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
            Place(topRule.rectTransform, new Vector2(0f, 112f), new Vector2(150f, 2f));

            Image bottomRule = CreateImage("BottomRule", canvas.transform, "#493363");
            Place(bottomRule.rectTransform, new Vector2(0f, -83f), new Vector2(88f, 1f));

            CreateText("TavernLabel", "ATRIUM GLORIAM", canvas.transform,
                new Vector2(0f, 92f), new Vector2(280f, 18f), 14, "#F0C15A", FontStyle.Bold);
            CreateText("LuckboundTitle", "LUCKBOUND", canvas.transform,
                new Vector2(0f, 52f), new Vector2(420f, 52f), 42, "#E8E3CD", FontStyle.Bold);
            CreateText("MenuTagline", "La fortuna no decide. Te ofrece el precio.", canvas.transform,
                new Vector2(0f, 13f), new Vector2(440f, 22f), 14, "#AA7DC2", FontStyle.Normal);
            CreateText("MenuStatus", "EL CASTILLO ESPERA", canvas.transform,
                new Vector2(-110f, -109f), new Vector2(200f, 18f), 14, "#BDC6CC", FontStyle.Bold);
            CreateText("MenuVersion", "PREPRODUCCIÓN  ·  0.1.0", canvas.transform,
                new Vector2(110f, -109f), new Vector2(200f, 18f), 14, "#8290A5", FontStyle.Normal);
            ReconcileMenuVisualLayout(scene);
        }

        /// <summary>Applies the shared responsive menu hierarchy after any scaffold adds its entry.</summary>
        public static bool ReconcileMenuVisualLayout(Scene scene)
        {
            GameObject canvas = FindMenuObject(scene, "MenuCanvas");
            if (canvas == null) return false;
            bool changed = false;
            GameObject root = EnsureLayoutObject(scene, canvas.transform, "PrototypeMenuLayout", ref changed, out _);
            changed |= SetRect(root.GetComponent<RectTransform>(), new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-32f, 247f));

            GameObject buttonsRoot = EnsureLayoutObject(scene, root.transform, "PrototypeMenuButtons", ref changed, out _);
            GameObject rowOne = EnsureLayoutObject(scene, buttonsRoot.transform, "PrototypeMenuRow1", ref changed,
                out bool rowOneMigrated);
            GameObject rowTwo = EnsureLayoutObject(scene, buttonsRoot.transform, "PrototypeMenuRow2", ref changed,
                out bool rowTwoMigrated);
            GameObject footer = EnsureLayoutObject(scene, root.transform, "PrototypeMenuFooter", ref changed, out _);
            changed |= SetRect(buttonsRoot.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -115f), new Vector2(0f, 96f));
            if (rowOneMigrated)
            {
                changed |= SetRect(rowOne.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 44f));
            }
            if (rowTwoMigrated)
            {
                changed |= SetRect(rowTwo.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 44f));
            }
            changed |= SetRect(footer.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, -227f), new Vector2(0f, 20f));

            VerticalLayoutGroup buttonLayout = EnsureComponent<VerticalLayoutGroup>(buttonsRoot, ref changed);
            changed |= ConfigureVerticalLayout(buttonLayout, spacing: 8f, childControlWidth: true,
                childControlHeight: true, childForceExpandWidth: true, childForceExpandHeight: false);
            LayoutElement rowOneElement = EnsureComponent<LayoutElement>(rowOne, ref changed);
            changed |= ConfigureLayoutElement(rowOneElement, minWidth: -1f, preferredWidth: -1f,
                flexibleWidth: -1f, minHeight: 44f, preferredHeight: 44f, flexibleHeight: 0f);
            LayoutElement rowTwoElement = EnsureComponent<LayoutElement>(rowTwo, ref changed);
            changed |= ConfigureLayoutElement(rowTwoElement, minWidth: -1f, preferredWidth: -1f,
                flexibleWidth: -1f, minHeight: 44f, preferredHeight: 44f, flexibleHeight: 0f);
            HorizontalLayoutGroup rowOneLayout = EnsureComponent<HorizontalLayoutGroup>(rowOne, ref changed);
            changed |= ConfigureHorizontalLayout(rowOneLayout, spacing: 16f, childControlWidth: true,
                childControlHeight: true, childForceExpandWidth: true, childForceExpandHeight: true);
            HorizontalLayoutGroup rowTwoLayout = EnsureComponent<HorizontalLayoutGroup>(rowTwo, ref changed);
            changed |= ConfigureHorizontalLayout(rowTwoLayout, spacing: 16f, childControlWidth: true,
                childControlHeight: true, childForceExpandWidth: true, childForceExpandHeight: true);
            HorizontalLayoutGroup footerLayout = EnsureComponent<HorizontalLayoutGroup>(footer, ref changed);
            changed |= ConfigureHorizontalLayout(footerLayout, spacing: 16f, childControlWidth: true,
                childControlHeight: true, childForceExpandWidth: true, childForceExpandHeight: true);

            changed |= SetMenuText(scene, "TavernLabel", root.transform,
                new Vector2(0f, -8f), new Vector2(0f, 18f), 14, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            changed |= SetMenuText(scene, "LuckboundTitle", root.transform,
                new Vector2(0f, -29f), new Vector2(0f, 52f), 36, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            changed |= SetMenuText(scene, "MenuTagline", root.transform,
                new Vector2(0f, -85f), new Vector2(0f, 22f), 14, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f));
            changed |= SetMenuText(scene, "MenuStatus", footer.transform,
                Vector2.zero, new Vector2(0f, 20f), 14, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            changed |= SetMenuText(scene, "MenuVersion", footer.transform,
                Vector2.zero, new Vector2(0f, 20f), 14, TextAnchor.MiddleCenter,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
            changed |= SetMenuGraphic(scene, "TopRule", root.transform,
                new Vector2(0f, 0f), new Vector2(150f, 2f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
            changed |= SetMenuGraphic(scene, "BottomRule", root.transform,
                new Vector2(0f, -219f), new Vector2(88f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));

            string[] names =
            {
                "StartMovementDemoButton",
                "StartTavernButton",
                "StartCombatArenaButton",
                "StartCustodianArenaButton"
            };
            Button[] buttons = names
                .Select(name => FindMenuObject(scene, name))
                .Where(gameObject => gameObject != null)
                .Select(gameObject => gameObject.GetComponent<Button>())
                .Where(button => button != null)
                .ToArray();
            for (int index = 0; index < buttons.Length; index++)
            {
                Transform row = index < 2 ? rowOne.transform : rowTwo.transform;
                bool migrated = buttons[index].transform.parent != row;
                if (migrated)
                {
                    buttons[index].transform.SetParent(row, false);
                    changed = true;
                }
                if (buttons[index].transform.GetSiblingIndex() != index % 2)
                {
                    buttons[index].transform.SetSiblingIndex(index % 2);
                    changed = true;
                }
                changed |= SetMenuButton(buttons[index], migrated);
            }

            return changed;
        }

        /// <summary>Creates a top-stacked, intrinsic-height HUD while preserving its bound Text and Button components.</summary>
        public static bool ReconcilePrototypeHudLayout(Scene scene, string canvasName, string telemetryName,
            string statusName, string returnButtonName)
        {
            GameObject canvas = FindMenuObject(scene, canvasName);
            Text telemetry = FindMenuObject(scene, telemetryName)?.GetComponent<Text>();
            Text status = FindMenuObject(scene, statusName)?.GetComponent<Text>();
            Button returnButton = FindMenuObject(scene, returnButtonName)?.GetComponent<Button>();
            if (canvas == null || telemetry == null || status == null || returnButton == null) return false;

            bool changed = false;
            GameObject root = EnsureLayoutObject(scene, canvas.transform, canvasName + "Layout", ref changed, out _);
            GameObject statusRow = EnsureLayoutObject(scene, root.transform, root.name + "StatusRow", ref changed,
                out bool statusRowMigrated);
            changed |= SetHudRootRect(root.GetComponent<RectTransform>());
            if (statusRowMigrated)
            {
                changed |= SetRect(statusRow.GetComponent<RectTransform>(), new Vector2(0f, 1f), new Vector2(1f, 1f),
                    new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            }

            VerticalLayoutGroup rootLayout = EnsureComponent<VerticalLayoutGroup>(root, ref changed);
            changed |= ConfigureVerticalLayout(rootLayout, spacing: 8f, childControlWidth: true,
                childControlHeight: true, childForceExpandWidth: true, childForceExpandHeight: false);
            ContentSizeFitter rootFitter = EnsureComponent<ContentSizeFitter>(root, ref changed);
            changed |= ConfigureContentSizeFitter(rootFitter, ContentSizeFitter.FitMode.Unconstrained,
                ContentSizeFitter.FitMode.PreferredSize);
            HorizontalLayoutGroup rowLayout = EnsureComponent<HorizontalLayoutGroup>(statusRow, ref changed);
            changed |= ConfigureHorizontalLayout(rowLayout, spacing: 12f, childControlWidth: true,
                childControlHeight: true, childForceExpandWidth: false, childForceExpandHeight: false);

            bool telemetryMigrated = telemetry.transform.parent != root.transform;
            if (telemetryMigrated)
            {
                telemetry.transform.SetParent(root.transform, false);
                changed = true;
            }
            bool statusMigrated = status.transform.parent != statusRow.transform;
            if (statusMigrated)
            {
                status.transform.SetParent(statusRow.transform, false);
                changed = true;
            }
            bool returnButtonMigrated = returnButton.transform.parent != statusRow.transform;
            if (returnButtonMigrated)
            {
                returnButton.transform.SetParent(statusRow.transform, false);
                changed = true;
            }
            if (telemetry.transform.GetSiblingIndex() != 0)
            {
                telemetry.transform.SetSiblingIndex(0);
                changed = true;
            }
            if (status.transform.GetSiblingIndex() != 0)
            {
                status.transform.SetSiblingIndex(0);
                changed = true;
            }
            if (returnButton.transform.GetSiblingIndex() != 1)
            {
                returnButton.transform.SetSiblingIndex(1);
                changed = true;
            }

            changed |= SetHudText(telemetry, TextAnchor.UpperLeft, addFlexibleWidth: false,
                initializeLayoutRect: telemetryMigrated);
            changed |= SetHudText(status, TextAnchor.UpperLeft, addFlexibleWidth: true,
                initializeLayoutRect: statusMigrated);
            LayoutElement returnElement = EnsureComponent<LayoutElement>(returnButton.gameObject, ref changed);
            changed |= ConfigureLayoutElement(returnElement, minWidth: 148f, preferredWidth: 148f,
                flexibleWidth: 0f, minHeight: 32f, preferredHeight: 32f, flexibleHeight: 0f);
            RectTransform returnRect = returnButton.GetComponent<RectTransform>();
            if (returnButtonMigrated)
            {
                changed |= SetRect(returnRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(148f, 32f));
            }
            foreach (Text label in returnButton.GetComponentsInChildren<Text>(true))
            {
                changed |= SetHudButtonLabel(label, new Vector2(8f, 4f), new Vector2(-8f, -4f));
            }
            return changed;
        }

        private static bool SetMenuText(Scene scene, string objectName, Transform parent, Vector2 position,
            Vector2 size, int fontSize, TextAnchor alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
        {
            Text text = FindMenuObject(scene, objectName)?.GetComponent<Text>();
            if (text == null) return false;
            bool changed = false;
            bool migrated = text.transform.parent != parent;
            if (migrated)
            {
                text.transform.SetParent(parent, false);
                changed = true;
            }
            if (parent.name != "PrototypeMenuFooter" || migrated)
            {
                changed |= SetRect(text.GetComponent<RectTransform>(), anchorMin, anchorMax, pivot, position, size);
            }
            if (text.fontSize != fontSize || text.alignment != alignment || text.resizeTextForBestFit ||
                text.horizontalOverflow != HorizontalWrapMode.Wrap || text.verticalOverflow != VerticalWrapMode.Truncate)
            {
                text.fontSize = fontSize;
                text.alignment = alignment;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                EditorUtility.SetDirty(text);
                changed = true;
            }
            if (parent.name == "PrototypeMenuFooter")
            {
                LayoutElement element = EnsureComponent<LayoutElement>(text.gameObject, ref changed);
                changed |= ConfigureLayoutElement(element, minWidth: 0f, preferredWidth: 0f,
                    flexibleWidth: 1f, minHeight: 20f, preferredHeight: 20f, flexibleHeight: 0f);
            }
            return changed;
        }

        private static bool SetMenuGraphic(Scene scene, string objectName, Transform parent, Vector2 position,
            Vector2 size, Vector2 anchor, Vector2 pivot)
        {
            GameObject imageObject = FindMenuObject(scene, objectName);
            if (imageObject == null) return false;
            bool changed = false;
            if (imageObject.transform.parent != parent)
            {
                imageObject.transform.SetParent(parent, false);
                changed = true;
            }
            changed |= SetRect(imageObject.GetComponent<RectTransform>(), anchor, anchor, pivot, position, size);
            return changed;
        }

        private static bool SetMenuButton(Button button, bool initializeLayoutRect)
        {
            bool changed = false;
            RectTransform rect = button.GetComponent<RectTransform>();
            if (initializeLayoutRect)
            {
                changed |= SetRect(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0f, 44f));
            }
            LayoutElement element = EnsureComponent<LayoutElement>(button.gameObject, ref changed);
            changed |= ConfigureLayoutElement(element, minWidth: 0f, preferredWidth: 0f,
                flexibleWidth: 1f, minHeight: 44f, preferredHeight: 44f, flexibleHeight: 0f);
            foreach (Text label in button.GetComponentsInChildren<Text>(true))
            {
                RectTransform labelRect = label.GetComponent<RectTransform>();
                changed |= SetHudStretchRect(labelRect, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    new Vector2(8f, 4f), new Vector2(-8f, -4f), Vector2.zero);
                if (label.fontSize != 14 || label.resizeTextForBestFit || label.horizontalOverflow != HorizontalWrapMode.Wrap ||
                    label.verticalOverflow != VerticalWrapMode.Truncate)
                {
                    label.fontSize = 14;
                    label.resizeTextForBestFit = false;
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                    EditorUtility.SetDirty(label);
                    changed = true;
                }
            }
            return changed;
        }

        private static bool SetHudText(Text text, TextAnchor alignment, bool addFlexibleWidth,
            bool initializeLayoutRect)
        {
            bool changed = false;
            if (initializeLayoutRect)
            {
                changed |= SetHudStretchRect(text.GetComponent<RectTransform>(), new Vector2(0f, 1f),
                    new Vector2(1f, 1f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Vector2.zero);
            }
            if (text.fontSize != 14 || text.resizeTextForBestFit || text.alignment != alignment ||
                text.horizontalOverflow != HorizontalWrapMode.Wrap || text.verticalOverflow != VerticalWrapMode.Truncate)
            {
                text.fontSize = 14;
                text.resizeTextForBestFit = false;
                text.alignment = alignment;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                EditorUtility.SetDirty(text);
                changed = true;
            }
            if (addFlexibleWidth)
            {
                LayoutElement element = EnsureComponent<LayoutElement>(text.gameObject, ref changed);
                changed |= ConfigureLayoutElement(element, minWidth: 0f, preferredWidth: 0f,
                    flexibleWidth: 1f, minHeight: -1f, preferredHeight: -1f, flexibleHeight: 0f);
            }
            return changed;
        }

        private static bool SetHudButtonLabel(Text label, Vector2 offsetMin, Vector2 offsetMax)
        {
            bool changed = false;
            changed |= SetHudStretchRect(label.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                new Vector2(0.5f, 0.5f), offsetMin, offsetMax, Vector2.zero);
            if (label.fontSize != 14 || label.resizeTextForBestFit || label.horizontalOverflow != HorizontalWrapMode.Wrap ||
                label.verticalOverflow != VerticalWrapMode.Truncate)
            {
                label.fontSize = 14;
                label.resizeTextForBestFit = false;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
                EditorUtility.SetDirty(label);
                changed = true;
            }
            return changed;
        }

        private static GameObject EnsureLayoutObject(Scene scene, Transform parent, string name, ref bool changed,
            out bool createdOrMigrated)
        {
            GameObject gameObject = FindMenuObject(scene, name);
            createdOrMigrated = gameObject == null || gameObject.transform.parent != parent;
            if (gameObject == null)
            {
                gameObject = new GameObject(name, typeof(RectTransform));
                SceneManager.MoveGameObjectToScene(gameObject, scene);
                changed = true;
            }
            if (gameObject.transform.parent != parent)
            {
                gameObject.transform.SetParent(parent, false);
                changed = true;
            }
            return gameObject;
        }

        private static bool SetHudRootRect(RectTransform rect)
        {
            bool changed = rect.anchorMin != new Vector2(0f, 1f) || rect.anchorMax != new Vector2(1f, 1f) ||
                rect.pivot != new Vector2(0.5f, 1f) || rect.sizeDelta.x != -32f ||
                rect.offsetMin.x != 16f || rect.offsetMax.x != -16f ||
                rect.anchoredPosition != new Vector2(0f, -16f);
            if (!changed) return false;

            float preferredHeight = rect.sizeDelta.y;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(-32f, preferredHeight);
            Vector2 offsetMin = rect.offsetMin;
            offsetMin.x = 16f;
            rect.offsetMin = offsetMin;
            Vector2 offsetMax = rect.offsetMax;
            offsetMax.x = -16f;
            rect.offsetMax = offsetMax;
            rect.anchoredPosition = new Vector2(0f, -16f);
            EditorUtility.SetDirty(rect);
            return true;
        }

        private static T EnsureComponent<T>(GameObject gameObject, ref bool changed) where T : Component
        {
            T component = gameObject.GetComponent<T>();
            if (component != null) return component;
            component = gameObject.AddComponent<T>();
            EditorUtility.SetDirty(gameObject);
            changed = true;
            return component;
        }

        private static bool ConfigureVerticalLayout(VerticalLayoutGroup group, float spacing, bool childControlWidth,
            bool childControlHeight, bool childForceExpandWidth, bool childForceExpandHeight)
        {
            bool changed = false;
            changed |= SetGroupProperties(group, spacing, childControlWidth, childControlHeight,
                childForceExpandWidth, childForceExpandHeight);
            if (group.padding.left != 0 || group.padding.right != 0 || group.padding.top != 0 || group.padding.bottom != 0)
            {
                group.padding = new RectOffset(0, 0, 0, 0);
                changed = true;
            }
            return changed;
        }

        private static bool ConfigureHorizontalLayout(HorizontalLayoutGroup group, float spacing,
            bool childControlWidth, bool childControlHeight, bool childForceExpandWidth, bool childForceExpandHeight)
        {
            bool changed = false;
            changed |= SetGroupProperties(group, spacing, childControlWidth, childControlHeight,
                childForceExpandWidth, childForceExpandHeight);
            if (group.padding.left != 0 || group.padding.right != 0 || group.padding.top != 0 || group.padding.bottom != 0)
            {
                group.padding = new RectOffset(0, 0, 0, 0);
                changed = true;
            }
            return changed;
        }

        private static bool SetGroupProperties(HorizontalOrVerticalLayoutGroup group, float spacing,
            bool childControlWidth, bool childControlHeight, bool childForceExpandWidth, bool childForceExpandHeight)
        {
            bool changed = group.spacing != spacing || group.childAlignment != TextAnchor.UpperLeft ||
                group.childControlWidth != childControlWidth || group.childControlHeight != childControlHeight ||
                group.childForceExpandWidth != childForceExpandWidth || group.childForceExpandHeight != childForceExpandHeight;
            if (!changed) return false;
            group.spacing = spacing;
            group.childAlignment = TextAnchor.UpperLeft;
            group.childControlWidth = childControlWidth;
            group.childControlHeight = childControlHeight;
            group.childForceExpandWidth = childForceExpandWidth;
            group.childForceExpandHeight = childForceExpandHeight;
            EditorUtility.SetDirty(group);
            return true;
        }

        private static bool ConfigureContentSizeFitter(ContentSizeFitter fitter,
            ContentSizeFitter.FitMode horizontalFit, ContentSizeFitter.FitMode verticalFit)
        {
            if (fitter.horizontalFit == horizontalFit && fitter.verticalFit == verticalFit) return false;
            fitter.horizontalFit = horizontalFit;
            fitter.verticalFit = verticalFit;
            EditorUtility.SetDirty(fitter);
            return true;
        }

        private static bool ConfigureLayoutElement(LayoutElement element, float minWidth, float preferredWidth,
            float flexibleWidth, float minHeight, float preferredHeight, float flexibleHeight)
        {
            bool changed = element.ignoreLayout || element.minWidth != minWidth || element.preferredWidth != preferredWidth ||
                element.flexibleWidth != flexibleWidth || element.minHeight != minHeight ||
                element.preferredHeight != preferredHeight || element.flexibleHeight != flexibleHeight;
            if (!changed) return false;
            element.ignoreLayout = false;
            element.minWidth = minWidth;
            element.preferredWidth = preferredWidth;
            element.flexibleWidth = flexibleWidth;
            element.minHeight = minHeight;
            element.preferredHeight = preferredHeight;
            element.flexibleHeight = flexibleHeight;
            EditorUtility.SetDirty(element);
            return true;
        }

        private static bool SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null) return false;
            if (rect.anchorMin == anchorMin && rect.anchorMax == anchorMax && rect.pivot == pivot &&
                rect.anchoredPosition == anchoredPosition && rect.sizeDelta == sizeDelta) return false;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
            EditorUtility.SetDirty(rect);
            return true;
        }

        private static bool SetHudStretchRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 offsetMin, Vector2 offsetMax, Vector2 anchoredPosition)
        {
            if (rect == null) return false;
            if (rect.anchorMin == anchorMin && rect.anchorMax == anchorMax && rect.pivot == pivot &&
                rect.offsetMin == offsetMin && rect.offsetMax == offsetMax && rect.anchoredPosition == anchoredPosition) return false;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.anchoredPosition = anchoredPosition;
            EditorUtility.SetDirty(rect);
            return true;
        }

        private static GameObject FindMenuObject(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .FirstOrDefault(gameObject => gameObject.name == objectName);
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
