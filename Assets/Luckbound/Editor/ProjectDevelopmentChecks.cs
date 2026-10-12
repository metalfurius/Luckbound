using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Luckbound.App;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
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
        private const string WebGlProfilePath = "Assets/Settings/Build Profiles/WebGL.asset";
        private static readonly string[] StartupScenes = { "Bootstrap", "Menu", "Hub", "Expedition" };

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
            ValidateSimulationRate(errors);
            ValidateWebGlProfile(errors);
            ValidateAssemblies(errors);

            foreach (string sceneName in StartupScenes)
            {
                ValidateScene(sceneName, errors);
            }

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
                !References(presentation, "Luckbound.Content") || References(presentation, "Luckbound.Gameplay")))
            {
                errors.Add("Luckbound.Presentation must depend on Core and Content, never Gameplay.");
            }
            if (app != null && (!References(app, "Luckbound.Core") || !References(app, "Luckbound.Content") ||
                !References(app, "Luckbound.Gameplay") || !References(app, "Luckbound.Presentation")))
            {
                errors.Add("Luckbound.App must compose the Core, Content, Gameplay and Presentation assemblies.");
            }
            if (editor != null && (!References(editor, "Luckbound.App") ||
                editor.includePlatforms == null || !editor.includePlatforms.Contains("Editor")))
            {
                errors.Add("Luckbound.Editor must be Editor-only and reference Luckbound.App.");
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
