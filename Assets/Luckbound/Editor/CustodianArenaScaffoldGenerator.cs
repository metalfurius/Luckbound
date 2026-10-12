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
    /// <summary>Idempotently generates D06's provisional boss/wave prefabs, arena, and menu entry.</summary>
    public static class CustodianArenaScaffoldGenerator
    {
        public const string ArenaScenePath = "Assets/Luckbound/Scenes/CustodianArena.unity";
        public const string BossPrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab";
        public const string WavePrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab";
        private const string PlayerPrefabPath = "Assets/Luckbound/Prefabs/MovementDemoPlayer.prefab";
        private const string StoneSpritePath = "Assets/Luckbound/Art/Prototypes/MovementDemo/StonePrototype.png";
        private static readonly Color BackgroundColor = ParseColor("#10111B");
        private static readonly Color Amber = ParseColor("#F0C15A");
        private const float FloorTop = -3.5f;

        public static void EnsureGenerated()
        {
            EnsureAssetFolder("Assets/Luckbound/Prefabs/CustodianArena");
            Sprite stone = AssetDatabase.LoadAssetAtPath<Sprite>(StoneSpritePath);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (stone == null || playerPrefab == null)
                throw new InvalidOperationException("D06 generation requires the B07 StonePrototype sprite and player prefab.");

            GameObject bossPrefab = EnsureBossPrefab(stone);
            GameObject wavePrefab = EnsureWavePrefab(stone);
            EnsureArenaScene(playerPrefab, bossPrefab, wavePrefab, stone);
            EnsureMenuEntry();
            AssetDatabase.SaveAssets();
        }

        private static GameObject EnsureBossPrefab(Sprite stone)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
            bool valid = prefab != null && prefab.layer == LayerMask.NameToLayer("Enemies") &&
                prefab.GetComponent<Rigidbody2D>() != null && prefab.GetComponent<CapsuleCollider2D>() != null &&
                prefab.GetComponent<SpriteRenderer>()?.sprite == stone && prefab.GetComponent<CustodianBoss2D>() != null;
            if (valid) return prefab;
            if (prefab != null) Debug.LogWarning("Replacing incomplete generated D06 Custodian prefab: " + BossPrefabPath);

            var boss = new GameObject("CustodianPrototype");
            try
            {
                boss.layer = LayerMask.NameToLayer("Enemies");
                Rigidbody2D body = boss.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Dynamic;
                body.sleepMode = RigidbodySleepMode2D.NeverSleep;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                body.constraints = RigidbodyConstraints2D.FreezeRotation;
                CapsuleCollider2D collider = boss.AddComponent<CapsuleCollider2D>();
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(1.15f, 1.8f);
                SpriteRenderer renderer = boss.AddComponent<SpriteRenderer>();
                renderer.sprite = stone;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(1.45f, 2.3f);
                renderer.sortingOrder = 9;
                renderer.color = ParseColor("#6B8A54");
                boss.AddComponent<CustodianBoss2D>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(boss, BossPrefabPath);
                if (saved == null) throw new IOException("Unity failed to create the D06 boss prefab.");
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(boss); }
        }

        private static GameObject EnsureWavePrefab(Sprite stone)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WavePrefabPath);
            bool valid = prefab != null && prefab.layer == LayerMask.NameToLayer("Enemies") &&
                prefab.GetComponent<Rigidbody2D>() != null && prefab.GetComponent<CircleCollider2D>() != null &&
                prefab.GetComponent<SpriteRenderer>()?.sprite == stone && prefab.GetComponent<CustodianWaveHazard2D>() != null;
            if (valid) return prefab;
            if (prefab != null) Debug.LogWarning("Replacing incomplete generated D06 wave prefab: " + WavePrefabPath);

            var wave = new GameObject("CustodianWavePrototype");
            try
            {
                wave.layer = LayerMask.NameToLayer("Enemies");
                Rigidbody2D body = wave.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Kinematic;
                body.gravityScale = 0f;
                body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                CircleCollider2D collider = wave.AddComponent<CircleCollider2D>();
                collider.isTrigger = true;
                collider.radius = 0.33f;
                SpriteRenderer renderer = wave.AddComponent<SpriteRenderer>();
                renderer.sprite = stone;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = new Vector2(0.7f, 0.45f);
                renderer.sortingOrder = 10;
                renderer.color = Amber;
                wave.AddComponent<CustodianWaveHazard2D>();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(wave, WavePrefabPath);
                if (saved == null) throw new IOException("Unity failed to create the D06 wave prefab.");
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(wave); }
        }

        private static void EnsureArenaScene(GameObject playerPrefab, GameObject bossPrefab, GameObject wavePrefab, Sprite stone)
        {
            if (!File.Exists(ArenaScenePath)) throw new FileNotFoundException("CustodianArena must be registered before D06 generation.", ArenaScenePath);
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(ArenaScenePath);
            bool openedByGenerator = !scene.IsValid() || !scene.isLoaded;
            if (openedByGenerator) scene = EditorSceneManager.OpenScene(ArenaScenePath, OpenSceneMode.Additive);
            try
            {
                if (FindObject(scene, "CustodianArenaComposition") != null)
                {
                    ValidateArena(scene);
                    ReconcileArenaHud(scene);
                    return;
                }

                int groundLayer = LayerMask.NameToLayer("Ground");
                int enemyLayer = LayerMask.NameToLayer("Enemies");
                if (groundLayer < 0 || enemyLayer < 0) throw new InvalidOperationException("D06 requires Ground and Enemies layers.");
                EnsureEventSystem(scene);

                GameObject cameraObject = FindObject(scene, "Main Camera");
                if (cameraObject == null) throw new InvalidOperationException("CustodianArena must have a camera before D06 generation.");
                ConfigureArenaCamera(cameraObject);
                RoomCameraRig2D cameraRig = cameraObject.GetComponent<RoomCameraRig2D>();

                GameObject world = new GameObject("CustodianArenaWorld");
                SceneManager.MoveGameObjectToScene(world, scene);
                BoxCollider2D bounds = new GameObject("CustodianArenaBounds").AddComponent<BoxCollider2D>();
                bounds.gameObject.transform.SetParent(world.transform, false);
                bounds.size = new Vector2(30f, 12f);
                bounds.offset = new Vector2(0f, 1f);
                bounds.isTrigger = true;

                CreateArenaSprite("CustodianArenaBackdrop", world.transform, stone, new Vector3(0f, 1f, 9f),
                    new Vector2(30f, 12f), ParseColor("#252436"), -100, -1, false);
                CreateArenaSprite("CustodianArenaFloor", world.transform, stone, new Vector3(0f, FloorTop - 0.5f, 0f),
                    new Vector2(30f, 1f), ParseColor("#59647A"), 0, groundLayer, true);
                CreateArenaSprite("CustodianArenaLeftWall", world.transform, stone, new Vector3(-15.25f, FloorTop + 1.75f, 0f),
                    new Vector2(0.5f, 3.5f), ParseColor("#3F465C"), 2, groundLayer, true);
                CreateArenaSprite("CustodianArenaRightWall", world.transform, stone, new Vector3(15.25f, FloorTop + 1.75f, 0f),
                    new Vector2(0.5f, 3.5f), ParseColor("#3F465C"), 2, groundLayer, true);

                GameObject playerSpawn = new GameObject("CustodianArenaPlayerSpawn");
                playerSpawn.transform.SetParent(world.transform, false);
                playerSpawn.transform.position = new Vector3(-8.5f, FloorTop, 0f);
                GameObject waveRoot = new GameObject("CustodianWaveRoot");
                waveRoot.transform.SetParent(world.transform, false);

                GameObject lightObject = new GameObject("CustodianArenaGlobalLight");
                lightObject.transform.SetParent(world.transform, false);
                Light2D light = lightObject.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Global;
                light.intensity = 1f;
                light.color = Color.white;

                Canvas canvas = CreateCanvas(scene, "CustodianArenaHUD");
                Text telemetry = CreateText("CustodianArenaTelemetry", canvas.transform,
                    new Vector2(12f, -12f), new Vector2(320f, 48f), 11, ParseColor("#E8E3CD"), TextAnchor.UpperLeft, top: true, left: true);
                Text bossStatus = CreateText("CustodianBossStatus", canvas.transform,
                    new Vector2(0f, -15f), new Vector2(460f, 40f), 10, Amber, TextAnchor.MiddleCenter, top: true);
                Text pause = CreateText("CustodianArenaPause", canvas.transform,
                    Vector2.zero, new Vector2(400f, 76f), 18, ParseColor("#E8E3CD"), TextAnchor.MiddleCenter);
                Button returnButton = CreateButton("CustodianArenaReturnButton", "VOLVER AL MENÚ",
                    canvas.transform, new Vector2(-12f, -12f), new Vector2(126f, 28f));

                GameObject compositionObject = new GameObject("CustodianArenaComposition");
                SceneManager.MoveGameObjectToScene(compositionObject, scene);
                CustodianArenaComposition composition = compositionObject.AddComponent<CustodianArenaComposition>();
                var data = new SerializedObject(composition);
                data.FindProperty("_playerPrefab").objectReferenceValue = playerPrefab;
                data.FindProperty("_custodianPrefab").objectReferenceValue = bossPrefab;
                data.FindProperty("_wavePrefab").objectReferenceValue = wavePrefab;
                data.FindProperty("_playerSpawn").objectReferenceValue = playerSpawn.transform;
                data.FindProperty("_waveRoot").objectReferenceValue = waveRoot.transform;
                data.FindProperty("_roomBounds").objectReferenceValue = bounds;
                data.FindProperty("_cameraRig").objectReferenceValue = cameraRig;
                data.FindProperty("_telemetryText").objectReferenceValue = telemetry;
                data.FindProperty("_bossStatusText").objectReferenceValue = bossStatus;
                data.FindProperty("_pauseText").objectReferenceValue = pause;
                data.FindProperty("_returnToMenuButton").objectReferenceValue = returnButton;
                data.ApplyModifiedPropertiesWithoutUndo();

                EventSystem eventSystem = FindEventSystem(scene);
                if (eventSystem != null) eventSystem.firstSelectedGameObject = returnButton.gameObject;
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ArenaScenePath)) throw new IOException("Unity failed to save CustodianArena.unity.");
            }
            finally { RestoreScene(scene, previousActiveScene, openedByGenerator); }
        }

        private static void EnsureMenuEntry()
        {
            const string menuPath = "Assets/Luckbound/Scenes/Menu.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(menuPath);
            bool openedByGenerator = !scene.IsValid() || !scene.isLoaded;
            if (openedByGenerator) scene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Additive);
            try
            {
                EnsureEventSystem(scene);
                GameObject canvas = FindObject(scene, "MenuCanvas");
                if (canvas == null) throw new InvalidOperationException("MenuCanvas is missing; D06 will not replace the menu scene.");
                Button button = FindComponent<Button>(scene, "StartCustodianArenaButton");
                bool changed = false;
                if (button == null)
                {
                    button = CreateButton("StartCustodianArenaButton", "ENFRENTAR · CUSTODIO (D06)", canvas.transform,
                        new Vector2(0f, -110f), new Vector2(230f, 24f), 10, Amber, ParseColor("#32251F"), topRight: false);
                    changed = true;
                }
                else
                {
                    changed |= SetButtonRect(button, new Vector2(0f, -110f), new Vector2(230f, 24f));
                    Text label = button.GetComponentInChildren<Text>(true);
                    if (label != null && label.text != "ENFRENTAR · CUSTODIO (D06)")
                    {
                        label.text = "ENFRENTAR · CUSTODIO (D06)";
                        EditorUtility.SetDirty(label);
                        changed = true;
                    }
                }

                MenuNavigationController navigation = FindComponent<MenuNavigationController>(scene, "MenuNavigation");
                if (navigation == null) throw new InvalidOperationException("MenuNavigationController is missing from Menu.");
                changed |= SetObjectReference(navigation, "_startCustodianButton", button);

                GameObject bottomRule = FindObject(scene, "BottomRule");
                RectTransform rule = bottomRule == null ? null : bottomRule.GetComponent<RectTransform>();
                if (rule != null && rule.anchoredPosition != new Vector2(0f, -145f))
                {
                    rule.anchoredPosition = new Vector2(0f, -145f);
                    EditorUtility.SetDirty(rule);
                    changed = true;
                }
                changed |= SetTextLayout(scene, "MenuStatus", new Vector2(0.5f, 0.5f), new Vector2(0f, -145f),
                    new Vector2(300f, 10f), TextAnchor.MiddleCenter);
                changed |= SetTextLayout(scene, "MenuVersion", new Vector2(0.5f, 0.5f), new Vector2(0f, -156f),
                    new Vector2(300f, 8f), TextAnchor.MiddleCenter);

                EventSystem eventSystem = FindEventSystem(scene);
                GameObject preferredButton = FindObject(scene, "StartTavernButton") ?? button.gameObject;
                if (eventSystem != null && eventSystem.firstSelectedGameObject != preferredButton)
                {
                    eventSystem.firstSelectedGameObject = preferredButton;
                    EditorUtility.SetDirty(eventSystem);
                    changed = true;
                }
                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, menuPath)) throw new IOException("Unity failed to save the D06 menu entry.");
                }
            }
            finally { RestoreScene(scene, previousActiveScene, openedByGenerator); }
        }

        private static void ValidateArena(Scene scene)
        {
            CustodianArenaComposition composition = FindComponent<CustodianArenaComposition>(scene, "CustodianArenaComposition");
            if (composition == null || FindComponent<BoxCollider2D>(scene, "CustodianArenaBounds") == null ||
                FindObject(scene, "CustodianArenaFloor") == null || FindObject(scene, "CustodianArenaPlayerSpawn") == null ||
                FindObject(scene, "CustodianWaveRoot") == null || FindComponent<RoomCameraRig2D>(scene, "Main Camera") == null)
                throw new InvalidOperationException("The existing CustodianArena scaffold is incomplete; it was left untouched for repair.");
        }

        private static void ReconcileArenaHud(Scene scene)
        {
            bool changed = false;
            changed |= SetTextLayout(scene, "CustodianArenaTelemetry", new Vector2(0f, 1f), new Vector2(12f, -12f),
                new Vector2(320f, 48f), TextAnchor.UpperLeft);
            changed |= SetTextLayout(scene, "CustodianBossStatus", new Vector2(0.5f, 1f), new Vector2(0f, -15f),
                new Vector2(460f, 40f), TextAnchor.MiddleCenter);
            changed |= SetTextLayout(scene, "CustodianArenaPause", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(400f, 76f), TextAnchor.MiddleCenter);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene, ArenaScenePath)) throw new IOException("Unity failed to reconcile CustodianArena HUD layout.");
            }
        }

        private static void ConfigureArenaCamera(GameObject cameraObject)
        {
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null) camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = BackgroundColor;
            if (cameraObject.GetComponent<AudioListener>() == null) cameraObject.AddComponent<AudioListener>();
            if (cameraObject.GetComponent<PixelPerfectCamera>() == null) cameraObject.AddComponent<PixelPerfectCamera>();
            if (cameraObject.GetComponent<CinemachineBrain>() == null) cameraObject.AddComponent<CinemachineBrain>();
            RoomCameraRig2D rig = cameraObject.GetComponent<RoomCameraRig2D>();
            if (rig == null) rig = cameraObject.AddComponent<RoomCameraRig2D>();
            rig.Configure();
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
            Color color, TextAnchor anchor, bool top = false, bool left = false)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = top ? (left ? new Vector2(0f, 1f) : new Vector2(0.5f, 1f)) : new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Button CreateButton(string name, string label, Transform parent, Vector2 position, Vector2 size,
            int fontSize = 10, Color textColor = default, Color backgroundColor = default, bool topRight = true)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = topRight ? new Vector2(1f, 1f) : new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
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
            Text labelText = labelObject.GetComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (labelText.font == null) labelText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = fontSize;
            labelText.fontStyle = FontStyle.Bold;
            labelText.color = textColor == default ? ParseColor("#E8E3CD") : textColor;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.raycastTarget = false;
            return button;
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

        private static GameObject FindObject(Scene scene, string objectName) => scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(gameObject => gameObject.name == objectName);

        private static T FindComponent<T>(Scene scene, string objectName) where T : Component
        {
            GameObject gameObject = FindObject(scene, objectName);
            return gameObject == null ? null : gameObject.GetComponent<T>();
        }

        private static bool SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
            return true;
        }

        private static bool SetButtonRect(Button button, Vector2 position, Vector2 size)
        {
            RectTransform rect = button.GetComponent<RectTransform>();
            bool changed = false;
            if (rect.anchoredPosition != position) { rect.anchoredPosition = position; changed = true; }
            if (rect.sizeDelta != size) { rect.sizeDelta = size; changed = true; }
            if (changed) EditorUtility.SetDirty(rect);
            return changed;
        }

        private static bool SetTextLayout(Scene scene, string name, Vector2 anchor, Vector2 position, Vector2 size, TextAnchor textAnchor)
        {
            Text text = FindComponent<Text>(scene, name);
            if (text == null) return false;
            RectTransform rect = text.GetComponent<RectTransform>();
            bool changed = false;
            if (rect.anchorMin != anchor || rect.anchorMax != anchor || rect.anchoredPosition != position || rect.sizeDelta != size)
            {
                rect.anchorMin = anchor;
                rect.anchorMax = anchor;
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
                EditorUtility.SetDirty(rect);
                changed = true;
            }
            if (text.alignment != textAnchor) { text.alignment = textAnchor; EditorUtility.SetDirty(text); changed = true; }
            return changed;
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
