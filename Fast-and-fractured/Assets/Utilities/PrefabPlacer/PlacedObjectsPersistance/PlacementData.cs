using System;
using UnityEngine;

[Serializable]
public class PlacementData 
{
    public string UniqueID;
    public GameObject prefab; // in case i decide to add a re-instantiate or somethinbg like that
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 scale;
}
