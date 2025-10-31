#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;
using Unity.Mathematics;

public class PrefabModeMultiplePlacer : MonoBehaviour, IPrefabPlacerMode
{
    private ProfilePlacedObjectsTrackerSO activeTrackerSO;
    private bool isPlacing; 
    private List<GameObject> targetPrefabsList = new List<GameObject>();
    public float placingRadius;
    public float density;
    public Vector2 scaleValues = Vector2.one;
    public float placingSeparation; // used to generate an area on each spawned object to not overlap other spawn pouints

    private const int MAX_PLACEMENT_TRIES = 20;

    public void OnModeActivated(ProfilePlacedObjectsTrackerSO trackerSO)
    {
        activeTrackerSO = trackerSO;
        isPlacing = true;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public void OnModeDeactivated()
    {
        isPlacing = false;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public void StartPlacing(List<GameObject> prefabs, ProfilePlacedObjectsTrackerSO trackerSO)
    {
        if (prefabs == null) return;

        activeTrackerSO = trackerSO;
        isPlacing = true;
        targetPrefabsList.Clear();
        targetPrefabsList = prefabs;
        SceneView.duringSceneGui += OnSceneGUI;
    }

    public void StopPlacing()
    {
        isPlacing = false;
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public void UpdatePrefabSelection(List<GameObject> prefabs, ProfilePlacedObjectsTrackerSO trackerSO)
    {
        if(prefabs == null)
        {
            targetPrefabsList.Clear();
            return;
        }
        activeTrackerSO = trackerSO; 
        targetPrefabsList = prefabs;
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if (!isPlacing) return;

        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Handles.color = new Color(0, 1, 0, 0.1f); 
            Handles.DrawSolidDisc(hit.point, hit.normal, placingRadius);
            Handles.color = Color.green;
            Handles.DrawWireDisc(hit.point, hit.normal, placingRadius);

 
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                PlaceObjects(hit.point, hit.normal);
                e.Use(); 
            }
        }

        sceneView.Repaint();
    }

    private void PlaceObjects(Vector3 centerPosition, Vector3 surfaceNormal)
    {
        if (activeTrackerSO == null || targetPrefabsList == null || targetPrefabsList.Count == 0)
        {
            Debug.LogWarning("No selected prefabs to place.");
            return;
        }

        // decide how many objects are goiong to be instantiated
        int objectCount = Mathf.RoundToInt(density * placingRadius / 5f);
        List<Vector3> placedPositions = new List<Vector3>();

        for (int i = 0; i < objectCount; i++)
        {
            Vector3 spawnPosition = Vector3.zero;
            int currentTries = 0;
            bool positionFound = false;
            Vector3 localNormal = Vector3.zero;

            // attempt to find a valid position 
            while (currentTries < MAX_PLACEMENT_TRIES)
            {
                currentTries++;

                // generate a random point within the placement circle
                Vector2 randomPointInCircle = UnityEngine.Random.insideUnitCircle * placingRadius;
                Vector3 potentialPosition = centerPosition + new Vector3(randomPointInCircle.x, 0, randomPointInCircle.y);

                //find the actual ground height at that point
                if (Physics.Raycast(potentialPosition + surfaceNormal * 5f, -surfaceNormal, out RaycastHit placementHit))
                {
                    potentialPosition = placementHit.point;

                    //check if this new spot is too close to others placed in this same burst.
                    bool isOverlapping = false;
                    foreach (Vector3 pos in placedPositions)
                    {
                        if (Vector3.Distance(potentialPosition, pos) < placingSeparation)
                        {
                            isOverlapping = true;
                            break; 
                        }
                    }

                
                    if (!isOverlapping)
                    {
                        spawnPosition = potentialPosition;
                        localNormal = placementHit.normal;
                        positionFound = true;
                        break; 
                    }
                }
            }

            // if after all tries we couldnt find a valid spot skip this object.
            if (!positionFound)
            {
                continue;
            }

            
            placedPositions.Add(spawnPosition);

           
            Quaternion surfaceAlignment = Quaternion.FromToRotation(Vector3.up, localNormal);
            Quaternion randomSpin = Quaternion.AngleAxis(UnityEngine.Random.Range(0f, 360f), Vector3.up);
            Quaternion spawnRotation = surfaceAlignment * randomSpin;

            GameObject prefabToPlace = targetPrefabsList[UnityEngine.Random.Range(0, targetPrefabsList.Count)];
            GameObject newObject = (GameObject)PrefabUtility.InstantiatePrefab(prefabToPlace);

            newObject.transform.position = spawnPosition;
            newObject.transform.rotation = spawnRotation;
            newObject.transform.localScale = Vector3.one * UnityEngine.Random.Range(scaleValues.x, scaleValues.y);

            Undo.RegisterCreatedObjectUndo(newObject, "Place Multiple Prefabs");
            PrefabPlacerObjectMarker marker = newObject.AddComponent<PrefabPlacerObjectMarker>();
            marker.trackingSO = activeTrackerSO;
            marker.uniqueID = Guid.NewGuid().ToString();
            activeTrackerSO.AddPlacement(new PlacementData
            {
                UniqueID = marker.uniqueID,
                prefab = newObject,
                position = newObject.transform.position,
                rotation = newObject.transform.rotation,
                scale = newObject.transform.localScale
            });
            EditorUtility.SetDirty(activeTrackerSO);
        }
        //StopPlacing();
    }
}
#endif
