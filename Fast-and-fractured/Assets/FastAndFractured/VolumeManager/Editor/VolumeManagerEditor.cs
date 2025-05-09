using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
[CustomEditor(typeof(VolumeManager))]
public class VolumeManagerEditor : Editor
{
    private int _selectedVolume = 0;
    VolumeManager volumeManager;

    private void OnEnable()
    {
        volumeManager = (VolumeManager)target;

    }
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Current Volume", EditorStyles.boldLabel);
        _selectedVolume = EditorGUILayout.Popup(_selectedVolume, volumeManager.GetVolumeNames().ToArray());
        if (GUILayout.Button("Update Current Volume"))
        {
            volumeManager.ChangeCurrentVolume(_selectedVolume);
            DirtyUpdate();
            Undo.RecordObject(volumeManager.gameObject, "Changed volume");
        }
        if (GUILayout.Button("Find all Global Volumes"))
        {
            var volumeList = FindObjectsOfType<Volume>(true);
            List<GameObject> volumesToAdd = new();
            foreach (Volume volume in volumeList)
            {
                if (volume != null && volume.isGlobal)
                {
                    volumesToAdd.Add(volume.transform.parent.gameObject);
                }
            }
            volumeManager.VolumeList = volumesToAdd;
            DirtyUpdate();
            Undo.RecordObject(volumeManager.gameObject, "Added volumes");
        }
    }
    void DirtyUpdate()
    {
        EditorUtility.SetDirty(volumeManager.gameObject);
        EditorUtility.SetDirty(volumeManager);
    }
}
