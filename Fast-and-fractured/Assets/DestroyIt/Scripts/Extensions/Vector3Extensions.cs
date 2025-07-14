using UnityEngine;

namespace DestroyIt
{
    public static class Vector3Extensions
    {
        public static Vector3 LerpByDistance(this Vector3 startPoint, Vector3 endPoint, float distance)
        {
            Vector3 point = distance * Vector3.Normalize(endPoint - startPoint) + startPoint;
            return point;
        }

        public static Vector3 ClosestDirection(this Vector3 vector)
        {
            Vector3[] compass = new Vector3[]{Vector3.left, Vector3.right, Vector3.forward, Vector3.back, Vector3.up, Vector3.down};
            Vector3 closestDirection = Vector3.zero;
            float maxDot = -(Mathf.Infinity);
            
            foreach (Vector3 direction in compass) 
            {
                float dot = Vector3.Dot(vector, direction);
                if (dot > maxDot) {
                    closestDirection = direction;
                    maxDot = dot;
                }
            }

            return closestDirection;
        }
    }
}