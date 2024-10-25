using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Weapon))]
public class WeaponEditor : Editor
{
    private SerializedProperty _weaponName;
    private SerializedProperty _weaponGameObject;
    private SerializedProperty _attackAnimations;

    private void OnEnable()
    {
        _weaponName = serializedObject.FindProperty("weaponName");
        _weaponGameObject = serializedObject.FindProperty("weaponGameObject");
        _attackAnimations = serializedObject.FindProperty("attackAnimations");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_weaponName);
        EditorGUILayout.PropertyField(_weaponGameObject);

        EditorGUILayout.LabelField("Attack Animations", EditorStyles.boldLabel);

        for (var i = 0; i < _attackAnimations.arraySize; i++)
        {
            var animation = _attackAnimations.GetArrayElementAtIndex(i);
            var sprites = animation.FindPropertyRelative("sprites");
            var frameTimings = animation.FindPropertyRelative("frameTimings");
            var colliders = animation.FindPropertyRelative("colliders");
            var damagePerFrame = animation.FindPropertyRelative("damagePerFrame");

            var animationName = animation.FindPropertyRelative("name").stringValue;
            if (string.IsNullOrEmpty(animationName))
            {
                animationName = $"Animation {i + 1}"; 
            }
            animation.isExpanded = EditorGUILayout.Foldout(animation.isExpanded, animationName);

            if (animation.isExpanded)
            {
                EditorGUILayout.BeginVertical("box");

                // Add a text field to edit the animation name
                EditorGUILayout.PropertyField(animation.FindPropertyRelative("name"), new GUIContent("Animation Name")); 

                EditorGUILayout.PropertyField(sprites, new GUIContent("Sprites"), true);
                EditorGUILayout.PropertyField(frameTimings, new GUIContent("Frame Timings"), true);

                // Ensure colliders and damagePerFrame arrays have the same size as frameTimings
                if (colliders.arraySize != frameTimings.arraySize)
                {
                    colliders.arraySize = frameTimings.arraySize;
                }
                if (damagePerFrame.arraySize != frameTimings.arraySize)
                {
                    damagePerFrame.arraySize = frameTimings.arraySize;
                }

                EditorGUILayout.LabelField("Colliders and Damage Per Frame", EditorStyles.boldLabel);

                for (var j = 0; j < frameTimings.arraySize; j++)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField($"Frame {j + 1}", EditorStyles.boldLabel);

                    var collider = colliders.GetArrayElementAtIndex(j);
                    var damage = damagePerFrame.GetArrayElementAtIndex(j);

                    EditorGUILayout.PropertyField(collider, new GUIContent("Collider"));
                    EditorGUILayout.PropertyField(damage, new GUIContent("Damage"));

                    if (collider.objectReferenceValue == null)
                    {
                        EditorGUILayout.HelpBox("No collider for this frame", MessageType.Info);
                    }

                    EditorGUILayout.EndVertical();
                }

                EditorGUILayout.EndVertical();
            }
        }

        if (GUILayout.Button("Add Animation"))
        {
            _attackAnimations.InsertArrayElementAtIndex(_attackAnimations.arraySize);
        }

        if (GUILayout.Button("Remove Last Animation"))
        {
            if (_attackAnimations.arraySize > 0)
            {
                _attackAnimations.DeleteArrayElementAtIndex(_attackAnimations.arraySize - 1);
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}