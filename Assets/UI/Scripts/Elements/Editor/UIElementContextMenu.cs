using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AngryKoala.UI
{
    public static class UIElementContextMenu
    {
        private const string _buttonPrefabAssetPath = "Assets/UI/Prefabs/Button.prefab";

        [MenuItem("GameObject/UI/Angry Koala/Button", false, 10)]
        private static void CreateButton(MenuCommand menuCommand)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_buttonPrefabAssetPath);
            if (prefab == null)
            {
                Debug.LogError($"Button prefab not found at path: {_buttonPrefabAssetPath}");
                return;
            }

            Transform parent = GetOrCreateUIParent(menuCommand);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "Button";

            GameObjectUtility.EnsureUniqueNameForSibling(instance);

            Undo.RegisterCreatedObjectUndo(instance, "Create Button");

            Selection.activeGameObject = instance;
        }

        [MenuItem("GameObject/UI/Angry Koala/Button", true)]
        private static bool ValidateCreateButton()
        {
            return !string.IsNullOrWhiteSpace(_buttonPrefabAssetPath);
        }

        private static Transform GetOrCreateUIParent(MenuCommand menuCommand)
        {
            Transform desiredParent = null;

            if (menuCommand.context is GameObject contextGameObject)
            {
                desiredParent = contextGameObject.transform;
            }
            else if (Selection.activeTransform != null)
            {
                desiredParent = Selection.activeTransform;
            }

            Canvas canvas = FindCanvasInParents(desiredParent);
            if (canvas == null)
            {
                canvas = FindCanvasInScene();
            }

            if (canvas == null)
            {
                canvas = CreateCanvas();
            }

            if (desiredParent != null && desiredParent.IsChildOf(canvas.transform))
            {
                return desiredParent;
            }

            return canvas.transform;
        }

        private static Canvas FindCanvasInParents(Transform transform)
        {
            if (transform == null)
            {
                return null;
            }

            return transform.GetComponentInParent<Canvas>(true);
        }

        private static Canvas FindCanvasInScene()
        {
            Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].isRootCanvas)
                {
                    return canvases[i];
                }
            }

            return canvases.Length > 0 ? canvases[0] : null;
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasGameObject =
                new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            Undo.RegisterCreatedObjectUndo(canvasGameObject, "Create Canvas");

            Canvas canvas = canvasGameObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            EnsureEventSystemExists();

            Selection.activeGameObject = canvasGameObject;

            return canvas;
        }

        private static void EnsureEventSystemExists()
        {
            EventSystem existingEventSystem = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Exclude);
            if (existingEventSystem != null)
            {
                return;
            }

            GameObject eventSystemGameObject =
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(eventSystemGameObject, "Create EventSystem");
        }
    }
}