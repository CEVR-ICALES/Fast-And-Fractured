using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Utilities;
[CustomEditor(typeof(TrafficLightLamp),false)]
[CanEditMultipleObjects]

public class TrafficLightEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Update light state"))
        {
           // ((TrafficLightLamp)target).UpdateLights();
        }
    }
}
