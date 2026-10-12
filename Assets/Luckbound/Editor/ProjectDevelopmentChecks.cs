using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luckbound.App;
using Luckbound.Content;
using Luckbound.Core.Combat;
using Luckbound.Core.Expeditions;
using Luckbound.Core.Inventory;
using Luckbound.Gameplay.Enemies;
using Luckbound.Gameplay.Movement;
using Luckbound.Gameplay.Rooms;
using Luckbound.Presentation;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Luckbound.Editor
{
    /// <summary>
    /// Performs non-destructive checks for the clean project scaffold and exposes them to CI.
    /// </summary>
    public static class ProjectDevelopmentChecks
    {
        private const string ProjectVersionPath = "ProjectSettings/ProjectVersion.txt";
        private const string InputActionsPath = "Assets/Resources/Luckbound/InputSystem_Actions.inputactions";
        private const string PlayerVisualControllerPath = "Assets/Resources/Luckbound/PlayerVisualStates.controller";
        private const string WebGlProfilePath = "Assets/Settings/Build Profiles/WebGL.asset";
        private static readonly string[] StartupScenes = { "Bootstrap", "Menu", "Hub", "Expedition", "CombatArena", "CustodianArena" };

        [MenuItem("Luckbound/Validate Project")]
        public static void RunAll()
        {
            IReadOnlyList<string> errors = CollectErrors();
            if (errors.Count > 0)
            {
                string report = string.Join(Environment.NewLine, errors.Select(error => "• " + error));
                Debug.LogError("Luckbound project checks failed:" + Environment.NewLine + report);
                throw new InvalidOperationException("Luckbound project checks failed with " + errors.Count + " error(s).");
            }

            Debug.Log("Luckbound project checks passed for Unity " + Application.unityVersion + ".");
        }

        public static IReadOnlyList<string> CollectErrors()
        {
            var errors = new List<string>();
            ValidateEditorVersion(errors);
            ValidateBuildScenes(errors);
            ValidateInputActions(errors);
            ValidateGroundLayer(errors);
            ValidateEnemyLayer(errors);
            ValidateSimulationRate(errors);
            ValidateWebGlProfile(errors);
            ValidateAssemblies(errors);
            ValidatePlayerVisualController(errors);

            foreach (string sceneName in StartupScenes)
            {
                ValidateScene(sceneName, errors);
            }

            ValidateCombatArena(errors);
            ValidateCustodianArena(errors);
            ValidateRoomTemplate(errors);
            ValidateExpeditionRouteCatalog(errors);
            ValidateItemCatalog(errors);

            return errors;
        }

        private static void ValidateEditorVersion(List<string> errors)
        {
            if (!File.Exists(ProjectVersionPath))
            {
                errors.Add("Missing " + ProjectVersionPath + ".");
                return;
            }

            string editorVersion = File.ReadAllLines(ProjectVersionPath)
                .FirstOrDefault(line => line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))?
                .Substring("m_EditorVersion:".Length)
                .Trim();

            if (string.IsNullOrEmpty(editorVersion))
            {
                errors.Add(ProjectVersionPath + " does not declare m_EditorVersion.");
            }
            else if (!string.Equals(editorVersion, Application.unityVersion, StringComparison.Ordinal))
            {
                errors.Add("The active Editor is " + Application.unityVersion +
                    " but the project requires " + editorVersion + ".");
            }
        }

        private static void ValidateBuildScenes(List<string> errors)
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes ?? Array.Empty<EditorBuildSettingsScene>();
            if (scenes.Length < StartupScenes.Length)
            {
                errors.Add("Build Settings must start with Bootstrap, Menu, Hub and Expedition.");
                return;
            }

            for (int index = 0; index < StartupScenes.Length; index++)
            {
                string expectedPath = ScenePath(StartupScenes[index]);
                if (!scenes[index].enabled || !string.Equals(scenes[index].path, expectedPath, StringComparison.Ordinal))
                {
                    errors.Add("Build Settings entry " + index + " must be enabled and point to " + expectedPath + ".");
                }

                int occurrences = scenes.Count(scene => string.Equals(scene.path, expectedPath, StringComparison.Ordinal));
                if (occurrences != 1)
                {
                    errors.Add("Build Settings must contain " + expectedPath + " exactly once; found " + occurrences + ".");
                }
            }
        }

        private static void ValidateInputActions(List<string> errors)
        {
            if (AssetDatabase.LoadMainAssetAtPath(InputActionsPath) == null)
            {
                errors.Add("Input Actions asset could not be imported from " + InputActionsPath + ".");
                return;
            }

            try
            {
                InputActionsDefinition actions = JsonUtility.FromJson<InputActionsDefinition>(File.ReadAllText(InputActionsPath));
                InputActionMapDefinition playerMap = actions?.maps?.FirstOrDefault(map =>
                    map != null && string.Equals(map.name, "Player", StringComparison.Ordinal));
                if (playerMap == null)
                {
                    errors.Add("Input Actions must contain the Player action map.");
                    return;
                }

                var requiredActions = new Dictionary<string, string[]>
                {
                    { "Move", new[] { "Value", "Axis" } },
                    { "Jump", new[] { "Button", "Button" } },
                    { "Attack", new[] { "Button", "Button" } },
                    { "Dodge", new[] { "Button", "Button" } },
                    { "Skill1", new[] { "Button", "Button" } },
                    { "Skill2", new[] { "Button", "Button" } },
                    { "Interact", new[] { "Button", "Button" } },
                    { "Consumable", new[] { "Button", "Button" } },
                    { "Inventory", new[] { "Button", "Button" } },
                    { "Map", new[] { "Button", "Button" } },
                    { "Pause", new[] { "Button", "Button" } }
                };

                foreach (KeyValuePair<string, string[]> requirement in requiredActions)
                {
                    bool found = playerMap.actions != null && playerMap.actions.Any(action => action != null &&
                        string.Equals(action.name, requirement.Key, StringComparison.Ordinal) &&
                        string.Equals(action.type, requirement.Value[0], StringComparison.Ordinal) &&
                        string.Equals(action.expectedControlType, requirement.Value[1], StringComparison.Ordinal));
                    if (!found)
                    {
                        errors.Add("Player map is missing action " + requirement.Key + " with the required type.");
                    }
                }

                string[][] requiredBindings =
                {
                    new[] { "Move", "<Gamepad>/leftStick/x", "Gamepad" },
                    new[] { "Move", "<Keyboard>/a", "Keyboard&Mouse" },
                    new[] { "Move", "<Keyboard>/d", "Keyboard&Mouse" },
                    new[] { "Move", "<Keyboard>/leftArrow", "Keyboard&Mouse" },
                    new[] { "Move", "<Keyboard>/rightArrow", "Keyboard&Mouse" },
                    new[] { "Jump", "<Keyboard>/space", "Keyboard&Mouse" },
                    new[] { "Jump", "<Gamepad>/buttonSouth", "Gamepad" },
                    new[] { "Attack", "<Mouse>/leftButton", "Keyboard&Mouse" },
                    new[] { "Attack", "<Keyboard>/j", "Keyboard&Mouse" },
                    new[] { "Attack", "<Gamepad>/buttonWest", "Gamepad" },
                    new[] { "Dodge", "<Keyboard>/leftShift", "Keyboard&Mouse" },
                    new[] { "Dodge", "<Gamepad>/rightShoulder", "Gamepad" },
                    new[] { "Skill1", "<Mouse>/rightButton", "Keyboard&Mouse" },
                    new[] { "Skill1", "<Keyboard>/k", "Keyboard&Mouse" },
                    new[] { "Skill1", "<Gamepad>/buttonNorth", "Gamepad" },
                    new[] { "Skill2", "<Keyboard>/q", "Keyboard&Mouse" },
                    new[] { "Skill2", "<Keyboard>/l", "Keyboard&Mouse" },
                    new[] { "Skill2", "<Gamepad>/rightTrigger", "Gamepad" },
                    new[] { "Interact", "<Keyboard>/e", "Keyboard&Mouse" },
                    new[] { "Interact", "<Gamepad>/buttonEast", "Gamepad" },
                    new[] { "Consumable", "<Keyboard>/f", "Keyboard&Mouse" },
                    new[] { "Consumable", "<Gamepad>/leftShoulder", "Gamepad" },
                    new[] { "Inventory", "<Keyboard>/tab", "Keyboard&Mouse" },
                    new[] { "Inventory", "<Gamepad>/select", "Gamepad" },
                    new[] { "Map", "<Keyboard>/m", "Keyboard&Mouse" },
                    new[] { "Map", "<Gamepad>/dpad/up", "Gamepad" },
                    new[] { "Pause", "<Keyboard>/escape", "Keyboard&Mouse" },
                    new[] { "Pause", "<Gamepad>/start", "Gamepad" }
                };

                foreach (string[] requirement in requiredBindings)
                {
                    bool found = playerMap.bindings != null && playerMap.bindings.Any(binding => binding != null &&
                        string.Equals(binding.action, requirement[0], StringComparison.Ordinal) &&
                        string.Equals(binding.path, requirement[1], StringComparison.Ordinal) &&
                        ContainsBindingGroup(binding.groups, requirement[2]));
                    if (!found)
                    {
                        errors.Add("Player map is missing " + requirement[0] + " binding " + requirement[1] +
                            " for " + requirement[2] + ".");
                    }
                }
            }
            catch (Exception exception)
            {
                errors.Add("Could not parse " + InputActionsPath + ": " + exception.Message);
            }
        }

        private static bool ContainsBindingGroup(string groups, string requiredGroup)
        {
            return !string.IsNullOrEmpty(groups) && groups.Split(';').Any(group =>
                string.Equals(group.Trim(), requiredGroup, StringComparison.Ordinal));
        }

        private static void ValidateGroundLayer(List<string> errors)
        {
            if (InternalEditorUtility.layers == null || !InternalEditorUtility.layers.Contains("Ground"))
            {
                errors.Add("The Ground layer is required for actor grounding and collision scenarios.");
            }
        }

        private static void ValidateEnemyLayer(List<string> errors)
        {
            if (InternalEditorUtility.layers == null || !InternalEditorUtility.layers.Contains("Enemies"))
            {
                errors.Add("The Enemies layer is required to filter combat projectiles away from the player and room geometry.");
            }
        }

        private static void ValidateSimulationRate(List<string> errors)
        {
            const float expectedFixedDeltaTime = 1f / 60f;
            if (Mathf.Abs(Time.fixedDeltaTime - expectedFixedDeltaTime) > 0.000001f)
            {
                errors.Add("Physics fixed timestep must be 1/60 second; found " + Time.fixedDeltaTime + ".");
            }
        }

        private static void ValidateWebGlProfile(List<string> errors)
        {
            if (AssetDatabase.LoadMainAssetAtPath(WebGlProfilePath) == null)
            {
                errors.Add("Missing or invalid WebGL Build Profile at " + WebGlProfilePath + ".");
            }
        }

        private static void ValidateAssemblies(List<string> errors)
        {
            AssemblyDefinition core = ReadAssembly("Assets/Luckbound/Scripts/Core/Luckbound.Core.asmdef", errors);
            AssemblyDefinition content = ReadAssembly("Assets/Luckbound/Scripts/Content/Luckbound.Content.asmdef", errors);
            AssemblyDefinition gameplay = ReadAssembly("Assets/Luckbound/Scripts/Gameplay/Luckbound.Gameplay.asmdef", errors);
            AssemblyDefinition presentation = ReadAssembly("Assets/Luckbound/Scripts/Presentation/Luckbound.Presentation.asmdef", errors);
            AssemblyDefinition app = ReadAssembly("Assets/Luckbound/Scripts/App/Luckbound.App.asmdef", errors);
            AssemblyDefinition editor = ReadAssembly("Assets/Luckbound/Editor/Luckbound.Editor.asmdef", errors);

            if (core != null && (!core.noEngineReferences || HasProjectAssemblyReference(core)))
            {
                errors.Add("Luckbound.Core must have no Unity engine references and no project assembly references.");
            }
            if (content != null && !References(content, "Luckbound.Core"))
            {
                errors.Add("Luckbound.Content must reference Luckbound.Core.");
            }
            if (gameplay != null && (!References(gameplay, "Luckbound.Core") ||
                !References(gameplay, "Luckbound.Content") || !References(gameplay, "Unity.InputSystem") ||
                References(gameplay, "Luckbound.Presentation")))
            {
                errors.Add("Luckbound.Gameplay must depend on Core, Content and Unity.InputSystem, never Presentation.");
            }
            if (presentation != null && (!References(presentation, "Luckbound.Core") ||
                !References(presentation, "Luckbound.Content") ||
                !References(presentation, "Unity.Cinemachine") ||
                !References(presentation, "Unity.RenderPipelines.Universal.Runtime") ||
                !References(presentation, "Unity.RenderPipelines.Universal.2D.Runtime") ||
                References(presentation, "Luckbound.Gameplay")))
            {
                errors.Add("Luckbound.Presentation must depend on Core, Content, Cinemachine and URP, never Gameplay.");
            }
            if (app != null && (!References(app, "Luckbound.Core") || !References(app, "Luckbound.Content") ||
                !References(app, "Luckbound.Gameplay") || !References(app, "Luckbound.Presentation")))
            {
                errors.Add("Luckbound.App must compose the Core, Content, Gameplay and Presentation assemblies.");
            }
            if (editor != null && (!References(editor, "Luckbound.App") || !References(editor, "Luckbound.Core") ||
                editor.includePlatforms == null || !editor.includePlatforms.Contains("Editor")))
            {
                errors.Add("Luckbound.Editor must be Editor-only and reference Luckbound.App and Luckbound.Core.");
            }
        }

        private static AssemblyDefinition ReadAssembly(string path, List<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add("Missing assembly definition " + path + ".");
                return null;
            }

            try
            {
                AssemblyDefinition assembly = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));
                if (assembly == null || string.IsNullOrEmpty(assembly.name))
                {
                    errors.Add("Invalid assembly definition " + path + ".");
                    return null;
                }

                return assembly;
            }
            catch (Exception exception)
            {
                errors.Add("Could not parse " + path + ": " + exception.Message);
                return null;
            }
        }

        private static bool References(AssemblyDefinition assembly, string reference)
        {
            return assembly.references != null && assembly.references.Contains(reference);
        }

        private static bool HasProjectAssemblyReference(AssemblyDefinition assembly)
        {
            return assembly.references != null && assembly.references.Any(reference =>
                reference.StartsWith("Luckbound.", StringComparison.Ordinal));
        }

        private static void ValidateScene(string sceneName, List<string> errors)
        {
            string path = ScenePath(sceneName);
            if (!File.Exists(path))
            {
                errors.Add("Missing startup scene " + path + ".");
                return;
            }

            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = SceneManager.GetSceneByPath(path);
            bool openedByCheck = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (openedByCheck)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }
                List<GameObject> objects = GetHierarchyObjects(scene);

                if (sceneName == "Bootstrap")
                {
                    int bootstrapperCount = objects.Sum(gameObject =>
                        gameObject.GetComponents<ApplicationBootstrapper>().Length);
                    if (bootstrapperCount != 1)
                    {
                        errors.Add("Bootstrap must contain exactly one ApplicationBootstrapper; found " + bootstrapperCount + ".");
                    }
                }
                else
                {
                    int cameraCount = objects.Count(gameObject => gameObject.GetComponent<Camera>() != null &&
                        gameObject.name == "Main Camera");
                    if (cameraCount != 1)
                    {
                        errors.Add(sceneName + " must contain exactly one Main Camera; found " + cameraCount + ".");
                    }
                }

                if (sceneName == "Menu")
                {
                    ValidateMenuText(objects, "LuckboundTitle", "LUCKBOUND", errors);
                    ValidateMenuText(objects, "MenuVersion", "PREPRODUCCIÓN  ·  0.1.0", errors);
                    ValidateMenuMovementEntry(objects, errors);
                }

                if (sceneName == "Hub")
                {
                    ValidateHubPreparation(objects, errors);
                }

                if (sceneName == "Expedition")
                {
                    ValidateExpeditionCamera(objects, errors);
                    ValidateMovementDemo(objects, errors);
                    ValidateExpeditionRunSession(objects, errors);
                }
            }
            catch (Exception exception)
            {
                errors.Add("Could not inspect " + path + ": " + exception.Message);
            }
            finally
            {
                if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
                {
                    EditorSceneManager.SetActiveScene(previousActiveScene);
                }
                if (openedByCheck && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateMenuMovementEntry(List<GameObject> objects, List<string> errors)
        {
            Button startButton = objects.FirstOrDefault(gameObject => gameObject.name == "StartMovementDemoButton")
                ?.GetComponent<Button>();
            Text buttonLabel = startButton == null ? null : startButton.GetComponentInChildren<Text>(true);
            if (startButton == null || buttonLabel == null || buttonLabel.text != "JUGAR DEMO 0.1")
            {
                errors.Add("Menu must expose the JUGAR DEMO 0.1 button.");
            }

            if (objects.Count(gameObject => gameObject.GetComponent<MenuNavigationController>() != null) != 1)
            {
                errors.Add("Menu must contain exactly one MenuNavigationController.");
            }

            Button arenaButton = objects.FirstOrDefault(gameObject => gameObject.name == "StartCombatArenaButton")?.GetComponent<Button>();
            Text arenaLabel = arenaButton == null ? null : arenaButton.GetComponentInChildren<Text>(true);
            if (arenaButton == null || arenaLabel == null || arenaLabel.text != "LUCHAR · ARENA 0.2")
            {
                errors.Add("Menu must expose the LUCHAR · ARENA 0.2 combat entry.");
            }

            Button tavernButton = objects.FirstOrDefault(gameObject => gameObject.name == "StartTavernButton")?.GetComponent<Button>();
            Text tavernLabel = tavernButton == null ? null : tavernButton.GetComponentInChildren<Text>(true);
            if (tavernButton == null || tavernLabel == null || tavernLabel.text != "PREPARAR EXPEDICIÓN")
            {
                errors.Add("Menu must expose the PREPARAR EXPEDICIÓN entry to Hub.");
            }

            Button custodianButton = objects.FirstOrDefault(gameObject => gameObject.name == "StartCustodianArenaButton")?.GetComponent<Button>();
            Text custodianLabel = custodianButton == null ? null : custodianButton.GetComponentInChildren<Text>(true);
            if (custodianButton == null || custodianLabel == null || custodianLabel.text != "ENFRENTAR · CUSTODIO (D06)")
            {
                errors.Add("Menu must expose the ENFRENTAR · CUSTODIO (D06) provisional-boss entry.");
            }

            MenuNavigationController navigation = objects.SelectMany(gameObject => gameObject.GetComponents<MenuNavigationController>()).FirstOrDefault();
            if (navigation == null)
            {
                errors.Add("Menu navigation must route the movement prototype, arenas and tavern.");
            }
            else
            {
                SerializedObject navigationData = new SerializedObject(navigation);
                if (navigationData.FindProperty("_startArenaButton").objectReferenceValue != arenaButton)
                {
                    errors.Add("MenuNavigationController must serialize the combat-arena button.");
                }
                if (navigationData.FindProperty("_startTavernButton").objectReferenceValue != tavernButton)
                {
                    errors.Add("MenuNavigationController must serialize the tavern-preparation button.");
                }
                if (navigationData.FindProperty("_startCustodianButton").objectReferenceValue != custodianButton)
                {
                    errors.Add("MenuNavigationController must serialize the D06 Custodian button.");
                }
            }

            if (objects.Count(gameObject => gameObject.GetComponent<EventSystem>() != null) != 1 ||
                objects.Count(gameObject => gameObject.GetComponent<InputSystemUIInputModule>() != null) != 1)
            {
                errors.Add("Menu must contain one EventSystem using InputSystemUIInputModule.");
            }
        }

        private static void ValidateHubPreparation(List<GameObject> objects, List<string> errors)
        {
            string[] requiredButtons =
            {
                "ClassBellatorButton", "ClassVenatorButton", "ClassMagusButton",
                "SkillOptionOneButton", "SkillOptionTwoButton", "SkillOptionThreeButton",
                "RaceHumanButton", "RaceOrciniButton", "RaceAlvariButton", "RaceDvergarButton",
                "StartExpeditionButton", "HubReturnToMenuButton"
            };
            foreach (string buttonName in requiredButtons)
            {
                if (objects.FirstOrDefault(gameObject => gameObject.name == buttonName)?.GetComponent<Button>() == null)
                    errors.Add("Hub must expose the " + buttonName + " control.");
            }

            HubPreparationController[] controllers = objects.SelectMany(gameObject => gameObject.GetComponents<HubPreparationController>()).ToArray();
            if (controllers.Length != 1)
            {
                errors.Add("Hub must contain exactly one HubPreparationController; found " + controllers.Length + ".");
                return;
            }

            SerializedObject serialized = new SerializedObject(controllers[0]);
            string[] arrayNames = { "_classButtons", "_skillButtons", "_lockedRaceButtons" };
            int[] expectedSizes = { 3, 3, 3 };
            for (int index = 0; index < arrayNames.Length; index++)
            {
                SerializedProperty array = serialized.FindProperty(arrayNames[index]);
                if (array == null || array.arraySize != expectedSizes[index] ||
                    Enumerable.Range(0, expectedSizes[index]).Any(item => array.GetArrayElementAtIndex(item).objectReferenceValue == null))
                    errors.Add("HubPreparationController must serialize three " + arrayNames[index] + " references.");
            }
            foreach (string propertyName in new[] { "_humanRaceButton", "_startExpeditionButton", "_returnToMenuButton", "_selectionSummaryText", "_statusText" })
            {
                if (serialized.FindProperty(propertyName)?.objectReferenceValue == null)
                    errors.Add("HubPreparationController must serialize " + propertyName + ".");
            }
            if (objects.Count(gameObject => gameObject.GetComponent<EventSystem>() != null) != 1 ||
                objects.Count(gameObject => gameObject.GetComponent<InputSystemUIInputModule>() != null) != 1)
                errors.Add("Hub must contain one EventSystem using InputSystemUIInputModule.");
        }

        private static void ValidateExpeditionRunSession(List<GameObject> objects, List<string> errors)
        {
            ExpeditionRunSessionPresenter[] presenters = objects.SelectMany(gameObject => gameObject.GetComponents<ExpeditionRunSessionPresenter>()).ToArray();
            if (presenters.Length != 1)
            {
                errors.Add("Expedition must contain exactly one ExpeditionRunSessionPresenter; found " + presenters.Length + ".");
                return;
            }
            SerializedObject presenter = new SerializedObject(presenters[0]);
            foreach (string propertyName in new[] { "_overlayRoot", "_titleText", "_summaryText", "_returnToTavernButton" })
            {
                if (presenter.FindProperty(propertyName)?.objectReferenceValue == null)
                    errors.Add("ExpeditionRunSessionPresenter must serialize " + propertyName + ".");
            }
            MovementDemoComposition[] demos = objects.SelectMany(gameObject => gameObject.GetComponents<MovementDemoComposition>()).ToArray();
            if (demos.Length != 1)
            {
                errors.Add("Expedition run routing requires exactly one MovementDemoComposition.");
                return;
            }
            SerializedObject demo = new SerializedObject(demos[0]);
            foreach (string propertyName in new[] { "_demoCourseRoot", "_movementDemoHudRoot" })
            {
                if (demo.FindProperty(propertyName)?.objectReferenceValue == null)
                    errors.Add("MovementDemoComposition must serialize " + propertyName + " for run/demo scene routing.");
            }
        }

        private static void ValidateCombatArena(List<string> errors)
        {
            Scene scene = SceneManager.GetSceneByName("CombatArena");
            bool openedByCheck = !scene.IsValid() || !scene.isLoaded;
            if (openedByCheck)
            {
                string path = ScenePath("CombatArena");
                if (!File.Exists(path))
                {
                    errors.Add("CombatArena scene is missing.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                List<GameObject> objects = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => transform.gameObject)
                    .ToList();
                CombatArenaComposition[] compositions = objects.SelectMany(gameObject => gameObject.GetComponents<CombatArenaComposition>()).ToArray();
                if (compositions.Length != 1)
                {
                    errors.Add("CombatArena must contain exactly one CombatArenaComposition; found " + compositions.Length + ".");
                    return;
                }

                SerializedObject composition = new SerializedObject(compositions[0]);
                if (composition.FindProperty("_playerPrefab").objectReferenceValue == null ||
                    composition.FindProperty("_slimePrefab").objectReferenceValue == null ||
                    composition.FindProperty("_huesoguardiaPrefab").objectReferenceValue == null ||
                    composition.FindProperty("_playerSpawn").objectReferenceValue == null ||
                    composition.FindProperty("_roomBounds").objectReferenceValue == null ||
                    composition.FindProperty("_cameraRig").objectReferenceValue == null ||
                    composition.FindProperty("_telemetryText").objectReferenceValue == null ||
                    composition.FindProperty("_encounterText").objectReferenceValue == null ||
                    composition.FindProperty("_pauseText").objectReferenceValue == null ||
                    composition.FindProperty("_returnToMenuButton").objectReferenceValue == null)
                {
                    errors.Add("CombatArenaComposition must serialize its player, enemy prototypes, spawn, bounds, camera and UI references.");
                }

                if (objects.Count(gameObject => gameObject.GetComponent<EventSystem>() != null) != 1 ||
                    objects.Count(gameObject => gameObject.GetComponent<InputSystemUIInputModule>() != null) != 1)
                {
                    errors.Add("CombatArena must contain one EventSystem using InputSystemUIInputModule.");
                }

                if (AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Luckbound/Data/Encounters/C08Arena.json") == null)
                {
                    errors.Add("CombatArena encounter data must be present in Resources.");
                }
            }
            finally
            {
                if (openedByCheck && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateCustodianArena(List<string> errors)
        {
            const string dataPath = "Assets/Resources/Luckbound/Data/Encounters/CustodianBoss.json";
            const string bossPrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianPrototype.prefab";
            const string wavePrefabPath = "Assets/Luckbound/Prefabs/CustodianArena/CustodianWavePrototype.prefab";
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(dataPath);
            if (source == null)
            {
                errors.Add("D06 Custodian definition is missing at " + dataPath + ".");
            }
            else
            {
                try
                {
                    CustodianBossDefinition definition = CustodianBossJsonLoader.Load(source.text);
                    if (definition.MaximumHealth != 900 || definition.Defense != 3 || definition.CutDamage != 14 ||
                        definition.WaveDamage != 12 || definition.ChargeDamage != 16)
                        errors.Add("D06 Custodian values differ from the boss specification.");
                }
                catch (Exception exception)
                {
                    errors.Add("D06 Custodian definition is invalid: " + exception.Message);
                }
            }

            GameObject bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(bossPrefabPath);
            if (bossPrefab == null || bossPrefab.GetComponent<CustodianBoss2D>() == null ||
                bossPrefab.GetComponent<Rigidbody2D>() == null || bossPrefab.GetComponent<Collider2D>() == null)
                errors.Add("D06 Custodian prefab must include its behavior and physical body.");
            GameObject wavePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(wavePrefabPath);
            if (wavePrefab == null || wavePrefab.GetComponent<CustodianWaveHazard2D>() == null ||
                wavePrefab.GetComponent<Rigidbody2D>() == null || wavePrefab.GetComponent<Collider2D>() == null)
                errors.Add("D06 wave prefab must include its behavior and physical trigger.");

            Scene scene = SceneManager.GetSceneByName("CustodianArena");
            bool openedByCheck = !scene.IsValid() || !scene.isLoaded;
            if (openedByCheck)
            {
                string path = ScenePath("CustodianArena");
                if (!File.Exists(path))
                {
                    errors.Add("CustodianArena scene is missing.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            }

            try
            {
                List<GameObject> objects = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                    .Select(transform => transform.gameObject)
                    .ToList();
                CustodianArenaComposition[] compositions = objects.SelectMany(gameObject => gameObject.GetComponents<CustodianArenaComposition>()).ToArray();
                if (compositions.Length != 1)
                {
                    errors.Add("CustodianArena must contain exactly one CustodianArenaComposition; found " + compositions.Length + ".");
                    return;
                }

                SerializedObject composition = new SerializedObject(compositions[0]);
                string[] references = { "_playerPrefab", "_custodianPrefab", "_wavePrefab", "_playerSpawn", "_waveRoot",
                    "_roomBounds", "_cameraRig", "_telemetryText", "_bossStatusText", "_pauseText", "_returnToMenuButton" };
                foreach (string reference in references)
                {
                    if (composition.FindProperty(reference)?.objectReferenceValue == null)
                        errors.Add("CustodianArenaComposition must serialize " + reference + ".");
                }
                if (objects.Count(gameObject => gameObject.GetComponent<EventSystem>() != null) != 1 ||
                    objects.Count(gameObject => gameObject.GetComponent<InputSystemUIInputModule>() != null) != 1)
                    errors.Add("CustodianArena must contain one EventSystem using InputSystemUIInputModule.");
            }
            finally
            {
                if (openedByCheck && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void ValidateRoomTemplate(List<string> errors)
        {
            const string definitionPath = "Assets/Resources/Luckbound/Data/Rooms/D01PrototypeRoom.json";
            const string prefabPath = "Assets/Resources/Luckbound/Rooms/PrototypeRoom.prefab";
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(definitionPath);
            if (source == null)
            {
                errors.Add("D01 room template JSON could not be imported from " + definitionPath + ".");
                return;
            }

            try
            {
                RoomTemplateJsonLoader.Load(source.text);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                RoomTemplatePrefab2D template = prefab == null ? null : prefab.GetComponent<RoomTemplatePrefab2D>();
                if (template == null)
                {
                    errors.Add("D01 room prefab must include RoomTemplatePrefab2D at " + prefabPath + ".");
                    return;
                }

                template.ValidateGeneratedPrefab();
            }
            catch (Exception exception)
            {
                errors.Add("D01 room template or generated prefab is invalid: " + exception.Message);
            }
        }

        private static void ValidateExpeditionRouteCatalog(List<string> errors)
        {
            const string catalogPath = "Assets/Resources/Luckbound/Data/Rooms/RouteTemplates.json";
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(catalogPath);
            if (source == null)
            {
                errors.Add("D02 expedition route catalog could not be imported from " + catalogPath + ".");
                return;
            }

            try
            {
                ExpeditionRouteCatalog catalog = ExpeditionRouteCatalogJsonLoader.Load(source.text);
                ExpeditionRoutePlan route = ExpeditionRouteGenerator.Generate(17320508u, catalog);
                IReadOnlyList<string> routeErrors = route.Validate();
                foreach (string error in routeErrors) errors.Add("D02 generated route is invalid: " + error);
                if (route.Nodes.Count != 24 || route.MandatoryNodeIds.Count != 21 || route.Connections.Count != 26)
                {
                    errors.Add("D02 route must contain 21 mandatory locations, three optional rooms and 26 connections.");
                }
            }
            catch (Exception exception)
            {
                errors.Add("D02 expedition route catalog is invalid: " + exception.Message);
            }
        }

        private static void ValidateItemCatalog(List<string> errors)
        {
            const string catalogPath = "Assets/Resources/Luckbound/Data/Items/Items.json";
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(catalogPath);
            if (source == null)
            {
                errors.Add("D04 item catalog could not be imported from " + catalogPath + ".");
                return;
            }

            try
            {
                ItemCatalog catalog = ItemCatalogJsonLoader.Load(source.text);
                if (catalog.Count != 16)
                {
                    errors.Add("D04 starter item catalog must contain 16 item definitions including six materials; found " + catalog.Count + ".");
                }
                if (!catalog.Get("weapon.bellator.rusted_sword").IsCompatibleWithClass("class.bellator") ||
                    !catalog.Get("weapon.venator.short_bow").IsCompatibleWithClass("class.venator") ||
                    !catalog.Get("weapon.magus.ember_staff").IsCompatibleWithClass("class.magus"))
                {
                    errors.Add("D04 starter weapons must match the Bellator, Venator and Magus classes.");
                }
                if (catalog.StartingLoadouts.Count != 3 ||
                    catalog.GetStartingLoadout("class.bellator").WeaponItemId != "weapon.bellator.rusted_sword" ||
                    catalog.GetStartingLoadout("class.venator").WeaponItemId != "weapon.venator.short_bow" ||
                    catalog.GetStartingLoadout("class.magus").WeaponItemId != "weapon.magus.ember_staff" ||
                    catalog.GetStartingLoadout("class.bellator").ArmorItemId != "armor.bellator.guild_cuirass" ||
                    catalog.GetStartingLoadout("class.venator").ArmorItemId != "armor.venator.leather_vest" ||
                    catalog.GetStartingLoadout("class.magus").ArmorItemId != "armor.magus.traveler_robe")
                    errors.Add("Items.json must map each playable class to its own starter weapon and armor.");
            }
            catch (Exception exception)
            {
                errors.Add("D04 item catalog is invalid: " + exception.Message);
            }
        }

        private static void ValidateMovementDemo(List<GameObject> objects, List<string> errors)
        {
            MovementDemoComposition[] compositions = objects
                .SelectMany(gameObject => gameObject.GetComponents<MovementDemoComposition>())
                .ToArray();
            if (compositions.Length != 1)
            {
                errors.Add("Expedition must contain exactly one MovementDemoComposition; found " + compositions.Length + ".");
                return;
            }

            MovementDemoComposition composition = compositions[0];
            var compositionData = new SerializedObject(composition);
            GameObject playerPrefab = compositionData.FindProperty("_playerPrefab").objectReferenceValue as GameObject;
            Transform spawn = compositionData.FindProperty("_spawnPoint").objectReferenceValue as Transform;
            Collider2D roomBounds = compositionData.FindProperty("_roomBounds").objectReferenceValue as Collider2D;
            RoomCameraRig2D cameraRig = compositionData.FindProperty("_cameraRig").objectReferenceValue as RoomCameraRig2D;
            Text telemetry = compositionData.FindProperty("_telemetryText").objectReferenceValue as Text;
            Text courseText = compositionData.FindProperty("_courseText").objectReferenceValue as Text;
            Button returnButton = compositionData.FindProperty("_returnToMenuButton").objectReferenceValue as Button;

            if (playerPrefab == null || spawn == null || roomBounds == null || cameraRig == null ||
                telemetry == null || courseText == null || returnButton == null)
            {
                errors.Add("MovementDemoComposition must serialize its player, spawn, bounds, camera and HUD references.");
            }

            if (roomBounds == null || roomBounds.bounds.size.x < 20f || roomBounds.bounds.size.y < 11.25f)
            {
                errors.Add("Movement-demo room bounds must contain the full B06 camera view.");
            }

            if (playerPrefab == null || playerPrefab.GetComponent<PlayerMotor2D>() == null ||
                playerPrefab.GetComponent<PlayerVisualStatePresenter>() == null ||
                playerPrefab.GetComponent<Rigidbody2D>() == null ||
                playerPrefab.GetComponent<CapsuleCollider2D>() == null ||
                playerPrefab.GetComponent<SpriteRenderer>()?.sprite == null ||
                playerPrefab.GetComponent<Animator>()?.runtimeAnimatorController == null)
            {
                errors.Add("Movement-demo player prefab must include its motor, sprite, collider, Animator and visual presenter.");
            }
            else if (!Mathf.Approximately(playerPrefab.GetComponent<SpriteRenderer>().sprite.pixelsPerUnit, 16f))
            {
                errors.Add("Movement-demo prototype sprite must use 16 PPU.");
            }

            string[] barriers = { "JumpBarrierOne", "JumpBarrierTwo", "JumpBarrierThree" };
            foreach (string barrierName in barriers)
            {
                GameObject barrier = objects.FirstOrDefault(gameObject => gameObject.name == barrierName);
                BoxCollider2D collider = barrier == null ? null : barrier.GetComponent<BoxCollider2D>();
                if (collider == null || collider.isTrigger || collider.attachedRigidbody != null ||
                    barrier.layer != LayerMask.NameToLayer("Ground"))
                {
                    errors.Add(barrierName + " must be a static solid obstacle on the Ground layer.");
                }
            }

            MovementDemoGoalTrigger goal = objects.SelectMany(gameObject => gameObject.GetComponents<MovementDemoGoalTrigger>())
                .FirstOrDefault();
            if (goal == null || goal.Composition != composition || goal.GetComponent<Collider2D>()?.isTrigger != true)
            {
                errors.Add("Movement-demo portal must forward its real trigger contact to the scene composition.");
            }

            if (objects.Count(gameObject => gameObject.GetComponent<EventSystem>() != null) != 1 ||
                objects.Count(gameObject => gameObject.GetComponent<InputSystemUIInputModule>() != null) != 1)
            {
                errors.Add("Expedition HUD must contain one EventSystem using InputSystemUIInputModule.");
            }
        }

        private static void ValidateExpeditionCamera(List<GameObject> objects, List<string> errors)
        {
            List<GameObject> cameras = objects.Where(gameObject =>
                gameObject.name == "Main Camera" && gameObject.GetComponent<Camera>() != null).ToList();
            if (cameras.Count != 1)
            {
                errors.Add("Expedition must contain exactly one Main Camera; found " + cameras.Count + ".");
                return;
            }

            GameObject cameraObject = cameras[0];
            Camera camera = cameraObject.GetComponent<Camera>();
            PixelPerfectCamera pixelPerfect = cameraObject.GetComponent<PixelPerfectCamera>();
            if (cameraObject.GetComponent<AudioListener>() == null)
            {
                errors.Add("Expedition Main Camera must contain one AudioListener.");
            }
            if (cameraObject.GetComponent<CinemachineBrain>() == null)
            {
                errors.Add("Expedition Main Camera must contain a CinemachineBrain.");
            }
            if (cameraObject.GetComponent<RoomCameraRig2D>() == null)
            {
                errors.Add("Expedition Main Camera must contain RoomCameraRig2D.");
            }
            if (pixelPerfect == null || pixelPerfect.assetsPPU != RoomCameraRig2D.AssetsPixelsPerUnit ||
                pixelPerfect.refResolutionX != RoomCameraRig2D.ReferenceWidth ||
                pixelPerfect.refResolutionY != RoomCameraRig2D.ReferenceHeight ||
                pixelPerfect.gridSnapping != PixelPerfectCamera.GridSnapping.UpscaleRenderTexture ||
                pixelPerfect.cropFrame != PixelPerfectCamera.CropFrame.Windowbox)
            {
                errors.Add("Expedition must use the 320×180, 16 PPU Windowbox/upscale Pixel Perfect contract.");
            }

            CinemachineCamera virtualCamera = cameraObject.GetComponentInChildren<CinemachineCamera>(true);
            CinemachineFollow follow = virtualCamera == null ? null : virtualCamera.GetComponent<CinemachineFollow>();
            CinemachineConfiner2D confiner = virtualCamera == null ? null : virtualCamera.GetComponent<CinemachineConfiner2D>();
            if (virtualCamera == null || follow == null || confiner == null)
            {
                errors.Add("Expedition requires one CinemachineCamera with Follow and Confiner2D components.");
                return;
            }

            Vector3 damping = follow.TrackerSettings.PositionDamping;
            if (!Mathf.Approximately(damping.x, RoomCameraRig2D.HorizontalDamping) ||
                !Mathf.Approximately(damping.y, RoomCameraRig2D.VerticalDamping) ||
                !Mathf.Approximately(camera.orthographicSize, RoomCameraRig2D.OrthographicSize))
            {
                errors.Add("Expedition camera damping and orthographic size must match the B06 contract.");
            }
        }

        private static void ValidatePlayerVisualController(List<string> errors)
        {
            UnityEditor.Animations.AnimatorController controller =
                AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(PlayerVisualControllerPath);
            if (controller == null)
            {
                errors.Add("Missing PlayerVisualStates Animator Controller at " + PlayerVisualControllerPath + ".");
                return;
            }

            var parameters = controller.parameters.ToDictionary(parameter => parameter.name, parameter => parameter.type);
            string[] requiredParameters = { "VisualState", "VelocityX", "VelocityY", "FacingX" };
            if (requiredParameters.Any(parameter => !parameters.ContainsKey(parameter)) ||
                parameters["VisualState"] != UnityEngine.AnimatorControllerParameterType.Int ||
                parameters["VelocityX"] != UnityEngine.AnimatorControllerParameterType.Float ||
                parameters["VelocityY"] != UnityEngine.AnimatorControllerParameterType.Float ||
                parameters["FacingX"] != UnityEngine.AnimatorControllerParameterType.Float)
            {
                errors.Add("PlayerVisualStates must expose VisualState (int) and VelocityX, VelocityY, FacingX (float).");
            }

            string[] requiredStates = { "Idle", "Moving", "Airborne", "Attacking", "Dodging", "Stunned", "Dead" };
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            string[] actualStates = stateMachine.states.Select(state => state.state.name).ToArray();
            if (requiredStates.Any(state => !actualStates.Contains(state)) || stateMachine.anyStateTransitions.Length != requiredStates.Length)
            {
                errors.Add("PlayerVisualStates must contain seven stable states and one VisualState transition per state.");
            }
        }

        private static void ValidateMenuText(
            List<GameObject> objects,
            string objectName,
            string expectedText,
            List<string> errors)
        {
            List<GameObject> matches = objects.Where(gameObject => gameObject.name == objectName).ToList();
            if (matches.Count != 1)
            {
                errors.Add("Menu must contain exactly one " + objectName + "; found " + matches.Count + ".");
                return;
            }

            Text text = matches[0].GetComponent<Text>();
            if (text == null || !string.Equals(text.text, expectedText, StringComparison.Ordinal))
            {
                errors.Add(objectName + " must display '" + expectedText + "'.");
            }
        }

        private static List<GameObject> GetHierarchyObjects(Scene scene)
        {
            var objects = new List<GameObject>();
            var pending = new Stack<Transform>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                pending.Push(root.transform);
            }

            while (pending.Count > 0)
            {
                Transform current = pending.Pop();
                objects.Add(current.gameObject);
                for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                {
                    pending.Push(current.GetChild(childIndex));
                }
            }

            return objects;
        }

        private static string ScenePath(string sceneName)
        {
            return "Assets/Luckbound/Scenes/" + sceneName + ".unity";
        }

        [Serializable]
        private sealed class AssemblyDefinition
        {
            public string name;
            public string[] references;
            public string[] includePlatforms;
            public bool noEngineReferences;
        }

        [Serializable]
        private sealed class InputActionsDefinition
        {
            public InputActionMapDefinition[] maps;
        }

        [Serializable]
        private sealed class InputActionMapDefinition
        {
            public string name;
            public InputActionDefinition[] actions;
            public InputActionBindingDefinition[] bindings;
        }

        [Serializable]
        private sealed class InputActionDefinition
        {
            public string name;
            public string type;
            public string expectedControlType;
        }

        [Serializable]
        private sealed class InputActionBindingDefinition
        {
            public string action;
            public string path;
            public string groups;
        }
    }
}
