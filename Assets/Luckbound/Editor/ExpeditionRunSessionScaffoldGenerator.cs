using System;
using System.IO;
using Luckbound.App;
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
                        canvas.transform, new Vector2(0f, 61f), new Vector2(440f, 30f), 18, "#E8E3CD", FontStyle.Bold);
                    Text summary = HubPreparationScaffoldGenerator.CreateText("RunSessionSummary", "Esperando configuración…",
                        canvas.transform, new Vector2(0f, -12f), new Vector2(428f, 116f), 9, "#BDC6CC", FontStyle.Normal);
                    Button returnButton = HubPreparationScaffoldGenerator.CreateButton("ReturnToTavernButton", "VOLVER A LA TABERNA",
                        canvas.transform, new Vector2(0f, -101f), new Vector2(198f, 28f), 9, "#10111B", "#F0C15A");
                    GameObject presenterObject = new GameObject("ExpeditionRunSession");
                    SceneManager.MoveGameObjectToScene(presenterObject, scene);
                    ExpeditionRunSessionPresenter presenter = presenterObject.AddComponent<ExpeditionRunSessionPresenter>();
                    var serialized = new SerializedObject(presenter);
                    serialized.FindProperty("_overlayRoot").objectReferenceValue = canvasObject;
                    serialized.FindProperty("_titleText").objectReferenceValue = title;
                    serialized.FindProperty("_summaryText").objectReferenceValue = summary;
                    serialized.FindProperty("_returnToTavernButton").objectReferenceValue = returnButton;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    changed = true;
                }
                else if (HubPreparationScaffoldGenerator.FindObject(scene, "ExpeditionRunSession") == null)
                {
                    throw new InvalidOperationException("ExpeditionRunSessionCanvas exists without its presenter; the scene was left unchanged.");
                }

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

        private static bool SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target);
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }
    }
}
