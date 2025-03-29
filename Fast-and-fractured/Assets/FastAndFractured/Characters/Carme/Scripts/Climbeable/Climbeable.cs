using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Climbeable : MonoBehaviour
{
    public BoxCollider colliderSurface;

    public Vector3 GetLandingPoint(Vector3 initialPosotion)
    {
        return colliderSurface.ClosestPoint(initialPosotion);
    }


}
