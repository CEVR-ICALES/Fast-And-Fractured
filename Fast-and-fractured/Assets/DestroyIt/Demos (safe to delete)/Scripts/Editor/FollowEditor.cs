using UnityEditor;

namespace DestroyIt
{
    [CustomEditor(typeof(Follow))]
    public class FollowEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            Follow script = target as Follow;

            // FixedFromPosition and FixedDistance should only be available when FixedPosition is true.
            script.isPositionFixed = EditorGUILayout.Toggle("Is Fixed From Position", script.isPositionFixed);
            if (script.isPositionFixed)
            {
                script.fixedFromPosition = EditorGUILayout.Vector3Field("Position", script.fixedFromPosition);
                script.fixedDistance = EditorGUILayout.FloatField("Distance", script.fixedDistance);
            }
        }
    }
}
