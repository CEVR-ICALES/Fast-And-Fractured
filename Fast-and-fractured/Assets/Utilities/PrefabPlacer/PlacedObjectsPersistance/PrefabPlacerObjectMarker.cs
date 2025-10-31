#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


[ExecuteInEditMode]
public class PrefabPlacerObjectMarker : MonoBehaviour
{
    public ProfilePlacedObjectsTrackerSO trackingSO;
    public string uniqueID;

    private const float BOUNDS_SPHERE_RADIUS = 0.3f;

    private void OnDestroy()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && trackingSO != null)
        {
            trackingSO.RemoveByUniqueID(uniqueID);
            EditorUtility.SetDirty(trackingSO); // save the SO so this actually persists
        }
    }

    public Bounds GetBounds()
    {
        return new Bounds(transform.position, new Vector3(BOUNDS_SPHERE_RADIUS, BOUNDS_SPHERE_RADIUS, BOUNDS_SPHERE_RADIUS));
    }
}

#endif