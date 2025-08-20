using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "ProfilePlacedObjectsTracker", menuName = "Tools/PrefabPlacer/ProfilePlacedObjectsTracker")]
public class ProfilePlacedObjectsTrackerSO : ScriptableObject
{
    public List<PlacementData> placedPrefabs = new List<PlacementData>();

    public void AddPlacement(PlacementData data)
    {
        if(!placedPrefabs.Exists(p => p.UniqueID == data.UniqueID))
            placedPrefabs.Add(data);
    }

    public void RemoveByUniqueID(string id)
    {
        placedPrefabs.RemoveAll(p => p.UniqueID == id); // remove all just as a safeguard
    }

    public bool ContainsByUniqueID(string id)
    {
        return placedPrefabs.Exists(p => p.UniqueID == id);
    }

    public PrefabPlacerObjectMarker[] GetMarkersArray()
    {
        PrefabPlacerObjectMarker[] markers = new PrefabPlacerObjectMarker[placedPrefabs.Count];

        //for(int i = 0; i < placedPrefabs.Count; i++)
        //{
        //    markers[i] = placedPrefabs[i].prefab.GetComponent<PrefabPlacerObjectMarker>();
        //}

        return placedPrefabs.Where(obj => obj != null)
            .Select(obj => obj.prefab.GetComponent<PrefabPlacerObjectMarker>()).ToArray();
    }
}
