#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public class PrefabModeSinglePlacer : MonoBehaviour, IPrefabPlacerMode //handles scene interaction (user inputs by keyboard) and preview placement 
{
    private GameObject previewInstance;
    private GameObject targetPrefab;
    private bool isPlacing;
    private ProfilePlacedObjectsTrackerSO activeTrackerSO;
    public Vector2 scaleValues = Vector2.one;

    public void OnModeActivated(ProfilePlacedObjectsTrackerSO trackerSO)
    {
        activeTrackerSO = trackerSO;
        isPlacing = true;
    }

    public void OnModeDeactivated()
    {
        isPlacing = false;
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyPreviewInstance();
    }

    public void StartPlacing(GameObject prefab, ProfilePlacedObjectsTrackerSO trackerSO)
    {
        if (prefab == null) return;

        activeTrackerSO = trackerSO;
        isPlacing = true;
        targetPrefab = prefab;
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
        previewInstance = UnityEngine.Object.Instantiate(targetPrefab);
        previewInstance.hideFlags = HideFlags.HideAndDontSave;
    }

    private void DestroyPreviewInstance()
    {
        if (previewInstance != null)
            UnityEngine.Object.DestroyImmediate(previewInstance);
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPlacing) return;

        Event e = Event.current;
        if(!RenameProfileWindow.currenltyOnRenameWindow) //avoid refocus on sceneView when reanming a profile
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
            if(Event.current.keyCode == KeyCode.Escape)
            {
                StopPlacing();
                Event.current.Use(); //prevent unity from handling this event by other UI elements just for this frame
            }
        }
    }

    private void PlacePrefabInScene()
    {
        GameObject instance = PrefabUtility.InstantiatePrefab(targetPrefab) as GameObject;
        if(instance != null)
        {
            //ProfilePlacedObjectsTrackerSO trackerSO = //PrefabPlacerWindow.
            Undo.RegisterCreatedObjectUndo(instance, $"Placed prefab '{instance.name}' by prefab placer tool"); // register the action to the undo history
            instance.transform.SetPositionAndRotation(previewInstance.transform.position, previewInstance.transform.rotation);
            instance.transform.localScale = Vector3.one * UnityEngine.Random.Range(scaleValues.x, scaleValues.y);

            var marker = instance.AddComponent<PrefabPlacerObjectMarker>();
            marker.trackingSO = activeTrackerSO;
            marker.uniqueID = Guid.NewGuid().ToString();

            activeTrackerSO.AddPlacement(new PlacementData
            {
                UniqueID = marker.uniqueID,
                prefab = instance,
                position = instance.transform.position,
                rotation = instance.transform.rotation,
                scale = instance.transform.localScale
            });

            EditorUtility.SetDirty(activeTrackerSO);
        }
        //StopPlacing();
    }

    
}

#endif
