#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class PrefabModeEraser : MonoBehaviour , IPrefabPlacerMode
{
    private ProfilePlacedObjectsTrackerSO activeTrackerSO;
    public float eraserRadius;
    private bool isErasing;
    private bool doLogic = true;
    public void OnModeActivated(ProfilePlacedObjectsTrackerSO trackerSO)
    {
        activeTrackerSO = trackerSO;
        isErasing = true;
        doLogic = true;
    }

    public void OnModeDeactivated()
    {
        isErasing = false;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public void StartErasing(ProfilePlacedObjectsTrackerSO trackerSO)
    {
        activeTrackerSO = trackerSO;
        isErasing = true;
        doLogic = true;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public void StopErasing()
    {
        isErasing = false;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!doLogic) return;

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Handles.color = Color.red;
            Handles.DrawWireDisc(hit.point, hit.normal, eraserRadius);

            if (e.type == EventType.MouseDown && e.button == 0) // Left Mouse Button Click
            {
                EraseObjects(hit.point);
                e.Use(); // Mark the event as used to prevent propagation
            }
        }

        sceneView.Repaint();

        if(!isErasing)
            doLogic = false;
    }

    private void EraseObjects(Vector3 position)
    {
        if (activeTrackerSO == null) return;

        HashSet<GameObject> objectsToRemove = new HashSet<GameObject>();
        Collider[] hitColliders = Physics.OverlapSphere(position, eraserRadius);
        Bounds eraserBounds = new Bounds(position, new Vector3(eraserRadius, eraserRadius, eraserRadius));

        foreach (var hit in hitColliders)
        {
            if(hit.gameObject.TryGetComponent(out PrefabPlacerObjectMarker marker))
            {
                if(activeTrackerSO.ContainsByUniqueID(marker.uniqueID))
                {
                    objectsToRemove.Add(hit.gameObject);
                }
            }
        }

        foreach(var obj in objectsToRemove)
        {
            if(obj.TryGetComponent(out PrefabPlacerObjectMarker markerToRemove))
            {
                if(activeTrackerSO.ContainsByUniqueID(markerToRemove.uniqueID) && eraserBounds.Intersects(markerToRemove.GetBounds())) // double check if it is contained in the active trackerSO just in case, not really necessary
                {
                    activeTrackerSO.RemoveByUniqueID(markerToRemove.uniqueID);
                    Undo.DestroyObjectImmediate(obj);
                    EditorUtility.SetDirty(activeTrackerSO);
                }
            }
        }

       
    }
}
#endif
