using System;
using System.IO;
using Luckbound.App;
using Luckbound.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.Editor
{
    /// <summary>Generates the lightweight run receipt shown until expedition rooms are connected.</summary>
    public static class ExpeditionRunSessionScaffoldGenerator
    {
        private const string ScenePath = "Assets/Luckbound/Scenes/Expedition.unity";

        public static void EnsureGenerated()
        {
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject courseRoot = HubPreparationScaffoldGenerator.FindObject(scene, "MovementDemoCourse");
                GameObject hudRoot = HubPreparationScaffoldGenerator.FindObject(scene, "MovementDemoHUD");
                MovementDemoComposition demo = HubPreparationScaffoldGenerator.FindComponent<MovementDemoComposition>(scene, "MovementDemoComposition");
                if (courseRoot == null || hudRoot == null || demo == null)
                    throw new InvalidOperationException("The existing B07 demo must be complete before adding the run session view.");

                bool changed = SetObjectReference(demo, "_demoCourseRoot", courseRoot) |
                    SetObjectReference(demo, "_movementDemoHudRoot", hudRoot);
                GameObject canvasObject = HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionRunSessionCanvas");
                if (canvasObject == null)
                {
                    Canvas canvas = HubPreparationScaffoldGenerator.CreateCanvas("ExpeditionRunSessionCanvas", scene, 50);
                    canvasObject = canvas.gameObject;
                    Image background = HubPreparationScaffoldGenerator.CreateImage("RunSessionBackground", canvas.transform, "#10111B");
                    HubPreparationScaffoldGenerator.Stretch(background.rectTransform);
                    Image rule = HubPreparationScaffoldGenerator.CreateImage("RunSessionRule", canvas.transform, "#D68F32");
                    HubPreparationScaffoldGenerator.Place(rule.rectTransform, new Vector2(164f, 2f), new Vector2(0f, 83f));
                    Text title = HubPreparationScaffoldGenerator.CreateText("RunSessionTitle", "EXPEDICIÓN PREPARADA",
                        canvas.transform, new Vector2(0f, 100f), new Vector2(440f, 26f), 16, "#E8E3CD", FontStyle.Bold);
                    Text summary = HubPreparationScaffoldGenerator.CreateText("RunSessionSummary", "Esperando configuración…",
                        canvas.transform, new Vector2(0f, 10f), new Vector2(428f, 144f), 8, "#BDC6CC", FontStyle.Normal);
                    Button returnButton = HubPreparationScaffoldGenerator.CreateButton("ReturnToTavernButton", "VOLVER A LA TABERNA",
                        canvas.transform, new Vector2(110f, -112f), new Vector2(180f, 26f), 8, "#10111B", "#F0C15A");
                    Button craftButton = HubPreparationScaffoldGenerator.CreateButton("CraftHealthPotionButton",
                        "FABRICAR POCIÓN · 2 HIERBAS + 1 RESINA", canvas.transform,
                        new Vector2(-110f, -112f), new Vector2(220f, 26f), 8, "#E8E3CD", "#32251F");
                    Text craftStatus = HubPreparationScaffoldGenerator.CreateText("RunSessionCraftStatus", string.Empty,
                        canvas.transform, new Vector2(0f, -86f), new Vector2(420f, 16f), 8, "#F0C15A", FontStyle.Bold);
                    GameObject presenterObject = new GameObject("ExpeditionRunSession");
                    SceneManager.MoveGameObjectToScene(presenterObject, scene);
                    ExpeditionRunSessionPresenter presenter = presenterObject.AddComponent<ExpeditionRunSessionPresenter>();
                    var serialized = new SerializedObject(presenter);
                    serialized.FindProperty("_overlayRoot").objectReferenceValue = canvasObject;
                    serialized.FindProperty("_titleText").objectReferenceValue = title;
                    serialized.FindProperty("_summaryText").objectReferenceValue = summary;
                    serialized.FindProperty("_returnToTavernButton").objectReferenceValue = returnButton;
                    serialized.FindProperty("_craftHealthPotionButton").objectReferenceValue = craftButton;
                    serialized.FindProperty("_craftStatusText").objectReferenceValue = craftStatus;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                else
                {
                    if (HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionRunSession") == null)
                        throw new InvalidOperationException("ExpeditionRunSessionCanvas exists without its presenter; the scene was left unchanged.");
                    Text summary = HubPreparationScaffoldGenerator.FindObject(scene, "RunSessionSummary")?.GetComponent<Text>();
                    Text title = HubPreparationScaffoldGenerator.FindObject(scene, "RunSessionTitle")?.GetComponent<Text>();
                    Button returnButton = HubPreparationScaffoldGenerator.FindObject(scene, "ReturnToTavernButton")?.GetComponent<Button>();
                    if (summary == null || title == null || returnButton == null)
                        throw new InvalidOperationException("The existing ExpeditionRunSessionCanvas must contain its title, summary and return button.");

                    Button craftButton = HubPreparationScaffoldGenerator.FindObject(scene, "CraftHealthPotionButton")?.GetComponent<Button>();
                    if (craftButton == null)
                    {
                        craftButton = HubPreparationScaffoldGenerator.CreateButton("CraftHealthPotionButton",
                            "FABRICAR POCIÓN · 2 HIERBAS + 1 RESINA", canvasObject.transform,
                            new Vector2(-110f, -112f), new Vector2(220f, 26f), 8, "#E8E3CD", "#32251F");
                        changed = true;
                    }
                    Text craftStatus = HubPreparationScaffoldGenerator.FindObject(scene, "RunSessionCraftStatus")?.GetComponent<Text>();
                    if (craftStatus == null)
                    {
                        craftStatus = HubPreparationScaffoldGenerator.CreateText("RunSessionCraftStatus", string.Empty,
                            canvasObject.transform, new Vector2(0f, -86f), new Vector2(420f, 16f), 8, "#F0C15A", FontStyle.Bold);
                        changed = true;
                    }

                    changed |= SetRect(title.rectTransform, new Vector2(0f, 100f), new Vector2(440f, 26f));
                    changed |= SetRect(summary.rectTransform, new Vector2(0f, 10f), new Vector2(428f, 144f));
                    changed |= SetRect(returnButton.GetComponent<RectTransform>(), new Vector2(110f, -112f), new Vector2(180f, 26f));
                    changed |= SetRect(craftButton.GetComponent<RectTransform>(), new Vector2(-110f, -112f), new Vector2(220f, 26f));
                    changed |= SetRect(craftStatus.rectTransform, new Vector2(0f, -86f), new Vector2(420f, 16f));

                    ExpeditionRunSessionPresenter presenter = HubPreparationScaffoldGenerator.FindComponent<ExpeditionRunSessionPresenter>(scene, "ExpeditionRunSession");
                    if (presenter == null)
                        throw new InvalidOperationException("The existing ExpeditionRunSessionCanvas is missing its presenter; the scene was left unchanged.");
                    changed |= SetObjectReference(presenter, "_craftHealthPotionButton", craftButton);
                    changed |= SetObjectReference(presenter, "_craftStatusText", craftStatus);
                }

                changed |= EnsureGameplayComposition(scene);

                if (changed)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene, ScenePath)) throw new IOException("Unity could not save the expedition session scene.");
                }
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded) EditorSceneManager.SetActiveScene(previousActiveScene);
                if (scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static bool EnsureGameplayComposition(Scene scene)
        {
            GameObject sessionObject = HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionRunSession");
            if (sessionObject == null)
                throw new InvalidOperationException("ExpeditionRunSession must exist before wiring gameplay composition.");

            bool changed = false;
            GameObject hudRoot = HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionGameplayHUD");
            if (hudRoot == null)
            {
                Canvas hudCanvas = HubPreparationScaffoldGenerator.CreateCanvas("ExpeditionGameplayHUD", scene, 70);
                hudRoot = hudCanvas.gameObject;
                HubPreparationScaffoldGenerator.CreateText("ExpeditionPlayerHUD", string.Empty, hudCanvas.transform,
                    new Vector2(0f, 78f), new Vector2(610f, 24f), 8, "#E8E3CD", FontStyle.Bold);
                HubPreparationScaffoldGenerator.CreateText("ExpeditionPauseLabel", string.Empty, hudCanvas.transform,
                    new Vector2(0f, 4f), new Vector2(360f, 48f), 12, "#F0C15A", FontStyle.Bold);
                Button returnButton = HubPreparationScaffoldGenerator.CreateButton("ExpeditionReturnToTavernButton",
                    "VOLVER A LA TABERNA", hudCanvas.transform, new Vector2(0f, -44f), new Vector2(176f, 24f),
                    8, "#10111B", "#F0C15A");
                returnButton.gameObject.SetActive(false);
                hudRoot.SetActive(false);
                changed = true;
            }

            Text hudText = HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionPlayerHUD")?.GetComponent<Text>();
            Text pauseText = HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionPauseLabel")?.GetComponent<Text>();
            Button returnToTavern = HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionReturnToTavernButton")?.GetComponent<Button>();
            RoomCameraRig2D cameraRig = HubPreparationScaffoldGenerator.FindObject(scene, "Main Camera")?.GetComponent<RoomCameraRig2D>();
            if (hudText == null || pauseText == null || returnToTavern == null || cameraRig == null)
                throw new InvalidOperationException("Expedition gameplay HUD and camera references must be complete.");
            if (hudRoot.activeSelf)
            {
                hudRoot.SetActive(false);
                changed = true;
            }
            if (returnToTavern.gameObject.activeSelf)
            {
                returnToTavern.gameObject.SetActive(false);
                changed = true;
            }

            ExpeditionGameplayComposition composition = sessionObject.GetComponent<ExpeditionGameplayComposition>();
            if (composition == null)
            {
                composition = sessionObject.AddComponent<ExpeditionGameplayComposition>();
                changed = true;
            }
            ExpeditionRoomTransitionController transitions = sessionObject.GetComponent<ExpeditionRoomTransitionController>();
            if (transitions == null)
            {
                transitions = sessionObject.AddComponent<ExpeditionRoomTransitionController>();
                changed = true;
            }

            changed |= SetObjectReference(composition, "_cameraRig", cameraRig);
            changed |= SetObjectReference(composition, "_gameplayHudRoot", hudRoot);
            changed |= SetObjectReference(composition, "_playerHudText", hudText);
            changed |= SetObjectReference(composition, "_pauseLabelText", pauseText);
            changed |= SetObjectReference(composition, "_returnToTavernButton", returnToTavern);
            changed |= SetObjectReference(composition, "_roomTransitions", transitions);
            return changed;
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
            if (rect == null) return false;
            if (rect.anchoredPosition == position && rect.sizeDelta == size) return false;
            HubPreparationScaffoldGenerator.Place(rect, size, position);
            EditorUtility.SetDirty(rect);
            return true;
        }
    }
}
