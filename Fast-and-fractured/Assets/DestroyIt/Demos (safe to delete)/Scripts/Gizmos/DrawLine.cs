using UnityEngine;

namespace DestroyIt
{
    public class DrawLine : MonoBehaviour
    {
        public bool isActive = true;

        void OnDrawGizmos()
        {
            if (isActive)
                Gizmos.DrawLine(transform.position, transform.position + transform.forward * 10f);
        }
    }
}