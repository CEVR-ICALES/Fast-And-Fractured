using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
[CustomEditor(typeof(TrafficLightBehaviour),false)]
[CanEditMultipleObjects]

public class TrafficLightEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Update light state"))
        {
            ((TrafficLightBehaviour)target).UpdateLights();
        }
    }
}
