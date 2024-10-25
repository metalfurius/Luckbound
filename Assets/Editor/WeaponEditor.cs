using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

[CustomEditor(typeof(Weapon))]
public class WeaponEditor : Editor
{
    private SerializedProperty _weaponName;
    private SerializedProperty _weaponGameObject;
    private SerializedProperty _attackAnimations;
    private ReorderableList _animationList;
    private int _elementToDelete = -1;

    private void OnEnable()
    {
        _weaponName = serializedObject.FindProperty("weaponName");
        _weaponGameObject = serializedObject.FindProperty("weaponGameObject");
        _attackAnimations = serializedObject.FindProperty("attackAnimations");

        // Initialize Reorder-ableList
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

                        var frameTimings = animation.FindPropertyRelative("frameTimings");
                        for (var j = 0; j < frameTimings.arraySize; j++)
                        {
                            var frameHeight = EditorGUIUtility.singleLineHeight * 3 + EditorGUIUtility.standardVerticalSpacing * 3;
                            var collider = animation.FindPropertyRelative("colliders").GetArrayElementAtIndex(j);
                            if (collider.objectReferenceValue == null)
                            {
                                frameHeight += EditorGUIUtility.singleLineHeight * 2;
                            }
                            height += frameHeight;
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

                    // Add left margin for the foldout arrow, separating it from the reorder icon
                    var foldoutRect = new Rect(rect.x + 20, rect.y, rect.width - 20, EditorGUIUtility.singleLineHeight);
                    animation.isExpanded = EditorGUI.Foldout(foldoutRect, animation.isExpanded, animationName);

                    if (animation.isExpanded)
                    {
                        EditorGUI.indentLevel++;

                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                        EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), animation.FindPropertyRelative("name"), new GUIContent("Animation Name"));
                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                        EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("sprites"), true)), animation.FindPropertyRelative("sprites"), new GUIContent("Sprites"), true);
                        rect.y += EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("sprites"), true) + EditorGUIUtility.standardVerticalSpacing;

                        EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("frameTimings"), true)), animation.FindPropertyRelative("frameTimings"), new GUIContent("Frame Timings"), true);
                        rect.y += EditorGUI.GetPropertyHeight(animation.FindPropertyRelative("frameTimings"), true) + EditorGUIUtility.standardVerticalSpacing;

                        var frameTimings = animation.FindPropertyRelative("frameTimings");
                        var colliders = animation.FindPropertyRelative("colliders");
                        var damagePerFrame = animation.FindPropertyRelative("damagePerFrame");

                        if (colliders.arraySize != frameTimings.arraySize)
                        {
                            colliders.arraySize = frameTimings.arraySize;
                        }
                        if (damagePerFrame.arraySize != frameTimings.arraySize)
                        {
                            damagePerFrame.arraySize = frameTimings.arraySize;
                        }

                        EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), "Colliders and Damage Per Frame", EditorStyles.boldLabel);
                        rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                        EditorGUI.indentLevel++;
                        for (var j = 0; j < frameTimings.arraySize; j++)
                        {
                            var collider = colliders.GetArrayElementAtIndex(j);

                            EditorGUI.LabelField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), $"Frame {j + 1}", EditorStyles.boldLabel);
                            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), collider, new GUIContent("Collider"));
                            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                            var damage = damagePerFrame.GetArrayElementAtIndex(j);
                            EditorGUI.PropertyField(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), damage, new GUIContent("Damage"));
                            rect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

                            if (collider.objectReferenceValue == null)
                            {
                                EditorGUI.HelpBox(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight * 2), "No collider for this frame", MessageType.Info);
                                rect.y += EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
                            }
                        }
                        EditorGUI.indentLevel--;

                        if (GUI.Button(new Rect(rect.x, rect.y, rect.width, EditorGUIUtility.singleLineHeight), "Remove Animation"))
                        {
                            _elementToDelete = index; // Mark this element for deletion
                        }

                        EditorGUI.indentLevel--;
                    }
                }
            };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_weaponName);
        EditorGUILayout.PropertyField(_weaponGameObject);

        _animationList.DoLayoutList();

        // Handle deletion after the list has been drawn
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