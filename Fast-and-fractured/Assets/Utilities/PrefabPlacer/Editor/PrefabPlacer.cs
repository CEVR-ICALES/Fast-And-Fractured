using UnityEditor;
using UnityEngine;

namespace Utilities
{
    public class PrefabPlacer //handles scene interaction (user inputs by keyboard) and preview placement 
    {
        private GameObject previewInstance;
        private GameObject targetPrefab;
        private bool isPlacing;

        public void StartPlacing(GameObject prefab)
        {
            if (prefab == null) return;

            targetPrefab = prefab;
            isPlacing = true;
            CreatePreviewInstance();
            SceneView.duringSceneGui += OnSceneGUI;
        }

        public void StopPlacing()
        {
            isPlacing = false;
            SceneView.duringSceneGui -= OnSceneGUI;
            DestroyPreviewInstance();
        }

        private void CreatePreviewInstance()
        {
            previewInstance = Object.Instantiate(targetPrefab);
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
        }

        private void DestroyPreviewInstance()
        {
            if (previewInstance != null)
                Object.DestroyImmediate(previewInstance);
        }

        private void OnSceneGUI(SceneView sceneView)
        {
            if (!isPlacing) return;

            Event e = Event.current;
            if (!RenameProfileWindow.currenltyOnRenameWindow)//avoid refocus on sceneView when reanming a profile
            {
                sceneView.Focus(); //focus to sceneView to ensure that esc works to cancel prefab placement 
            }
            UpdatePreviewPosition(e);
            HandleSceneViewInput(e);
            sceneView.Repaint();
        }

        private void UpdatePreviewPosition(Event e)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                previewInstance.transform.position = hit.point;
                previewInstance.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
        }

        private void HandleSceneViewInput(Event e)
        {
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlacePrefabInScene();
                e.Use();
            }

            if (Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Escape)
                {
                    StopPlacing();
                    Event.current.Use(); //prevent unity from handling this event by other UI elements just for this frame
                }
            }
        }

        private void PlacePrefabInScene()
        {
            GameObject instance = PrefabUtility.InstantiatePrefab(targetPrefab) as GameObject;
            instance.transform.SetPositionAndRotation(previewInstance.transform.position, previewInstance.transform.rotation);
            StopPlacing();
        }
    }

}
