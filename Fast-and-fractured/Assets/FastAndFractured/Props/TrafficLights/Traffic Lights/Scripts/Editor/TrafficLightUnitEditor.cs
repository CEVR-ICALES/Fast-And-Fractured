using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(TrafficLightUnit))]
public class TrafficLightUnitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TrafficLightUnit unit = (TrafficLightUnit)target;

        GUILayout.Space(10);
        if (GUILayout.Button("Auto-Collect Lamps for this Unit"))
        {
            Undo.RecordObject(unit, "Auto-Collect Lamps for Unit");
            unit.Editor_CollectLamps();
            EditorUtility.SetDirty(unit);
        }
    }
}