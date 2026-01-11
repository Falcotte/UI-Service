using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace AngryKoala.UI
{
    [CustomEditor(typeof(ScreenRegistry))]
    public sealed class ScreenRegistryEditor : Editor
    {
        private ReorderableList _reorderableList;

        private readonly List<string> _duplicateKeys = new();

        private void OnEnable()
        {
            SerializedProperty registrationsProperty = serializedObject.FindProperty("_registrations");

            if (registrationsProperty == null)
            {
                return;
            }

            _reorderableList = new ReorderableList(serializedObject, registrationsProperty, true, true, true, true)
            {
                drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "Screen Registrations"); },
                drawElementCallback = (rect, index, isActive, isFocused) =>
                {
                    SerializedProperty element = registrationsProperty.GetArrayElementAtIndex(index);
                    SerializedProperty keyProperty = element.FindPropertyRelative(nameof(ScreenRegistration.Key));
                    SerializedProperty addressProperty =
                        element.FindPropertyRelative(nameof(ScreenRegistration.Address));

                    float halfWidth = rect.width * 0.5f;
                    Rect keyRect = new Rect(rect.x, rect.y + 2f, halfWidth - 6f, EditorGUIUtility.singleLineHeight);
                    Rect addressRect = new Rect(rect.x + halfWidth + 6f, rect.y + 2f, halfWidth - 6f,
                        EditorGUIUtility.singleLineHeight);

                    EditorGUI.PropertyField(keyRect, keyProperty, GUIContent.none);
                    EditorGUI.PropertyField(addressRect, addressProperty, GUIContent.none);
                }
            };

            _reorderableList.elementHeight = EditorGUIUtility.singleLineHeight + 6f;
        }

        public override void OnInspectorGUI()
        {
            if (_reorderableList == null)
            {
                return;
            }

            serializedObject.Update();
            _reorderableList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();

            DrawDuplicateKeyWarning();
        }

        private void DrawDuplicateKeyWarning()
        {
            _duplicateKeys.Clear();

            ScreenRegistry registry = (ScreenRegistry)target;
            IReadOnlyList<ScreenRegistration> registrations = registry.GetRegistrations();

            HashSet<string> seenKeys = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (ScreenRegistration registration in registrations)
            {
                if (string.IsNullOrWhiteSpace(registration.Key))
                {
                    continue;
                }

                if (!seenKeys.Add(registration.Key) && !_duplicateKeys.Contains(registration.Key))
                {
                    _duplicateKeys.Add(registration.Key);
                }
            }

            if (_duplicateKeys.Count > 0)
            {
                EditorGUILayout.Space();
                string joined = string.Join(", ", _duplicateKeys);

                EditorGUILayout.HelpBox($"Duplicate screen keys detected: {joined}", MessageType.Warning);
            }
        }
    }
}