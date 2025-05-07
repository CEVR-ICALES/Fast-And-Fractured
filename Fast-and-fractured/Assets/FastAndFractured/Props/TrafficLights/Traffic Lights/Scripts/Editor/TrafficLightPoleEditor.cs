using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(TrafficLightPole))]
public class TrafficLightPoleEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TrafficLightPole pole = (TrafficLightPole)target;

        GUILayout.Space(10);
        EditorGUILayout.LabelField("Editor Actions", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto-Collect Units & Lamps"))
        {
            Undo.RecordObject(pole, "Auto-Collect Traffic Units and Lamps");
            pole.Editor_CollectUnitsAndLamps();
            EditorUtility.SetDirty(pole);
        }

        if (GUILayout.Button("Apply Initial Pole Color"))
        {
            Undo.RecordObject(pole.poleRenderer != null ? pole.poleRenderer : pole, "Apply Initial Pole Color");
            pole.Editor_ApplyInitialPoleColor();
            if (pole.poleRenderer != null) EditorUtility.SetDirty(pole.poleRenderer);
            EditorUtility.SetDirty(pole);
        }

        if (GUILayout.Button("Apply Initial Light States (Preview)"))
        {
            List<UnityEngine.Object> objectsToRecord = new List<UnityEngine.Object>();
            objectsToRecord.Add(pole);
            foreach (var unit in pole.trafficUnits)
            {
                if (unit == null) continue;
                objectsToRecord.Add(unit);
                foreach (var lamp in unit.lamps)
                {
                    if (lamp == null || lamp.lampRenderer == null) continue;
                    objectsToRecord.Add(lamp.lampRenderer);
                }
            }
            Undo.RecordObjects(objectsToRecord.ToArray(), "Apply Initial Light States");

            pole.Editor_ApplyInitialLightStates();

            foreach (var obj in objectsToRecord)
            {
                if (obj != null) EditorUtility.SetDirty(obj);
            }
        }

        GUILayout.Space(10);
        if (Application.isPlaying)
        {
            if (GUILayout.Button(pole.activateOnStart ? "Deactivate System (Runtime)" : "Activate System (Runtime)"))
            {
                if (pole.activateOnStart)
                {
                    pole.DeactivateSystem();
                    pole.activateOnStart = false;
                }
                else
                {
                    pole.ActivateSystem();
                    pole.activateOnStart = true;
                }
            }
        }
    }
}