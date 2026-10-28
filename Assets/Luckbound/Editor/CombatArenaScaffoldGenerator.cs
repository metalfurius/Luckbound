using System;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Gameplay.Enemies;
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
    /// <summary>Idempotently generates C08 provisional combat prefabs, menu entry and arena scene.</summary>
    public static class CombatArenaScaffoldGenerator
    {
        public const string ArenaScenePath = "Assets/Luckbound/Scenes/CombatArena.unity";
        public const string SlimePrefabPath = "Assets/Luckbound/Prefabs/CombatArena/SlimePrototype.prefab";
        public const string HuesoguardiaPrefabPath = "Assets/Luckbound/Prefabs/CombatArena/HuesoguardiaPrototype.prefab";
        private const string PlayerPrefabPath = "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab";
        private const string StoneSpritePath = "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png";
        private const float FloorTop = -3.5f;

        public static void EnsureGenerated()
        {
            EnsureAssetFolder("Assets/Luckbound/Prefabs/CombatArena");
            Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>(StoneSpritePath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (stone == null || playerPrefab == null)
            {
                throw new InvalidOperationException("C08 generation requires the B07 StonePrototype sprite and player prefab.");
            }

            GameObject slimePrefab = EnsureEnemyPrefab(SlimePrefabPath, stone, slime: true);
            GameObject huesoguardiaPrefab = EnsureEnemyPrefab(HuesoguardiaPrefabPath, stone, slime: false);
            EnsureArenaScene(playerPrefab, slimePrefab, huesoguardiaPrefab, stone);
            EnsureArenaMenuEntry();
            AssetDatabase.SaveAssets();
        }

        private static GameObject EnsureEnemyPrefab(string path, Sprite sprite, bool slime)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool valid = prefab != null && prefab.GetComponent<Rigidbody2D>() != null &&
                prefab.GetComponent<Collider2D>() != null && prefab.GetComponent<SpriteRenderer>()?.sprite == sprite &&
                (slime ? prefab.GetComponent<Slime2D>() != null : prefab.GetComponent<Huesoguardia2D>() != null) &&
                prefab.layer == LayerMask.NameToLayer("Enemies");
            Color prototypeColor = ParseColor(slime ? "#94BC74" : "#BDC6CC");
            if (valid)
            {
                ReconcilePrefabSpriteColor(path, prototypeColor);
                return AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }

            if (prefab != null) Debug.LogWarning("Replacing incomplete generated C08 enemy prefab: " + path);
            var enemy = new GameObject(slime ? "SlimePrototype" : "HuesoguardiaPrototype");
            try
            {
                enemy.layer = LayerMask.NameToLayer("Enemies");
                Rigidbody2D body = enemy.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;

                CapsuleCollider2D collider = enemy.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = slime ? new Vector2(0.9f, 0.8f) : new Vector2(0.9f, 0.9f);
                SpriteRenderer renderer = enemy.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sortingOrder = 8;
                renderer.color = prototypeColor;
                enemy.transform.localScale = slime ? new Vector3(0.8f, 0.8f, 1f) : new Vector3(1f, 1.25f, 1f);
                if (slime) enemy.AddComponent<Slime2D>();
                else enemy.AddComponent<Huesoguardia2D>();

                GameObject saved = PrefabUtility.SaveAsPrefabAsset(enemy, path);
                if (saved == null) throw new IOException("Unity failed to create the C08 enemy prefab at " + path + ".");
                return saved;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemy);
            }
        }

        private static void ReconcilePrefabSpriteColor(string path, Color color)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
                if (renderer == null || renderer.color == color) return;
                renderer.color = color;
                EditorUtility.SetDirty(renderer);
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("Unity failed to reconcile prototype tint for " + path + ".");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureArenaScene(
            GameObject playerPrefab,
            GameObject slimePrefab,
            GameObject huesoguardiaPrefab,
            Sprite stone)
        {
            if (!File.Exists(ArenaScenePath)) throw new FileNotFoundException("CombatArena scene must be registered before C08 generation.", ArenaScenePath);
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ArenaScenePath);
            bool openedByGenerator = !scene.IsValid() || !scene.isLoaded;
            if (openedByGenerator) scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Additive);
            try
            {
                if (FindObject(scene, "CombatArenaComposition") != null)
                {
                    ValidateArena(scene);
                    ReconcileArenaHud(scene);
                    return;
                }

                int groundLayer = LayerMask.NameToLayer("Ground");
                int enemyLayer = LayerMask.NameToLayer("Enemies");
                if (groundLayer < 0 || enemyLayer < 0) throw new InvalidOperationException("C08 requires Ground and Enemies layers.");
                EnsureEventSystem(scene);

                GameObject cameraObject = FindObject(scene, "Main Camera");
                if (cameraObject == null) throw new InvalidOperationException("CombatArena must have a camera before C08 generation.");
                ConfigureArenaCamera(cameraObject);
                RoomCameraRig2D cameraRig = cameraObject.GetComponent<RoomCameraRig2D>();

                GameObject world = new GameObject("CombatArenaWorld");
                SceneManager.MoveGameObjectToScene(world, scene);
                BoxCollider2D bounds = new GameObject("CombatArenaBounds").AddComponent<BoxCollider2D>();
                bounds.gameObject.transform.SetParent(world.transform, false);
                bounds.size = new Vector2(32f, 12f);
                bounds.offset = new Vector2(0f, 1f);
                bounds.isTrigger = true;

                CreateArenaSprite("ArenaBackdrop", world.transform, stone, new Vector3(0f, 1f, 9f),
                    new Vector2(32f, 12f), ParseColor("#8290A5"), -100, -1, false);
                CreateArenaSprite("ArenaFloor", world.transform, stone, new Vector3(0f, FloorTop - 0.5f, 0f),
                    new Vector2(32f, 1f), ParseColor("#A7794D"), 0, groundLayer, true);
                CreateArenaSprite("ArenaLeftWall", world.transform, stone, new Vector3(-16.25f, FloorTop + 1.75f, 0f),
                    new Vector2(0.5f, 3.5f), ParseColor("#59647A"), 2, groundLayer, true);
                CreateArenaSprite("ArenaRightWall", world.transform, stone, new Vector3(16.25f, FloorTop + 1.75f, 0f),
                    new Vector2(0.5f, 3.5f), ParseColor("#59647A"), 2, groundLayer, true);

                GameObject spawnObject = new GameObject("CombatArenaPlayerSpawn");
                spawnObject.transform.SetParent(world.transform, false);
                spawnObject.transform.position = new Vector3(-11.5f, FloorTop, 0f);

                GameObject lightObject = new GameObject("CombatArenaGlobalLight");
                lightObject.transform.SetParent(world.transform, false);
                Light2D light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.intensity = 1f;
                light.color = Color.white;

                Canvas canvas = CreateCanvas(scene, "CombatArenaHUD");
                Text telemetry = CreateText("CombatArenaTelemetry", canvas.transform,
                    new Vector2(16f, -16f), new Vector2(260f, 56f), 14, ParseColor("#E8E3CD"), TextAnchor.UpperLeft,
                    new Vector2(0f, 1f), new Vector2(0f, 1f));
                Text encounter = CreateText("CombatArenaStatus", canvas.transform,
                    new Vector2(16f, 16f), new Vector2(260f, 52f), 14, ParseColor("#F0C15A"), TextAnchor.LowerLeft,
                    Vector2.zero, Vector2.zero);
                Text pause = CreateText("CombatArenaPause", canvas.transform,
                    Vector2.zero, new Vector2(400f, 76f), 18, ParseColor("#E8E3CD"), TextAnchor.MiddleCenter,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
                Button returnButton = CreateButton("CombatArenaReturnButton", "VOLVER AL MENÚ",
                    canvas.transform, new Vector2(-16f, 16f), new Vector2(148f, 32f), bottomRight: true);

                GameObject compositionObject = new GameObject("CombatArenaComposition");
                SceneManager.MoveGameObjectToScene(compositionObject, scene);
                CombatArenaComposition composition = compositionObject.AddComponent<CombatArenaComposition>();
                var data = new SerializedObject(composition);
                data.FindProperty("_playerPrefab").objectReferenceValue = playerPrefab;
                data.FindProperty("_slimePrefab").objectReferenceValue = slimePrefab;
                data.FindProperty("_huesoguardiaPrefab").objectReferenceValue = huesoguardiaPrefab;
                data.FindProperty("_playerSpawn").objectReferenceValue = spawnObject.transform;
                data.FindProperty("_roomBounds").objectReferenceValue = bounds;
                data.FindProperty("_cameraRig").objectReferenceValue = cameraRig;
                data.FindProperty("_telemetryText").objectReferenceValue = telemetry;
                data.FindProperty("_encounterText").objectReferenceValue = encounter;
                data.FindProperty("_pauseText").objectReferenceValue = pause;
                data.FindProperty("_returnToMenuButton").objectReferenceValue = returnButton;
                data.ApplyModifiedPropertiesWithoutUndo();
                ProjectScaffoldGenerator.ReconcilePrototypeHudLayout(scene, "CombatArenaHUD",
                    "CombatArenaTelemetry", "CombatArenaStatus", "CombatArenaReturnButton");

                EventSystem eventSystem = FindEventSystem(scene);
                if (eventSystem != null) eventSystem.firstSelectedGameObject = returnButton.gameObject;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ArenaScenePath)) throw new IOException("Unity failed to save CombatArena.unity.");
            }
            finally
            {
                RestoreScene(scene, previousActiveScene, openedByGenerator);
            }
        }

        private static void EnsureArenaMenuEntry()
        {
            const string menuPath = "Assets/Luckbound/Scenes/Menu.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(menuPath);
            bool openedByGenerator = !scene.IsValid() || !scene.isLoaded;
            if (openedByGenerator) scene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Additive);
            try
            {
                EnsureEventSystem(scene);
                GameObject canvasObject = FindObject(scene, "MenuCanvas");
                if (canvasObject == null) throw new InvalidOperationException("MenuCanvas is missing; C08 will not replace the menu scene.");
                Button button = FindComponent<Button>(scene, "StartCombatArenaButton");
                bool changed = false;
                if (button == null)
                {
                    button = CreateButton("StartCombatArenaButton", "LUCHAR · ARENA 0.2", canvasObject.transform,
                        new Vector2(0f, -18f), new Vector2(204f, 28f), 14, ParseColor("#F0C15A"), ParseColor("#32251F"), topRight: false);
                    changed = true;
                }
                else
                {
                    Text label = button.GetComponentInChildren<Text>(true);
                    if (label != null && label.text != "LUCHAR · ARENA 0.2")
                    {
                        label.text = "LUCHAR · ARENA 0.2";
                        EditorUtility.SetDirty(label);
                        changed = true;
                    }
                }

                GameObject navigationObject = FindObject(scene, "MenuNavigation");
                if (navigationObject == null) throw new InvalidOperationException("MenuNavigation is missing; C08 will not replace the menu scene.");
                MenuNavigationController navigation = navigationObject.GetComponent<MenuNavigationController>();
                if (navigation == null) throw new InvalidOperationException("MenuNavigationController is missing from Menu.");
                changed |= SetObjectReference(navigation, "_startArenaButton", button);

                EventSystem eventSystem = FindEventSystem(scene);
                GameObject tavernButton = FindObject(scene, "StartTavernButton");
                GameObject firstSelected = tavernButton != null ? tavernButton : button.gameObject;
                if (eventSystem != null && eventSystem.firstSelectedGameObject != firstSelected)
                {
                    eventSystem.firstSelectedGameObject = firstSelected;
                    EditorUtility.SetDirty(eventSystem);
                    changed = true;
                }

                changed |= ProjectScaffoldGenerator.ReconcileMenuVisualLayout(scene);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, menuPath)) throw new IOException("Unity failed to save the C08 menu entry.");
                }
            }
            finally
            {
                RestoreScene(scene, previousActiveScene, openedByGenerator);
            }
        }

        private static void ConfigureArenaCamera(GameObject cameraObject)
        {
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null) camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = ParseColor("#10111B");
            if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            if (cameraObject.GetComponent<PixelPerfectCamera>() == null) cameraObject.AddComponent<PixelPerfectCamera>();
            if (cameraObject.GetComponent<CinemachineBrain>() == null) cameraObject.AddComponent<CinemachineBrain>();
            RoomCameraRig2D rig = cameraObject.GetComponent<RoomCameraRig2D>();
            if (rig == null) rig = cameraObject.AddComponent<RoomCameraRig2D>();
            rig.Configure();
        }

        private static void ValidateArena(Scene scene)
        {
            CombatArenaComposition composition = FindComponent<CombatArenaComposition>(scene, "CombatArenaComposition");
            if (composition == null || FindComponent<BoxCollider2D>(scene, "CombatArenaBounds") == null ||
                FindObject(scene, "ArenaFloor") == null || FindObject(scene, "CombatArenaPlayerSpawn") == null ||
                FindComponent<RoomCameraRig2D>(scene, "Main Camera") == null)
            {
                throw new InvalidOperationException("The existing CombatArena scaffold is incomplete; it was left untouched for repair.");
            }
        }

        private static void ReconcileArenaHud(Scene scene)
        {
            bool changed = false;
            changed |= ProjectScaffoldGenerator.ReconcilePrototypeHudLayout(scene, "CombatArenaHUD",
                "CombatArenaTelemetry", "CombatArenaStatus", "CombatArenaReturnButton");
            changed |= SetTextLayout(scene, "CombatArenaPause", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(400f, 76f), 18, TextAnchor.MiddleCenter);
            changed |= SetSpriteColor(scene, "ArenaBackdrop", "#8290A5");
            changed |= SetSpriteColor(scene, "ArenaFloor", "#A7794D");
            changed |= SetSpriteColor(scene, "ArenaLeftWall", "#59647A");
            changed |= SetSpriteColor(scene, "ArenaRightWall", "#59647A");
            changed |= SetCameraBackground(scene, "#10111B");
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ArenaScenePath)) throw new IOException("Unity failed to reconcile CombatArena HUD layout.");
            }
        }

        private static bool SetTextLayout(Scene scene, string objectName, Vector2 anchor, Vector2 pivot,
            Vector2 position, Vector2 size, int fontSize, TextAnchor textAnchor)
        {
            Text text = FindComponent<Text>(scene, objectName);
            if (text == null) return false;
            bool changed = false;
            RectTransform rect = text.GetComponent<RectTransform>();
            if (rect.anchorMin != anchor || rect.anchorMax != anchor || rect.pivot != pivot ||
                rect.anchoredPosition != position || rect.sizeDelta != size)
            {
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.pivot = pivot;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                EditorUtility.SetDirty(rect);
                changed = true;
            }
            if (text.fontSize != fontSize || text.alignment != textAnchor || text.resizeTextForBestFit ||
                text.horizontalOverflow != HorizontalWrapMode.Wrap || text.verticalOverflow != VerticalWrapMode.Truncate)
            {
                text.fontSize = fontSize;
                text.alignment = textAnchor;
                text.resizeTextForBestFit = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                EditorUtility.SetDirty(text);
                changed = true;
            }
            return changed;
        }

        private static bool SetSpriteColor(Scene scene, string objectName, string colorHex)
        {
            SpriteRenderer renderer = FindComponent<SpriteRenderer>(scene, objectName);
            if (renderer == null) return false;
            Color color = ParseColor(colorHex);
            if (renderer.color == color) return false;
            renderer.color = color;
            EditorUtility.SetDirty(renderer);
            return true;
        }

        private static bool SetCameraBackground(Scene scene, string colorHex)
        {
            Camera camera = FindComponent<Camera>(scene, "Main Camera");
            if (camera == null) return false;
            Color color = ParseColor(colorHex);
            if (camera.backgroundColor == color) return false;
            camera.backgroundColor = color;
            EditorUtility.SetDirty(camera);
            return true;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            EventSystem[] systems = Resources.FindObjectsOfTypeAll<EventSystem>().Where(item => item.gameObject.scene == scene).ToArray();
            if (systems.Length > 1) throw new InvalidOperationException(scene.name + " contains more than one EventSystem.");
            if (systems.Length == 1)
            {
                if (systems[0].GetComponent<InputSystemUIInputModule>() == null) systems[0].gameObject.AddComponent<InputSystemUIInputModule>();
                return;
            }

            var system = new GameObject("LuckboundEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(system, scene);
        }

        private static EventSystem FindEventSystem(Scene scene) =>
            Resources.FindObjectsOfTypeAll<EventSystem>().FirstOrDefault(item => item.gameObject.scene == scene);

        private static Canvas CreateCanvas(Scene scene, string name)
        {
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static Text CreateText(string name, Transform parent, Vector2 position, Vector2 size, int fontSize,
            Color color, TextAnchor alignment, Vector2 anchor, Vector2 pivot)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, string label, Transform parent, Vector2 position, Vector2 size,
            int fontSize = 14, Color textColor = default, Color backgroundColor = default,
            bool topRight = true, bool bottomRight = false)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            Vector2 buttonAnchor = bottomRight ? new Vector2(1f, 0f) : topRight ? new Vector2(1f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchorMin = buttonAnchor;
            rect.anchorMax = rect.anchorMin;
            rect.pivot = buttonAnchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = backgroundColor == default ? ParseColor("#32251F") : backgroundColor;
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            Text text = labelObject.GetComponent<Text>();
            text.text = label;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.color = textColor == default ? ParseColor("#E8E3CD") : textColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return button;
        }

        private static GameObject CreateArenaSprite(string name, Transform parent, Sprite sprite, Vector3 position,
            Vector2 size, Color color, int sortingOrder, int physicsLayer, bool withCollider)
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
            if (withCollider)
            {
                BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
                collider.size = size;
            }
            return gameObject;
        }

        private static bool SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var data = new SerializedObject(target);
            SerializedProperty property = data.FindProperty(propertyName);
            if (property == null) throw new InvalidOperationException("Missing serialized field " + propertyName + ".");
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            data.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        private static GameObject FindObject(Scene scene, string objectName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == objectName);

        private static T FindComponent<T>(Scene scene, string objectName) where T : Component
        {
            GameObject gameObject = FindObject(scene, objectName);
            return gameObject == null ? null : gameObject.GetComponent<T>();
        }

        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/');
            string parent = path.Substring(0, split);
            string name = path.Substring(split + 1);
            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static void RestoreScene(Scene scene, Scene previousActiveScene, bool closeScene)
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded) SceneManager.SetActiveScene(previousActiveScene);
            if (closeScene && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }

        private static Color ParseColor(string html)
        {
            if (!ColorUtility.TryParseHtmlString(html, out Color color)) throw new ArgumentException("Invalid color " + html + ".");
            return color;
        }
    }
}
