using System;
using System.IO;
using System.Linq;
using Luckbound.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.Editor
{
    /// <summary>Creates the data-driven class and skill selection screen in the Hub scene.</summary>
    public static class HubPreparationScaffoldGenerator
    {
        private const string ScenePath = "Assets/Luckbound/Scenes/Hub.unity";

        public static void EnsureGenerated()
        {
            EnsureHubScene();
            EnsureMenuEntry();
        }

        private static void EnsureHubScene()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                bool changed = EnsureEventSystem(scene);
                GameObject canvasObject = FindObject(scene, "HubPreparationCanvas");
                if (canvasObject == null)
                {
                    canvasObject = CreateCanvas("HubPreparationCanvas", scene, 0).gameObject;
                    Image background = CreateImage("HubBackground", canvasObject.transform, "#10111B");
                    Stretch(background.rectTransform);
                    Image goldRule = CreateImage("HubTopRule", canvasObject.transform, "#D68F32");
                    Place(goldRule.rectTransform, new Vector2(156f, 2f), new Vector2(0f, 130f));
                    CreateText("HubTitle", "LA TABERNA", canvasObject.transform, new Vector2(0f, 111f),
                        new Vector2(330f, 28f), 21, "#E8E3CD", FontStyle.Bold);
                    CreateText("HubSubtitle", "Elige tu senda. El castillo ya está despierto.", canvasObject.transform,
                        new Vector2(0f, 86f), new Vector2(390f, 20f), 10, "#AA7DC2", FontStyle.Normal);
                    CreateText("ClassSectionTitle", "CLASE", canvasObject.transform, new Vector2(0f, 67f),
                        new Vector2(90f, 15f), 8, "#F0C15A", FontStyle.Bold);
                    Button[] classButtons =
                    {
                        CreateButton("ClassBellatorButton", "BELLATOR", canvasObject.transform, new Vector2(-130f, 47f), new Vector2(122f, 26f)),
                        CreateButton("ClassVenatorButton", "VENATOR", canvasObject.transform, new Vector2(0f, 47f), new Vector2(122f, 26f)),
                        CreateButton("ClassMagusButton", "MAGUS", canvasObject.transform, new Vector2(130f, 47f), new Vector2(122f, 26f))
                    };
                    CreateText("SkillSectionTitle", "ELIGE DOS HABILIDADES · SE CONSERVAN DURANTE LA EXPEDICIÓN",
                        canvasObject.transform, new Vector2(0f, 24f), new Vector2(390f, 18f), 8, "#F0C15A", FontStyle.Bold);
                    Button[] skillButtons =
                    {
                        CreateButton("SkillOptionOneButton", "HABILIDAD 1", canvasObject.transform, new Vector2(0f, 2f), new Vector2(286f, 22f)),
                        CreateButton("SkillOptionTwoButton", "HABILIDAD 2", canvasObject.transform, new Vector2(0f, -24f), new Vector2(286f, 22f)),
                        CreateButton("SkillOptionThreeButton", "HABILIDAD 3", canvasObject.transform, new Vector2(0f, -50f), new Vector2(286f, 22f))
                    };
                    Button human = CreateButton("RaceHumanButton", "HUMANO · DISPONIBLE · 2 FORTUNA",
                        canvasObject.transform, new Vector2(0f, -77f), new Vector2(255f, 20f));
                    Button[] lockedRaces =
                    {
                        CreateButton("RaceOrciniButton", "ORCINI · BLOQUEADA", canvasObject.transform, new Vector2(-130f, -99f), new Vector2(122f, 17f), enabled: false),
                        CreateButton("RaceAlvariButton", "ALVARI · BLOQUEADA", canvasObject.transform, new Vector2(0f, -99f), new Vector2(122f, 17f), enabled: false),
                        CreateButton("RaceDvergarButton", "DVERGAR · BLOQUEADA", canvasObject.transform, new Vector2(130f, -99f), new Vector2(122f, 17f), enabled: false)
                    };
                    Text summary = CreateText("PreparationSelectionSummary", "CLASE BELLATOR · RAZA HUMANO",
                        canvasObject.transform, new Vector2(-72f, -121f), new Vector2(258f, 24f), 7, "#BDC6CC", FontStyle.Normal);
                    Text status = CreateText("HubPreparationStatus", "Elige dos habilidades.",
                        canvasObject.transform, new Vector2(-66f, -132f), new Vector2(272f, 15f), 7, "#F0C15A", FontStyle.Bold);
                    Button start = CreateButton("StartExpeditionButton", "PARTIR",
                        canvasObject.transform, new Vector2(173f, -121f), new Vector2(88f, 28f), 10, "#10111B", "#F0C15A");
                    Button back = CreateButton("HubReturnToMenuButton", "VOLVER AL MENÚ",
                        canvasObject.transform, new Vector2(-12f, -12f), new Vector2(106f, 19f), 7, "#E8E3CD", "#32251F", topRight: true);
                    GameObject controllerObject = new GameObject("HubPreparation");
                    SceneManager.MoveGameObjectToScene(controllerObject, scene);
                    HubPreparationController controller = controllerObject.AddComponent<HubPreparationController>();
                    SerializedObject serializedController = new SerializedObject(controller);
                    SetArray(serializedController, "_classButtons", classButtons);
                    SetArray(serializedController, "_skillButtons", skillButtons);
                    SetArray(serializedController, "_lockedRaceButtons", lockedRaces);
                    serializedController.FindProperty("_humanRaceButton").objectReferenceValue = human;
                    serializedController.FindProperty("_startExpeditionButton").objectReferenceValue = start;
                    serializedController.FindProperty("_returnToMenuButton").objectReferenceValue = back;
                    serializedController.FindProperty("_selectionSummaryText").objectReferenceValue = summary;
                    serializedController.FindProperty("_statusText").objectReferenceValue = status;
                    serializedController.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                else if (FindObject(scene, "HubPreparation") == null)
                {
                    throw new InvalidOperationException("HubPreparationCanvas exists but its controller is missing; the scene was left unchanged.");
                }

                HubPreparationController existingController = FindComponent<HubPreparationController>(scene, "HubPreparation");
                if (existingController != null) changed |= EnsureUpgradePanel(scene, canvasObject, existingController);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unity could not save the Hub preparation scene.");
                }
            }
            finally
            {
                RestoreScene(scene, previousActiveScene);
            }
        }

        private static bool EnsureUpgradePanel(Scene scene, GameObject canvasObject, HubPreparationController controller)
        {
            bool changed = false;
            Transform canvas = canvasObject.transform;

            Button openButton = FindComponent<Button>(scene, "OpenPermanentUpgradesButton");
            if (openButton == null)
            {
                openButton = CreateButton("OpenPermanentUpgradesButton", "MEJORAS", canvas,
                    new Vector2(48f, -121f), new Vector2(94f, 22f), 8, "#10111B", "#AA7DC2");
                changed = true;
            }
            changed |= SetRect(openButton.GetComponent<RectTransform>(), new Vector2(48f, -121f), new Vector2(94f, 22f));
            changed |= SetButtonLabel(openButton, "MEJORAS");
            Text selectionSummary = FindComponent<Text>(scene, "PreparationSelectionSummary");
            if (selectionSummary != null)
                changed |= SetRect(selectionSummary.GetComponent<RectTransform>(), new Vector2(-127f, -118f), new Vector2(180f, 24f));
            Text hubStatus = FindComponent<Text>(scene, "HubPreparationStatus");
            if (hubStatus != null)
                changed |= SetRect(hubStatus.GetComponent<RectTransform>(), new Vector2(-127f, -135f), new Vector2(190f, 10f));

            Image panel = FindComponent<Image>(scene, "PermanentUpgradePanel");
            if (panel == null)
            {
                panel = CreateImage("PermanentUpgradePanel", canvas, "#10111B");
                panel.raycastTarget = true;
                changed = true;
            }
            changed |= SetRect(panel.rectTransform, Vector2.zero, new Vector2(390f, 238f));
            if (panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
                changed = true;
            }

            Text title = EnsurePanelText(scene, "PermanentUpgradeTitle", "MEJORAS PERMANENTES · RENOMBRE",
                panel.transform, new Vector2(0f, 94f), new Vector2(340f, 20f), 12, "#F0C15A", FontStyle.Bold, ref changed);
            Text balance = EnsurePanelText(scene, "PermanentUpgradeBalance", "RENOMBRE DISPONIBLE · 0",
                panel.transform, new Vector2(0f, 71f), new Vector2(330f, 18f), 9, "#E8E3CD", FontStyle.Normal, ref changed);
            Text status = EnsurePanelText(scene, "PermanentUpgradeStatus", string.Empty,
                panel.transform, new Vector2(0f, -101f), new Vector2(350f, 16f), 8, "#F0C15A", FontStyle.Bold, ref changed);
            Button health = EnsurePanelButton(scene, "PermanentHealthUpgradeButton", "SALUD MÁXIMA · +5 %",
                panel.transform, new Vector2(0f, 41f), ref changed);
            Button energy = EnsurePanelButton(scene, "PermanentEnergyUpgradeButton", "ENERGÍA MÁXIMA · +5 %",
                panel.transform, new Vector2(0f, 5f), ref changed);
            Button potion = EnsurePanelButton(scene, "PermanentPotionUpgradeButton", "POCIÓN INICIAL · +1",
                panel.transform, new Vector2(0f, -31f), ref changed);
            Button close = FindComponent<Button>(scene, "ClosePermanentUpgradesButton");
            if (close == null)
            {
                close = CreateButton("ClosePermanentUpgradesButton", "VOLVER", panel.transform,
                    new Vector2(0f, -72f), new Vector2(118f, 23f), 8, "#E8E3CD", "#32251F");
                changed = true;
            }
            changed |= SetRect(close.GetComponent<RectTransform>(), new Vector2(0f, -72f), new Vector2(118f, 23f));
            changed |= SetButtonLabel(close, "VOLVER");

            changed |= SetObjectReference(controller, "_openUpgradesButton", openButton);
            changed |= SetObjectReference(controller, "_upgradePanel", panel.gameObject);
            changed |= SetObjectReference(controller, "_upgradeBalanceText", balance);
            changed |= SetObjectReference(controller, "_upgradeStatusText", status);
            changed |= SetObjectReference(controller, "_healthUpgradeButton", health);
            changed |= SetObjectReference(controller, "_energyUpgradeButton", energy);
            changed |= SetObjectReference(controller, "_potionUpgradeButton", potion);
            changed |= SetObjectReference(controller, "_closeUpgradesButton", close);
            return changed;
        }

        private static Text EnsurePanelText(Scene scene, string name, string value, Transform parent, Vector2 position,
            Vector2 size, int fontSize, string color, FontStyle style, ref bool changed)
        {
            Text text = FindComponent<Text>(scene, name);
            if (text == null)
            {
                text = CreateText(name, value, parent, position, size, fontSize, color, style);
                changed = true;
            }
            if (text.transform.parent != parent)
            {
                text.transform.SetParent(parent, false);
                changed = true;
            }
            changed |= SetRect(text.GetComponent<RectTransform>(), position, size);
            if (text.text != value)
            {
                text.text = value;
                EditorUtility.SetDirty(text);
                changed = true;
            }
            return text;
        }

        private static Button EnsurePanelButton(Scene scene, string name, string label, Transform parent,
            Vector2 position, ref bool changed)
        {
            Button button = FindComponent<Button>(scene, name);
            if (button == null)
            {
                button = CreateButton(name, label, parent, position, new Vector2(338f, 28f),
                    8, "#E8E3CD", "#32251F");
                changed = true;
            }
            if (button.transform.parent != parent)
            {
                button.transform.SetParent(parent, false);
                changed = true;
            }
            changed |= SetRect(button.GetComponent<RectTransform>(), position, new Vector2(338f, 28f));
            changed |= SetButtonLabel(button, label);
            return button;
        }

        private static bool SetButtonLabel(Button button, string value)
        {
            Text label = button == null ? null : button.GetComponentInChildren<Text>(true);
            if (label == null || label.text == value) return false;
            label.text = value;
            EditorUtility.SetDirty(label);
            return true;
        }

        private static void EnsureMenuEntry()
        {
            const string menuPath = "Assets/Luckbound/Scenes/Menu.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(menuPath, OpenSceneMode.Additive);
            try
            {
                bool changed = EnsureEventSystem(scene);
                GameObject menuCanvas = FindObject(scene, "MenuCanvas");
                GameObject navigationObject = FindObject(scene, "MenuNavigation");
                MenuNavigationController navigation = navigationObject == null ? null : navigationObject.GetComponent<MenuNavigationController>();
                if (menuCanvas == null || navigation == null)
                    throw new InvalidOperationException("The existing Menu scene must have its canvas and navigation controller before adding the tavern entry.");

                Button tavernButton = FindObject(scene, "StartTavernButton")?.GetComponent<Button>();
                if (tavernButton == null)
                {
                    tavernButton = CreateButton("StartTavernButton", "PREPARAR EXPEDICIÓN", menuCanvas.transform,
                        new Vector2(-105f, -110f), new Vector2(190f, 24f), 9, "#10111B", "#F0C15A");
                    changed = true;
                }
                changed |= SetRect(tavernButton.GetComponent<RectTransform>(), new Vector2(-105f, -110f), new Vector2(190f, 24f));
                Text tavernLabel = tavernButton.GetComponentInChildren<Text>(true);
                if (tavernLabel != null && tavernLabel.text != "PREPARAR EXPEDICIÓN")
                {
                    tavernLabel.text = "PREPARAR EXPEDICIÓN";
                    EditorUtility.SetDirty(tavernLabel);
                    changed = true;
                }

                changed |= SetObjectReference(navigation, "_startTavernButton", tavernButton);
                changed |= SetTextLayout(scene, "MenuStatus", new Vector2(0.5f, 0.5f), new Vector2(0f, -122f),
                    new Vector2(300f, 12f), TextAnchor.MiddleCenter);
                changed |= SetTextLayout(scene, "MenuVersion", new Vector2(0.5f, 0.5f), new Vector2(0f, -133f),
                    new Vector2(300f, 8f), TextAnchor.MiddleCenter);
                GameObject bottomRule = FindObject(scene, "BottomRule");
                RectTransform ruleRect = bottomRule == null ? null : bottomRule.GetComponent<RectTransform>();
                if (ruleRect != null) changed |= SetRect(ruleRect, new Vector2(0f, -123f), ruleRect.sizeDelta);

                EventSystem eventSystem = FindEventSystem(scene);
                if (eventSystem != null && eventSystem.firstSelectedGameObject != tavernButton.gameObject)
                {
                    eventSystem.firstSelectedGameObject = tavernButton.gameObject;
                    EditorUtility.SetDirty(eventSystem);
                    changed = true;
                }

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, menuPath)) throw new IOException("Unity could not save the tavern menu entry.");
                }
            }
            finally
            {
                RestoreScene(scene, previousActiveScene);
            }
        }

        internal static bool EnsureEventSystem(Scene scene)
        {
            EventSystem[] systems = Resources.FindObjectsOfTypeAll<EventSystem>()
                .Where(item => item.gameObject.scene == scene).ToArray();
            if (systems.Length > 1) throw new InvalidOperationException(scene.name + " contains multiple EventSystems.");
            if (systems.Length == 1)
            {
                if (systems[0].GetComponent<InputSystemUIInputModule>() != null) return false;
                systems[0].gameObject.AddComponent<InputSystemUIInputModule>();
                EditorUtility.SetDirty(systems[0].gameObject);
                return true;
            }
            GameObject eventSystem = new GameObject("LuckboundEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
            return true;
        }

        internal static EventSystem FindEventSystem(Scene scene)
        {
            return Resources.FindObjectsOfTypeAll<EventSystem>().FirstOrDefault(item => item.gameObject.scene == scene);
        }

        internal static GameObject FindObject(Scene scene, string name)
        {
            return Resources.FindObjectsOfTypeAll<Transform>().FirstOrDefault(item => item.gameObject.scene == scene && item.name == name)?.gameObject;
        }

        internal static T FindComponent<T>(Scene scene, string name) where T : Component
        {
            GameObject found = FindObject(scene, name);
            return found == null ? null : found.GetComponent<T>();
        }

        internal static Canvas CreateCanvas(string name, Scene scene, int sortingOrder)
        {
            GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasObject, scene);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(480f, 270f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        internal static Image CreateImage(string name, Transform parent, string color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = ParseColor(color);
            return image;
        }

        internal static Text CreateText(string name, string value, Transform parent, Vector2 position, Vector2 size,
            int fontSize, string color, FontStyle style)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Place(textObject.GetComponent<RectTransform>(), size, position);
            Text text = textObject.GetComponent<Text>();
            text.text = value;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null) text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = ParseColor(color);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        internal static Button CreateButton(string name, string label, Transform parent, Vector2 position, Vector2 size,
            int fontSize = 9, string textColor = "#E8E3CD", string backgroundColor = "#32251F", bool enabled = true,
            bool topRight = false)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = topRight ? Vector2.one : new Vector2(0.5f, 0.5f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = rect.anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = ParseColor(backgroundColor);
            Button button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            ColorBlock colors = button.colors;
            colors.normalColor = ParseColor(backgroundColor);
            colors.highlightedColor = ParseColor("#493363");
            colors.pressedColor = ParseColor("#9A6026");
            colors.disabledColor = ParseColor("#3F465C");
            button.colors = colors;
            GameObject labelObject = new GameObject(name + "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
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
            text.color = ParseColor(textColor);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return button;
        }

        internal static void Place(RectTransform rect, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        internal static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetArray(SerializedObject target, string propertyName, UnityEngine.Object[] values)
        {
            SerializedProperty array = target.FindProperty(propertyName);
            array.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++) array.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static bool SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static bool SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            if (rect == null || (rect.anchoredPosition == position && rect.sizeDelta == size)) return false;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            EditorUtility.SetDirty(rect);
            return true;
        }

        private static bool SetTextLayout(Scene scene, string name, Vector2 anchor, Vector2 position, Vector2 size, TextAnchor alignment)
        {
            Text text = FindComponent<Text>(scene, name);
            if (text == null) return false;
            RectTransform rect = text.GetComponent<RectTransform>();
            bool changed = rect.anchorMin != anchor || rect.anchorMax != anchor || rect.anchoredPosition != position || rect.sizeDelta != size || text.alignment != alignment;
            if (!changed) return false;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            text.alignment = alignment;
            EditorUtility.SetDirty(text);
            EditorUtility.SetDirty(rect);
            return true;
        }

        private static Color ParseColor(string value)
        {
            if (!ColorUtility.TryParseHtmlString(value, out Color color)) throw new ArgumentException("Invalid palette color: " + value);
            return color;
        }

        private static void RestoreScene(Scene scene, Scene previousActiveScene)
        {
            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded) EditorSceneManager.SetActiveScene(previousActiveScene);
            if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
