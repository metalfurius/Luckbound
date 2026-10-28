using System;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Gameplay.Movement;
using Luckbound.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.Editor
{
    /// <summary>Creates the explicitly provisional, playable B07 movement course without replacing user content.</summary>
    public static class MovementDemoScaffoldGenerator
    {
        public const string AvatarSpritePath = "Assets/Luckbound/Art/Prototypes/MovementDemo/BellatorPrototype.png";
        public const string StoneSpritePath = "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png";
        public const string PlayerPrefabPath = "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab";

        private const string VisualControllerPath = "Assets/Resources/Luckbound/PlayerVisualStates.controller";
        private const float FloorTop = -3.5f;

        public static void EnsureGenerated()
        {
            EnsureAssetFolder("Assets/Luckbound/Art");
            EnsureAssetFolder("Assets/Luckbound/Art/Prototypes");
            EnsureAssetFolder("Assets/Luckbound/Art/Prototypes/MovementDemo");
            EnsureAssetFolder("Assets/Luckbound/Prefabs");

            bool spriteAssetsChanged = EnsurePrototypeSprites();
            if (spriteAssetsChanged)
            {
                // A newly configured texture can still be represented by its pre-reimport object in this Editor pass.
                // Force one synchronous post-configuration import before any prefab stores the Sprite reference.
                AssetDatabase.ImportAsset(AvatarSpritePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.ImportAsset(StoneSpritePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            }
            Sprite avatar = AssetDatabase.LoadAssetAtPath<Sprite>(AvatarSpritePath);
            Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>(StoneSpritePath);
            if (avatar == null || stone == null)
            {
                throw new InvalidOperationException("B07 prototype sprites could not be imported as Unity sprites.");
            }

            GameObject playerPrefab = EnsurePlayerPrefab(avatar);
            EnsureMenuEntry();
            EnsureExpeditionCourse(playerPrefab, stone);
        }

        private static bool EnsurePrototypeSprites()
        {
            bool changed = EnsureTextureAsset(
                AvatarSpritePath, 32, 48, BuildAvatarPixels(), TextureWrapMode.Clamp, new Vector2(0.5f, 0f));
            changed |= EnsureTextureAsset(
                StoneSpritePath, 16, 16, BuildStonePixels(), TextureWrapMode.Repeat, new Vector2(0.5f, 0.5f));
            return changed;
        }

        private static bool EnsureTextureAsset(
            string assetPath,
            int width,
            int height,
            Color32[] pixels,
            TextureWrapMode wrapMode,
            Vector2 pivot)
        {
            bool changed = false;
            if (!File.Exists(assetPath))
            {
                var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = wrapMode
                };
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(assetPath, texture.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                changed = true;
            }

            var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Could not configure prototype texture importer for " + assetPath + ".");
            }

            var textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; changed = true; }
            if (importer.spritePixelsPerUnit != 16f) { importer.spritePixelsPerUnit = 16f; changed = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; changed = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; changed = true; }
            if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }
            if (importer.wrapMode != wrapMode) { importer.wrapMode = wrapMode; changed = true; }
            if (textureSettings.spriteMeshType != SpriteMeshType.FullRect)
            {
                textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(textureSettings);
                changed = true;
            }
            if (importer.spritePivot != pivot) { importer.spritePivot = pivot; changed = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
            return changed;
        }

        private static Color32[] BuildAvatarPixels()
        {
            const int width = 32;
            const int height = 48;
            var pixels = new Color32[width * height];
            Color32 outline = Color32FromHex("#10111B");
            Color32 steel = Color32FromHex("#59647A");
            Color32 steelLight = Color32FromHex("#BDC6CC");
            Color32 steelDark = Color32FromHex("#2A2E40");
            Color32 leather = Color32FromHex("#50382B");
            Color32 leatherLight = Color32FromHex("#A7794D");
            Color32 amber = Color32FromHex("#F0C15A");
            Color32 amberDark = Color32FromHex("#9A6026");
            Color32 face = Color32FromHex("#E8E3CD");

            FillRect(pixels, width, 5, 0, 15, 6, outline);
            FillRect(pixels, width, 18, 0, 28, 6, outline);
            FillRect(pixels, width, 6, 1, 14, 5, leather);
            FillRect(pixels, width, 19, 1, 27, 5, leather);
            FillRect(pixels, width, 8, 6, 25, 20, outline);
            FillRect(pixels, width, 10, 7, 14, 18, steelDark);
            FillRect(pixels, width, 19, 7, 23, 18, steelDark);
            FillRect(pixels, width, 11, 8, 13, 17, steel);
            FillRect(pixels, width, 20, 8, 22, 17, steel);
            FillRect(pixels, width, 8, 17, 25, 21, outline);
            FillRect(pixels, width, 9, 18, 24, 20, amberDark);
            FillRect(pixels, width, 14, 18, 18, 20, amber);

            FillRect(pixels, width, 4, 20, 28, 35, outline);
            FillRect(pixels, width, 7, 21, 25, 33, steelDark);
            FillRect(pixels, width, 8, 23, 24, 31, steel);
            FillRect(pixels, width, 11, 26, 21, 29, steelLight);
            FillRect(pixels, width, 14, 22, 18, 24, amberDark);
            FillRect(pixels, width, 4, 29, 9, 34, steelDark);
            FillRect(pixels, width, 23, 29, 28, 34, steelDark);
            FillRect(pixels, width, 2, 30, 7, 34, leatherLight);
            FillRect(pixels, width, 0, 32, 5, 37, amberDark);
            FillRect(pixels, width, 1, 34, 9, 37, amber);

            FillRect(pixels, width, 8, 33, 25, 46, outline);
            FillRect(pixels, width, 10, 34, 23, 44, steel);
            FillRect(pixels, width, 12, 35, 22, 42, steelLight);
            FillRect(pixels, width, 15, 37, 24, 40, outline);
            FillRect(pixels, width, 21, 38, 23, 39, amber);
            FillRect(pixels, width, 13, 43, 20, 45, steelDark);
            FillRect(pixels, width, 13, 45, 20, 48, outline);
            FillRect(pixels, width, 14, 46, 19, 47, amberDark);
            FillRect(pixels, width, 11, 40, 14, 42, face);
            SetPixel(pixels, width, 18, 41, face);
            return pixels;
        }

        private static Color32[] BuildStonePixels()
        {
            const int width = 16;
            const int height = 16;
            var pixels = new Color32[width * height];
            Color32 baseColor = Color32FromHex("#8290A5");
            Color32 shadow = Color32FromHex("#59647A");
            Color32 mid = Color32FromHex("#BDC6CC");
            Color32 edge = Color32FromHex("#E8E3CD");
            FillRect(pixels, width, 0, 0, width, height, baseColor);
            FillRect(pixels, width, 0, 14, width, 16, edge);
            FillRect(pixels, width, 0, 0, width, 1, shadow);
            FillRect(pixels, width, 7, 1, 8, 6, shadow);
            FillRect(pixels, width, 3, 7, 5, 8, mid);
            FillRect(pixels, width, 11, 9, 14, 10, mid);
            FillRect(pixels, width, 8, 3, 9, 5, edge);
            SetPixel(pixels, width, 2, 11, mid);
            SetPixel(pixels, width, 13, 4, shadow);
            SetPixel(pixels, width, 5, 2, mid);
            return pixels;
        }

        private static GameObject EnsurePlayerPrefab(Sprite avatar)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (existing != null)
            {
                SpriteRenderer existingRenderer = existing.GetComponent<SpriteRenderer>();
                Animator existingAnimator = existing.GetComponent<Animator>();
                bool isValid = existing.GetComponent<PlayerMotor2D>() != null &&
                    existing.GetComponent<PlayerVisualStatePresenter>() != null &&
                    existing.GetComponent<CapsuleCollider2D>() != null &&
                    existingRenderer != null && existingRenderer.sprite != null &&
                    AssetDatabase.GetAssetPath(existingRenderer.sprite) == AvatarSpritePath &&
                    Mathf.Approximately(existingRenderer.sprite.pixelsPerUnit, 16f) &&
                    existingAnimator != null && existingAnimator.runtimeAnimatorController != null;
                if (!isValid)
                {
                    Debug.LogWarning("Replacing the incomplete generated B07 player prefab with its deterministic scaffold.");
                    return CreatePlayerPrefab(avatar);
                }

                return existing;
            }

            return CreatePlayerPrefab(avatar);
        }

        private static GameObject CreatePlayerPrefab(Sprite avatar)
        {
            var player = new GameObject("MovementDemoPlayer");
            try
            {
                var body = player.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.sleepMode = RigidbodySleepMode2D.StartAwake;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                var collider = player.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(0.75f, 1.5f);
                collider.offset = new Vector2(0f, 0.75f);

                var renderer = player.AddComponent<SpriteRenderer>();
                renderer.sprite = avatar;
                renderer.sortingOrder = 10;

                var animator = player.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(VisualControllerPath);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                player.AddComponent<PlayerMotor2D>();
                player.AddComponent<PlayerVisualStatePresenter>();

                var cameraTarget = new GameObject("CameraTarget");
                cameraTarget.transform.SetParent(player.transform, false);
                cameraTarget.transform.localPosition = new Vector3(0f, 1.25f, 0f);

                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
                if (prefab == null)
                {
                    throw new IOException("Unity failed to save the B07 player prefab.");
                }
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        private static void EnsureMenuEntry()
        {
            const string scenePath = "Assets/Luckbound/Scenes/Menu.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                bool changed = EnsureEventSystem(scene);
                GameObject canvasObject = FindObject(scene, "MenuCanvas");
                if (canvasObject == null)
                {
                    throw new InvalidOperationException("Menu scene must contain MenuCanvas before B07 can add its entry.");
                }

                Button startButton = FindComponent<Button>(scene, "StartMovementDemoButton");
                if (startButton == null)
                {
                    startButton = CreateButton(
                        "StartMovementDemoButton",
                        "JUGAR DEMO 0.1",
                        canvasObject.transform,
                        new Vector2(0f, -18f),
                        new Vector2(204f, 28f),
                        14,
                        "#F0C15A",
                        "#32251F");
                    changed = true;
                }

                GameObject flowObject = FindObject(scene, "MenuNavigation");
                if (flowObject == null)
                {
                    flowObject = new GameObject("MenuNavigation");
                    SceneManager.MoveGameObjectToScene(flowObject, scene);
                    changed = true;
                }

                MenuNavigationController navigation = flowObject.GetComponent<MenuNavigationController>();
                if (navigation == null)
                {
                    navigation = flowObject.AddComponent<MenuNavigationController>();
                    changed = true;
                }
                changed |= SetObjectReference(navigation, "_startDemoButton", startButton);

                changed |= ProjectScaffoldGenerator.ReconcileMenuVisualLayout(scene);

                EventSystem eventSystem = FindEventSystem(scene);
                GameObject arenaButton = FindObject(scene, "StartCombatArenaButton");
                GameObject tavernButton = FindObject(scene, "StartTavernButton");
                GameObject firstSelected = tavernButton != null
                    ? tavernButton
                    : arenaButton != null ? arenaButton : startButton.gameObject;
                if (eventSystem != null && eventSystem.firstSelectedGameObject != firstSelected)
                {
                    eventSystem.firstSelectedGameObject = firstSelected;
                    EditorUtility.SetDirty(eventSystem);
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, scenePath))
                    {
                        throw new IOException("Unity failed to save the B07 menu entry.");
                    }
                }
            }
            finally
            {
                RestoreScene(scene, previousActiveScene);
            }
        }

        private static void EnsureExpeditionCourse(GameObject playerPrefab, Sprite stone)
        {
            const string scenePath = "Assets/Luckbound/Scenes/Expedition.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            try
            {
                bool changed = EnsureEventSystem(scene);
                GameObject courseRoot = FindObject(scene, "MovementDemoCourse");
                if (courseRoot != null)
                {
                    ValidateExistingCourse(scene, playerPrefab);
                    changed |= ReconcileExistingCourseVisuals(scene);
                    if (changed)
                    {
                        EditorSceneManager.MarkSceneDirty(scene);
                        if (!EditorSceneManager.SaveScene(scene, scenePath))
                        {
                            throw new IOException("Unity failed to save the repaired B07 EventSystem.");
                        }
                    }
                    return;
                }

                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer < 0)
                {
                    throw new InvalidOperationException("B07 requires the Ground layer created by B03.");
                }

                courseRoot = new GameObject("MovementDemoCourse");
                SceneManager.MoveGameObjectToScene(courseRoot, scene);

                GameObject boundsObject = new GameObject("MovementDemoRoomBounds");
                boundsObject.transform.SetParent(courseRoot.transform, false);
                BoxCollider2D roomBounds = boundsObject.AddComponent<BoxCollider2D>();
                roomBounds.size = new Vector2(100f, 40f);
                roomBounds.isTrigger = true;

                CreateCourseSprite(
                    "StoneBackdrop",
                    courseRoot.transform,
                    stone,
                    new Vector3(0f, 0f, 8f),
                    new Vector2(100f, 40f),
                    ColorFromHex("#2A2E40"),
                    -100,
                    -1,
                    false);
                CreateCourseSprite(
                    "TrainingFloor",
                    courseRoot.transform,
                    stone,
                    new Vector3(0f, FloorTop - 0.5f, 0f),
                    new Vector2(100f, 1f),
                    ColorFromHex("#A7794D"),
                    0,
                    groundLayer,
                    true);

                CreateCourseSprite("JumpBarrierOne", courseRoot.transform, stone,
                    new Vector3(-20f, FloorTop + 1f, 0f), new Vector2(1.5f, 2f), ColorFromHex("#AA7DC2"), 2, groundLayer, true);
                CreateCourseSprite("JumpBarrierTwo", courseRoot.transform, stone,
                    new Vector3(0f, FloorTop + 0.9f, 0f), new Vector2(1.5f, 1.8f), ColorFromHex("#AA7DC2"), 2, groundLayer, true);
                CreateCourseSprite("JumpBarrierThree", courseRoot.transform, stone,
                    new Vector3(20f, FloorTop + 1f, 0f), new Vector2(1.5f, 2f), ColorFromHex("#AA7DC2"), 2, groundLayer, true);

                var spawn = new GameObject("MovementDemoSpawn");
                spawn.transform.SetParent(courseRoot.transform, false);
                spawn.transform.position = new Vector3(-35f, FloorTop, 0f);

                CreateCourseSprite("MovementDemoPortal", courseRoot.transform, stone,
                    new Vector3(40f, FloorTop + 1.5f, 0f), new Vector2(1f, 3f), ColorFromHex("#F0C15A"), 5, -1, false);
                GameObject portalTriggerObject = new GameObject("MovementDemoGoal");
                portalTriggerObject.transform.SetParent(courseRoot.transform, false);
                portalTriggerObject.transform.position = new Vector3(40f, FloorTop + 1.5f, 0f);
                var goalCollider = portalTriggerObject.AddComponent<BoxCollider2D>();
                goalCollider.size = new Vector2(3f, 4f);
                goalCollider.isTrigger = true;

                GameObject globalLightObject = new GameObject("MovementDemoGlobalLight");
                globalLightObject.transform.SetParent(courseRoot.transform, false);
                Light2D globalLight = globalLightObject.AddComponent<Light2D>();
                globalLight.lightType = Light2D.LightType.Global;
                globalLight.intensity = 1f;
                globalLight.color = Color.white;

                GameObject compositionObject = new GameObject("MovementDemoComposition");
                SceneManager.MoveGameObjectToScene(compositionObject, scene);
                MovementDemoComposition composition = compositionObject.AddComponent<MovementDemoComposition>();
                MovementDemoGoalTrigger goal = portalTriggerObject.AddComponent<MovementDemoGoalTrigger>();
                goal.Composition = composition;

                Canvas hudCanvas = CreateHudCanvas(scene);
                Text telemetry = CreateText("MovementDemoTelemetry", hudCanvas.transform,
                    new Vector2(16f, -16f), new Vector2(260f, 56f), 14, "#E8E3CD", TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), new Vector2(0f, 1f));
                Text courseText = CreateText("MovementDemoCourseStatus", hudCanvas.transform,
                    new Vector2(16f, 16f), new Vector2(260f, 52f), 14, "#F0C15A", TextAnchor.LowerLeft,
                    Vector2.zero, Vector2.zero);
                Button returnButton = CreateButton("ReturnToMenuButton", "VOLVER AL MENÚ", hudCanvas.transform,
                    new Vector2(-16f, 16f), new Vector2(148f, 32f), 14, "#E8E3CD", "#32251F", false, true);

                SerializedObject compositionObjectData = new SerializedObject(composition);
                compositionObjectData.FindProperty("_playerPrefab").objectReferenceValue = playerPrefab;
                compositionObjectData.FindProperty("_spawnPoint").objectReferenceValue = spawn.transform;
                compositionObjectData.FindProperty("_roomBounds").objectReferenceValue = roomBounds;
                GameObject mainCamera = FindObject(scene, "Main Camera");
                RoomCameraRig2D cameraRig = mainCamera == null ? null : mainCamera.GetComponent<RoomCameraRig2D>();
                if (cameraRig == null)
                {
                    throw new InvalidOperationException("Expedition Main Camera must have its B06 RoomCameraRig2D before B07 generation.");
                }
                compositionObjectData.FindProperty("_cameraRig").objectReferenceValue = cameraRig;
                compositionObjectData.FindProperty("_telemetryText").objectReferenceValue = telemetry;
                compositionObjectData.FindProperty("_courseText").objectReferenceValue = courseText;
                compositionObjectData.FindProperty("_returnToMenuButton").objectReferenceValue = returnButton;
                compositionObjectData.ApplyModifiedPropertiesWithoutUndo();
                ProjectScaffoldGenerator.ReconcilePrototypeHudLayout(scene, "MovementDemoHUD",
                    "MovementDemoTelemetry", "MovementDemoCourseStatus", "ReturnToMenuButton");

                EventSystem eventSystem = FindEventSystem(scene);
                if (eventSystem != null) eventSystem.firstSelectedGameObject = returnButton.gameObject;

                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, scenePath))
                {
                    throw new IOException("Unity failed to save the B07 movement-demo scene.");
                }
                changed = true;
            }
            finally
            {
                RestoreScene(scene, previousActiveScene);
            }
        }

        private static void ValidateExistingCourse(Scene scene, GameObject playerPrefab)
        {
            if (FindComponent<MovementDemoComposition>(scene, "MovementDemoComposition") == null ||
                FindComponent<MovementDemoGoalTrigger>(scene, "MovementDemoGoal") == null ||
                FindComponent<BoxCollider2D>(scene, "MovementDemoRoomBounds") == null ||
                FindObject(scene, "TrainingFloor") == null ||
                FindObject(scene, "JumpBarrierOne") == null ||
                FindObject(scene, "JumpBarrierTwo") == null ||
                FindObject(scene, "JumpBarrierThree") == null ||
                FindObject(scene, "MovementDemoSpawn") == null ||
                FindComponent<CinemachineCamera>(scene, "Cinemachine Camera") == null ||
                playerPrefab == null)
            {
                throw new InvalidOperationException("The existing B07 movement course is incomplete; it was left untouched for repair.");
            }
        }

        private static bool ReconcileExistingCourseVisuals(Scene scene)
        {
            bool changed = false;
            changed |= SetSpriteColor(scene, "StoneBackdrop", "#2A2E40");
            changed |= SetSpriteColor(scene, "TrainingFloor", "#A7794D");
            changed |= SetSpriteColor(scene, "JumpBarrierOne", "#AA7DC2");
            changed |= SetSpriteColor(scene, "JumpBarrierTwo", "#AA7DC2");
            changed |= SetSpriteColor(scene, "JumpBarrierThree", "#AA7DC2");
            changed |= SetSpriteColor(scene, "MovementDemoPortal", "#F0C15A");
            changed |= ProjectScaffoldGenerator.ReconcilePrototypeHudLayout(scene, "MovementDemoHUD",
                "MovementDemoTelemetry", "MovementDemoCourseStatus", "ReturnToMenuButton");
            return changed;
        }

        private static bool SetSpriteColor(Scene scene, string objectName, string colorHex)
        {
            SpriteRenderer renderer = FindComponent<SpriteRenderer>(scene, objectName);
            if (renderer == null) return false;
            Color color = ColorFromHex(colorHex);
            if (renderer.color == color) return false;
            renderer.color = color;
            EditorUtility.SetDirty(renderer);
            return true;
        }

        private static bool EnsureEventSystem(Scene scene)
        {
            EventSystem[] eventSystems = Resources.FindObjectsOfTypeAll<EventSystem>()
                .Where(eventSystem => eventSystem.gameObject.scene == scene).ToArray();
            if (eventSystems.Length > 1)
            {
                throw new InvalidOperationException(scene.name + " contains more than one EventSystem.");
            }

            if (eventSystems.Length == 1)
            {
                if (eventSystems[0].GetComponent<InputSystemUIInputModule>() != null) return false;
                eventSystems[0].gameObject.AddComponent<InputSystemUIInputModule>();
                EditorUtility.SetDirty(eventSystems[0].gameObject);
                return true;
            }

            var eventSystemObject = new GameObject("LuckboundEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            return true;
        }

        private static EventSystem FindEventSystem(Scene scene)
        {
            return Resources.FindObjectsOfTypeAll<EventSystem>()
                .FirstOrDefault(eventSystem => eventSystem.gameObject.scene == scene);
        }

        private static Canvas CreateHudCanvas(Scene scene)
        {
            var canvasObject = new GameObject(
                "MovementDemoHUD",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Button CreateButton(
            string name,
            string label,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            string textColor,
            string backgroundColor,
            bool topRight = false,
            bool bottomRight = false)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            Vector2 anchor = topRight ? Vector2.one : bottomRight ? new Vector2(1f, 0f) : new Vector2(0.5f, 0.5f);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.GetComponent<Image>();
            image.color = ColorFromHex(backgroundColor);
            image.raycastTarget = true;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            ColorBlock colors = button.colors;
            colors.normalColor = ColorFromHex(backgroundColor);
            colors.highlightedColor = ColorFromHex("#493363");
            colors.pressedColor = ColorFromHex("#9A6026");
            colors.selectedColor = ColorFromHex("#493363");
            colors.disabledColor = ColorFromHex("#3F465C");
            button.colors = colors;

            var textObject = new GameObject(name + "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = ColorFromHex(textColor);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return button;
        }

        private static Text CreateText(
            string name,
            Transform parent,
            Vector2 anchoredPosition,
            Vector2 size,
            int fontSize,
            string color,
            TextAnchor alignment,
            Vector2 anchor,
            Vector2 pivot)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = ColorFromHex(color);
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static GameObject CreateCourseSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Vector3 position,
            Vector2 size,
            Color color,
            int sortingOrder,
            int physicsLayer,
            bool hasCollider)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            if (physicsLayer >= 0) gameObject.layer = physicsLayer;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = size;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            if (hasCollider)
            {
                BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
                collider.size = size;
            }
            return gameObject;
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            if (split <= 0) throw new InvalidOperationException("Invalid Unity asset folder: " + path);
            string parent = path.Substring(0, split);
            string name = path.Substring(split + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static GameObject FindObject(Scene scene, string objectName)
        {
            return scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Select(transform => transform.gameObject)
                .FirstOrDefault(gameObject => gameObject.name == objectName);
        }

        private static T FindComponent<T>(Scene scene, string objectName) where T : Component
        {
            GameObject gameObject = FindObject(scene, objectName);
            return gameObject == null ? null : gameObject.GetComponent<T>();
        }

        private static bool SetObjectReference(SerializedObject target, string propertyName, UnityEngine.Object value)
        {
            SerializedProperty property = target.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("Missing serialized field " + propertyName + ".");
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            target.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serializedObject = new SerializedObject(target);
            bool changed = SetObjectReference(serializedObject, propertyName, value);
            if (changed) EditorUtility.SetDirty(target);
            return changed;
        }

        private static void RestoreScene(Scene scene, Scene previousActiveScene)
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
            if (scene.IsValid() && scene.isLoaded)
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static Color32 Color32FromHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                throw new ArgumentException("Invalid prototype palette color " + hex + ".", nameof(hex));
            }

            return color;
        }

        private static Color ColorFromHex(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                throw new ArgumentException("Invalid prototype palette color " + hex + ".", nameof(hex));
            }

            return color;
        }

        private static void FillRect(Color32[] pixels, int width, int xMin, int yMin, int xMax, int yMax, Color32 color)
        {
            for (int y = yMin; y < yMax; y++)
            {
                for (int x = xMin; x < xMax; x++) SetPixel(pixels, width, x, y, color);
            }
        }

        private static void SetPixel(Color32[] pixels, int width, int x, int y, Color32 color)
        {
            if (x < 0 || x >= width || y < 0 || y * width + x >= pixels.Length) return;
            pixels[y * width + x] = color;
        }
    }
}
