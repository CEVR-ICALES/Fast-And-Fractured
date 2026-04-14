using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utilities
{
    public static class FileUtils
    {
        public static bool DeleteAsset(string assetPath)
        {
            string normalizedAssetPath = NormalizePath(assetPath);
            bool deleted = AssetDatabase.DeleteAsset(normalizedAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }
        public static bool DeleteFolder(string folderPath)
        {
            if (AssetDatabase.IsValidFolder(folderPath))
            {
                return DeleteAsset(folderPath);
            }
            return false;
        }
        public static bool DeleteAsset(string[] assetsPath)
        {
            List<string> outFailedPaths = new List<string>();
            bool deleted = AssetDatabase.DeleteAssets(assetsPath, outFailedPaths);
            foreach (string outFailedPath in outFailedPaths)
            {
                Debug.Log("File " + outFailedPath + " couldn't been deleted due to path not existing.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }

        public static void SaveAssets<T>(T[] objectsUnity, string folderPath, string extension) where T : UnityEngine.Object
        {
            int skinNum = 0;
            foreach (T objectUnity in objectsUnity)
            {
                if (objectUnity != null)
                {
                    objectUnity.name = skinNum + "_" + objectUnity.name;
                    string assetPath = Path.Combine(folderPath, objectUnity.name + extension);
                    string normalizedAssetPath = NormalizePath(assetPath);
                    T objectToSave = UnityEngine.Object.Instantiate(objectUnity);
                    AssetDatabase.CreateAsset(objectToSave, normalizedAssetPath);
                }
                skinNum++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static Material[] GetAllMaterialsFromFolder(string folderPath)
        {
            string normalizedFolderPath = NormalizePath(folderPath);
            return Resources.LoadAll<Material>(normalizedFolderPath);
        }

        public static bool DirectoryExist(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError("Folder " + directoryPath + " doesn't exist");
                return false;
            }
            return true;
        }
        public static void CheckAndCreateDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log("Folder " + folderPath + " created");
            }
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/');
        }
    }
}
