using UnityEditor;
using UnityEngine;

namespace AngryKoala.UI
{
    [CustomEditor(typeof(Button))]
    public class ButtonEditor : Editor
    {
        private Button _button;

        private SerializedProperty _buttonVisualProperty;

        private SerializedProperty _animateOnClickProperty;

        private SerializedProperty _pressedMoveByProperty;
        private SerializedProperty _pressedRotateByProperty;
        private SerializedProperty _pressedScaleByProperty;

        private SerializedProperty _pressDurationProperty;
        private SerializedProperty _releaseDurationProperty;

        private SerializedProperty _pressEaseProperty;
        private SerializedProperty _releaseEaseProperty;

        private SerializedProperty _allowMultipleClicksProperty;
        private SerializedProperty _disableDurationProperty;

        private SerializedProperty _disableAfterClickProperty;

        private SerializedProperty _onPointerDownEventProperty;
        private SerializedProperty _onPointerUpEventProperty;
        private SerializedProperty _onClickEventProperty;

        private void OnEnable()
        {
            _button = (Button)target;

            SetSerializedProperties();
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(_buttonVisualProperty);

            DrawAnimateOnClickSection();

            DrawMultipleClicksSection();

            EditorGUILayout.PropertyField(_disableAfterClickProperty);

            DrawEvents();

            DrawButtonControls();

            serializedObject.ApplyModifiedProperties();
        }

        private void SetSerializedProperties()
        {
            if (_buttonVisualProperty != null)
                return;

            _buttonVisualProperty = serializedObject.FindProperty("_buttonVisual");

            _animateOnClickProperty = serializedObject.FindProperty("_animateOnClick");

            _pressedMoveByProperty = serializedObject.FindProperty("_pressedMoveBy");
            _pressedRotateByProperty = serializedObject.FindProperty("_pressedRotateBy");
            _pressedScaleByProperty = serializedObject.FindProperty("_pressedScaleBy");

            _pressDurationProperty = serializedObject.FindProperty("_pressDuration");
            _releaseDurationProperty = serializedObject.FindProperty("_releaseDuration");

            _pressEaseProperty = serializedObject.FindProperty("_pressEase");
            _releaseEaseProperty = serializedObject.FindProperty("_releaseEase");

            _allowMultipleClicksProperty = serializedObject.FindProperty("_allowMultipleClicks");
            _disableDurationProperty = serializedObject.FindProperty("_disableDuration");

            _disableAfterClickProperty = serializedObject.FindProperty("_disableAfterClick");

            _onPointerDownEventProperty = serializedObject.FindProperty("OnPointerDownEvent");
            _onPointerUpEventProperty = serializedObject.FindProperty("OnPointerUpEvent");
            _onClickEventProperty = serializedObject.FindProperty("OnClickEvent");
        }

        private void DrawAnimateOnClickSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.PropertyField(_animateOnClickProperty);

            if (!_animateOnClickProperty.boolValue)
            {
                EditorGUILayout.EndVertical();
                return;
            }
            
            EditorGUILayout.PropertyField(_pressedMoveByProperty);
            EditorGUILayout.PropertyField(_pressedRotateByProperty);
            EditorGUILayout.PropertyField(_pressedScaleByProperty);

            EditorGUILayout.PropertyField(_pressDurationProperty);
            EditorGUILayout.PropertyField(_releaseDurationProperty);

            EditorGUILayout.PropertyField(_pressEaseProperty);
            EditorGUILayout.PropertyField(_releaseEaseProperty);

            EditorGUILayout.EndVertical();
        }

        private void DrawMultipleClicksSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.PropertyField(_allowMultipleClicksProperty);
            
            if (_allowMultipleClicksProperty.boolValue)
            {
                EditorGUILayout.EndVertical();
                return;
            }

            float newValue = EditorGUILayout.FloatField(new GUIContent("Disable Duration"),
                _disableDurationProperty.floatValue);
            _disableDurationProperty.floatValue = Mathf.Max(0f, newValue);
            
            EditorGUILayout.EndVertical();
        }

        private void DrawEvents()
        {
            EditorGUILayout.PropertyField(_onPointerDownEventProperty, new GUIContent("OnPointerDown"));
            EditorGUILayout.PropertyField(_onPointerUpEventProperty, new GUIContent("OnPointerUp"));
            EditorGUILayout.PropertyField(_onClickEventProperty, new GUIContent("OnClick"));
        }

        private void DrawButtonControls()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Enable"))
            {
                _button.EnableButton();
                EditorUtility.SetDirty(_button);
            }

            if (GUILayout.Button("Disable"))
            {
                _button.DisableButton();
                EditorUtility.SetDirty(_button);
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Set Initial Transform Values"))
            {
                Undo.RecordObject(_button, "Set Initial Transform Values");
                _button.SetInitialTransformValues();
                EditorUtility.SetDirty(_button);
            }

            if (GUILayout.Button("Reset Transform Values"))
            {
                Undo.RecordObject(_button, "Reset Transform Values");
                _button.ResetTransformValues();
                EditorUtility.SetDirty(_button);
            }

            EditorGUILayout.EndVertical();
        }
    }
}