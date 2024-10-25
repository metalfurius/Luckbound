using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(Weapon))]
public class WeaponEditor : Editor
{
    private SerializedProperty _weaponName;
    private SerializedProperty _weaponGameObject;
    private SerializedProperty _attackAnimations;
    private ReorderableList _animationList;
    private int _elementToDelete = -1;
    private Dictionary<int, string[]> _colliderNames = new Dictionary<int, string[]>();
    private Dictionary<int, Collider2D[]> _colliderComponents = new Dictionary<int, Collider2D[]>();

    private void OnEnable()
    {
        _weaponName = serializedObject.FindProperty("weaponName");
        _weaponGameObject = serializedObject.FindProperty("weaponGameObject");
        _attackAnimations = serializedObject.FindProperty("attackAnimations");

        UpdateColliderCache();

        // Initialize ReorderableList
        _animationList = new ReorderableList(serializedObject, _attackAnimations, true, true, true, true)
        {
            drawHeaderCallback = rect => {
                EditorGUI.LabelField(rect, "Attack Animations", EditorStyles.boldLabel);
            },
            elementHeightCallback = index => {
                var animation = _attackAnimations.GetArrayElementAtIndex(index);
                var height = EditorGUIUtility.singleLineHeight + 5;

                if (animation.isExpanded)
                {
                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    height += EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("sprites"), true);
                    height += EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("frameTimings"), true);
                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    var collidersExpanded = animation.FindPropertyRelative("collidersExpanded");
                    if (collidersExpanded.boolValue)
                    {
                        var frameTimings = animation.FindPropertyRelative("frameTimings");
                        for (var j = 0; j < frameTimings.arraySize; j++)
                        {
                            var frameHeight = EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 3;
                            height += frameHeight;
                        }
                    }

                    height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2;
                }

                return height;
            },
            drawElementCallback = (rect, index, _, _) => {
                var animation = _attackAnimations.GetArrayElementAtIndex(index);
                var animationName = animation.FindPropertyRelative("name").stringValue;
                if (string.IsNullOrEmpty(animationName))
                {
                    animationName = $"Animation {index + 1}";
                }

                rect.y += 2;

                var foldoutRect = new Rect(rect.x + 20, rect.y, rect.width - 20, EditorGUIUtility.singleLineHeight);
                animation.isExpanded = EditorGUI.Foldout(foldoutRect, animation.isExpanded, animationName);

                if (animation.isExpanded)
                {
                    EditorGUI.indentLevel++;

                    rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
                        animation.FindPropertyRelative("name"), new GUIContent("Animation Name"));
                    rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, 
                        EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("sprites"), true)), 
                        animation.FindPropertyRelative("sprites"), new GUIContent("Sprites"), true);
                    rect.y += EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("sprites"), true) + 
                        EditorGUIUtility.standardVerticalSpacing;

                    EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, 
                        EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("frameTimings"), true)), 
                        animation.FindPropertyRelative("frameTimings"), new GUIContent("Frame Timings"), true);
                    rect.y += EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("frameTimings"), true) + 
                        EditorGUIUtility.standardVerticalSpacing;

                    var frameTimings = animation.FindPropertyRelative("frameTimings");
                    var colliders = animation.FindPropertyRelative("colliders");
                    var damagePerFrame = animation.FindPropertyRelative("damagePerFrame");
                    var collidersExpanded = animation.FindPropertyRelative("collidersExpanded");

                    if (colliders.arraySize != frameTimings.arraySize)
                    {
                        colliders.arraySize = frameTimings.arraySize;
                    }
                    if (damagePerFrame.arraySize != frameTimings.arraySize)
                    {
                        damagePerFrame.arraySize = frameTimings.arraySize;
                    }

                    var headerRect = new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight);
                    EditorGUI.DrawRect(headerRect, new Color(0.1f, 0.1f, 0.1f, 0.1f));

                    var foldoutStyle = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold,
                        margin = new RectOffset(15, 0, 0, 0)
                    };

                    collidersExpanded.boolValue = EditorGUI.Foldout(
                        headerRect,
                        collidersExpanded.boolValue,
                        "Colliders and Damage Per Frame",
                        true,
                        foldoutStyle);
                    
                    rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                    if (collidersExpanded.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        for (var j = 0; j < frameTimings.arraySize; j++)
                        {
                            var collider = colliders.GetArrayElementAtIndex(j);

                            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
                                $"Frame {j + 1}", EditorStyles.boldLabel);
                            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                            // Draw collider popup
                            DrawColliderPopup(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
                                collider, index);
                            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                            var damage = damagePerFrame.GetArrayElementAtIndex(j);
                            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
                                damage, new GUIContent("Damage"));
                            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                        }
                        EditorGUI.indentLevel--;
                    }

                    if (GUI.Button(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), 
                        "Remove Animation"))
                    {
                        string animDisplayName = string.IsNullOrEmpty(animationName) ? 
                            $"Animation {index + 1}" : animationName;
                        if (EditorUtility.DisplayDialog(
                            "Confirm Removal",
                            $"Are you sure you want to remove '{animDisplayName}'? This action cannot be undone.",
                            "Remove",
                            "Cancel"))
                        {
                            _elementToDelete = index;
                        }
                    }

                    EditorGUI.indentLevel--;
                }
            }
        };
    }

    private void UpdateColliderCache()
    {
        _colliderNames.Clear();
        _colliderComponents.Clear();

        var weaponObj = _weaponGameObject.objectReferenceValue as GameObject;
        if (weaponObj != null)
        {
            // Aquí se realiza el cambio: se añade true como parámetro
            var colliders = weaponObj.GetComponentsInChildren<Collider2D>(true); 
            if (colliders.Length > 0)
            {
                var names = new string[colliders.Length + 1];
                names[0] = "None";
                for (int i = 0; i < colliders.Length; i++)
                {
                    names[i + 1] = GetColliderPath(colliders[i]);
                }

                _colliderNames[0] = names;
                _colliderComponents[0] = colliders;
            }
        }
    }

    private string GetColliderPath(Collider2D collider)
    {
        string path = collider.gameObject.name;
        Transform parent = collider.transform.parent;
        while (parent != null && parent.gameObject != _weaponGameObject.objectReferenceValue)
        {
            path = $"{parent.gameObject.name}/{path}";
            parent = parent.parent;
        }
        return path;
    }

    private void DrawColliderPopup(Rect position, SerializedProperty colliderProp, int index)
    {
        if (!_colliderNames.ContainsKey(index))
        {
            UpdateColliderCache();
        }

        var names = _colliderNames.GetValueOrDefault(0, new[] { "None" });
        var colliders = _colliderComponents.GetValueOrDefault(0, new Collider2D[0]);

        // Find current index
        int currentIndex = 0;
        if (colliderProp.objectReferenceValue != null)
        {
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == colliderProp.objectReferenceValue)
                {
                    currentIndex = i + 1; // +1 because "None" is at index 0
                    break;
                }
            }
        }

        EditorGUI.BeginChangeCheck();
        int newIndex = EditorGUI.Popup(position, "Collider", currentIndex, names);
        if (EditorGUI.EndChangeCheck())
        {
            colliderProp.objectReferenceValue = newIndex == 0 ? null : colliders[newIndex - 1];
        }
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(_weaponName);
        EditorGUILayout.PropertyField(_weaponGameObject);
        if (EditorGUI.EndChangeCheck() && _weaponGameObject.objectReferenceValue != null)
        {
            UpdateColliderCache();
        }

        _animationList.DoLayoutList();

        if (_elementToDelete != -1)
        {
            _attackAnimations.DeleteArrayElementAtIndex(_elementToDelete);
            _elementToDelete = -1;
            GUI.changed = true;
        }

        if (GUILayout.Button("Add Animation"))
        {
            _attackAnimations.InsertArrayElementAtIndex(_attackAnimations.arraySize);
        }

        serializedObject.ApplyModifiedProperties();
    }
}