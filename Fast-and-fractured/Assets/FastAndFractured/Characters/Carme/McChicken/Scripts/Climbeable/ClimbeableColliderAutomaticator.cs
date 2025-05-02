using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class ClimbeableColliderAutomaticator : MonoBehaviour
{
    public Collider mainCollider;
    public BoxCollider landeableCollider;

    public Color gizmosColor;

    public float landeableColliderHeight = 0.03f;
    public float heightPositionOffset = 0.05f;

    [ContextMenu("Sync Colliders Positions")]
    public void SyncColliders()
    {

        float topBoundY = mainCollider.bounds.max.y;
        // local coordinates
        Vector3 topBoundLocal = transform.InverseTransformPoint(new Vector3(0f, topBoundY, 0f));
        float localY = topBoundLocal.y + heightPositionOffset;

        // Calcular tamaño y posición del detector
        Vector3 detectorSize = GetMainColliderSize();
        detectorSize.y = landeableColliderHeight;

        Vector3 detectorCenter = GetMainColliderCenter();
        detectorCenter.y = localY;

        // Aplicar cambios
        landeableCollider.size = detectorSize;
        landeableCollider.center = detectorCenter;
        
    }

    private Vector3 GetMainColliderSize()
    {
        switch(mainCollider)
        {
            case BoxCollider box:
                return new Vector3(box.size.x, 0f, box.size.z);

            default: return Vector3.zero;
        }
    }

    private Vector3 GetMainColliderCenter()
    {
        switch (mainCollider)
        {
            case BoxCollider box:
                return box.center;

            default: return Vector3.zero;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = gizmosColor;
        DrawColliderGizmo(landeableCollider);
    }

    private void DrawColliderGizmo(BoxCollider collider)
    {
        Gizmos.matrix = Matrix4x4.TRS(
                    transform.TransformPoint(collider.center),
                    transform.rotation,
                    transform.lossyScale
                );
        Gizmos.DrawCube(Vector3.zero, collider.size);
        Gizmos.matrix = Matrix4x4.identity;
    }
}
