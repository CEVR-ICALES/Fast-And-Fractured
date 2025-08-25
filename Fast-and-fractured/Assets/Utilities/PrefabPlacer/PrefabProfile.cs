using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PrefabProfile
{
    public string profileName;
    public List<string> prefabPaths = new List<string>();

    public PrefabProfile(string name)
    {
        profileName = name;
    }

    public void ChangePrefabProfileName(string newName)
    {
        if (!string.IsNullOrEmpty(newName))
        {
            profileName = newName;
        }
    }
}
