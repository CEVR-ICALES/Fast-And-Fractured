using UnityEngine;

/// <summary>
/// Put this script on your _GameManager gameObject to have it draw gizmos for DestroyIt features (like cling points, support 
/// points, and structural joints). NOTE: This script is designed to only run in Editor mode, for performance reasons.
/// </summary>
namespace DestroyIt
{
    public class EditorGizmos : MonoBehaviour
    {
        public bool showJointAnchors = true;

        #if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            // Checks
            if (Application.isPlaying) return;

            // JOINT ANCHORS
            if (showJointAnchors)
            {
                Joint[] joints = Object.FindObjectsOfType<Joint>();
                if (joints.Length > 0)
                {
                    foreach (Joint jnt in joints)
                    {
                        Vector3 pos = jnt.transform.TransformPoint(jnt.anchor);
                        Gizmos.DrawWireSphere(pos, 0.05f);
                    }
                }
            }
        }
        #endif
    }
}