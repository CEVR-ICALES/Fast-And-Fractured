#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Linq;

public static class PrefabProfileManager //handle file operations & data persistance
{
    public const string PROFILE_FOLDER_PATH = "Assets/Utilities/PrefabPlacer/PrefabProfiles";
    private const string SELECTED_PROFILE_KEY = "PrefabPlacer_SelectedProfile";

    public static void CreateProfile(string profileName)
    {
        EnsureDirectoryStructure();
        string fullPath = Path.Combine(PROFILE_FOLDER_PATH, profileName);

        if (!Directory.Exists(fullPath))
        {
            Directory.CreateDirectory(fullPath);

            // create and save the tracker SO
            var tracker = ScriptableObject.CreateInstance<ProfilePlacedObjectsTrackerSO>();
            AssetDatabase.CreateAsset(tracker, Path.Combine(fullPath, $"{profileName}_Tracker.asset"));
            AssetDatabase.SaveAssets();

            SaveSelectedProfile(profileName);
            AssetDatabase.Refresh();
        }
    }

    public static void DeleteProfile(string profileName)
    {
        string path = GetProfilePath(profileName);
        if (Directory.Exists(path))
        {
            Directory.Delete(path, true);
            File.Delete(path + ".meta");
            AssetDatabase.Refresh();

            if (GetSelectedProfile() == profileName)
            {
                ClearSelectedProfile();
            }
        }
    }

    public static ProfilePlacedObjectsTrackerSO GetTrackerForProfile(string profileName)
    {
        string path = Path.Combine(PROFILE_FOLDER_PATH, profileName, $"{profileName}_Tracker.asset");
        return AssetDatabase.LoadAssetAtPath<ProfilePlacedObjectsTrackerSO>(path);
    }

    public static string[] GetAllProfileNames()
    {
        EnsureDirectoryStructure();
        return Directory.GetDirectories(PROFILE_FOLDER_PATH)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToArray();
    }

    public static List<GameObject> GetPrefabsForProfile(string profileName)
    {
        string path = GetPrefabListPath(profileName);
        if (!File.Exists(path)) return new List<GameObject>();

        return File.ReadAllLines(path)
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
            .Where(prefab => prefab != null)
            .ToList();
    }

    public static void AddPrefabToProfile(string profileName, string prefabPath)
    {
        string filePath = GetPrefabListPath(profileName);
        File.AppendAllText(filePath, prefabPath + "\n");
        AssetDatabase.Refresh();
    }

    public static void RemovePrefabFromProfile(string profileName, string prefabPath)
    {
        string filePath = GetPrefabListPath(profileName);
        var lines = File.ReadAllLines(filePath).ToList();
        lines.Remove(prefabPath);
        File.WriteAllLines(filePath, lines);
        AssetDatabase.Refresh();
    }

    public static string GetSelectedProfile()
    {
        return EditorPrefs.GetString(SELECTED_PROFILE_KEY, "");
    }

    public static void SaveSelectedProfile(string profileName)
    {
        EditorPrefs.SetString(SELECTED_PROFILE_KEY, profileName);
    }

    private static void ClearSelectedProfile()
    {
        EditorPrefs.DeleteKey(SELECTED_PROFILE_KEY);
    }

    public static string GetProfilePath(string profileName)
    {
        return Path.Combine(PROFILE_FOLDER_PATH, profileName);
    }

    private static string GetPrefabListPath(string profileName)
    {
        return Path.Combine(GetProfilePath(profileName), "prefabs.txt");
    }

    private static void EnsureDirectoryStructure()
    {
        if (!Directory.Exists(PROFILE_FOLDER_PATH))
        {
            Directory.CreateDirectory(PROFILE_FOLDER_PATH);
            AssetDatabase.Refresh();
        }
    }

    public static bool ChangeProfileName(string oldName, string newName)
    {
        if (string.IsNullOrEmpty(oldName)) return false;
        if (string.IsNullOrEmpty(newName)) return false;
        if (oldName == newName) return true; //no change needed

        string oldPath = GetProfilePath(oldName);
        string newPath = GetProfilePath(newName);

        //check if new name already exists
        if (Directory.Exists(newPath))
        {
            EditorUtility.DisplayDialog("Error", $"A profile named '{newName}' already exists.", "OK");
            return false;
        }

        //rename directory
        try
        {
            Directory.Move(oldPath, newPath);
            string oldMetaPath = oldPath + ".meta";
            string newMetaPath = newPath + ".meta"; //rename the meta file
            if (File.Exists(oldMetaPath))
            {
                File.Move(oldMetaPath, newMetaPath);
            }

            AssetDatabase.Refresh();


            //uipdate selected profile if it was renamed
            if (GetSelectedProfile() == oldName)
            {
                SaveSelectedProfile(newName);
            }

            return true;
        }
        catch (System.Exception e)
        {
            EditorUtility.DisplayDialog("Error", $"Failed to rename profile: {e.Message}", "OK");
            return false;
        }
    }
}
#endif
