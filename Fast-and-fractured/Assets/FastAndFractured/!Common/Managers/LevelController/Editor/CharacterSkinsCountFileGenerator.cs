using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Enums;
namespace FastAndFractured {

    public class CharacterSkinsCountFileGenerator
    {
        private const string RESOURCES_FOLDER_PATH = "Assets/FastAndFractured/Resources";
        private const string LIST_OF_CHARACTER_SKINS = "/ListOfCharacterSkins.asset";
        private const string SKIN_PREFIX = "_";
        private const int SKIN_STARTING_INT = 1;
        private const string SKIN_CHARACTER_PREFAB_FOLDER = "Character";
        private const string SKIN_CHASSIS_PREFAB_FOLDER = "Chassis";
        private const string SKIN_WHEELS_PREFAB_FOLDER = "Wheels";

        private static string characterFolderPath = RESOURCES_FOLDER_PATH + "/" + LevelConstants.SKINS_LOADER_PATH;

        public class CharacterSkin
        {
            public Material[] CharacterMaterials = new Material[1];
            public Material[] ChasisMaterials = new Material[1];
            public Material[] WheelMaterials = new Material[1];
        }

        [MenuItem("Tools/GenerateCharacterSkinCountFile")]
        public static void GenerateCharacterSkinsCountFile()
        {
            if (!Directory.Exists(characterFolderPath))
            {
                Debug.LogError("Folder " + characterFolderPath + " doesn't exist");
                return;
            }
            string[] characters = Directory.GetDirectories(characterFolderPath, "*", SearchOption.TopDirectoryOnly);

            string[] charactersName = new string[characters.Length];
            int[] skinCount = new int[characters.Length];

            for (int i = 0; i < characters.Length; i++) 
            {
                charactersName[i] = Path.GetFileName(characters[i]);
                string[] subfolders = Directory.GetDirectories(characterFolderPath + "/" + charactersName[i], "*", SearchOption.TopDirectoryOnly);
                skinCount[i] = subfolders.Length;
            }
            ListOfCharactersSkins asset = ScriptableObject.CreateInstance<ListOfCharactersSkins>();
            asset.listOfCharacters = charactersName.ToList();
            asset.listOfCharactersSkinCount = skinCount.ToList();

            AssetDatabase.CreateAsset(asset, characterFolderPath + LIST_OF_CHARACTER_SKINS);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void CreateNewCharacterInCharacterSkinsFolder(string CharacterName)
        {
            if (!Directory.Exists(characterFolderPath))
            {
                Debug.LogError("Folder " + characterFolderPath + " doesn't exist");
                return;
            }
            AssetDatabase.CreateFolder(characterFolderPath,CharacterName);
        }

        public static string[] ReturnCharactersInCharacterSkinsFolder()
        {
            if (!Directory.Exists(characterFolderPath))
            {
                Debug.LogError("Folder " + characterFolderPath + " doesn't exist");
                return null;
            }
            string[] charactersFolder = Directory.GetDirectories(characterFolderPath, "*", SearchOption.TopDirectoryOnly);
            string[] currentCharacters = new string[charactersFolder.Length];
            for (int i = 0; i < charactersFolder.Length; i++)
            {
                currentCharacters[i] = Path.GetFileName(charactersFolder[i]);
            }
            return currentCharacters;
        }

        public static int ReturnSkinCountOfACharacter(string name)
        {
            string characterDirectory = Path.Combine(characterFolderPath, name);
            if (!DirectoryExist(characterDirectory))
                return 0;
            string[] characterSkins = Directory.GetDirectories(characterDirectory, "*", SearchOption.TopDirectoryOnly);
            return characterSkins.Length;
        }

        public static bool CharacterFolderExist(string name)
        {
            string characterPath = Path.Combine(characterFolderPath, name);
            return DirectoryExist(characterPath);
        }

        public static Material[] ReturnMaterialsOfCharacterPrefabPart(string name, int skinNum, CharacterPrefabParts characterPrefabParts)
        {
            string skinCountInFolder = SKIN_PREFIX + (skinNum + 1).ToString();
            string characterSkinPath = Path.Combine(LevelConstants.SKINS_LOADER_PATH, name, skinCountInFolder);
            switch (characterPrefabParts)
            {
                case CharacterPrefabParts.Character:
                    return GetAllMaterialsFromFolder(Path.Combine(characterSkinPath,SKIN_CHARACTER_PREFAB_FOLDER));
                case CharacterPrefabParts.Chassis:
                    return GetAllMaterialsFromFolder(Path.Combine(characterSkinPath, SKIN_CHASSIS_PREFAB_FOLDER));
                case CharacterPrefabParts.Wheel:
                    return GetAllMaterialsFromFolder(Path.Combine(characterSkinPath, SKIN_WHEELS_PREFAB_FOLDER));
            }
            return default;
        }

        public static void SaveNewSkinsInCharacterSkinDirectory(CharacterSkin[] characterSkins, string name)
        {
            string characterPath = Path.Combine(LevelConstants.SKINS_LOADER_PATH, name);
            if (!DirectoryExist(characterPath))
            {
                return;
            }
            for(int i = 0; i < characterSkins.Length; i++)
            {
                string skinFolder = Path.Combine(characterFolderPath,SKIN_PREFIX + (i + SKIN_STARTING_INT));
                string characterSkinFolder = NormalizePath(Path.Combine(skinFolder,SKIN_CHARACTER_PREFAB_FOLDER));
                string chassisSkinFolder = NormalizePath(Path.Combine(skinFolder, SKIN_CHASSIS_PREFAB_FOLDER));
                string wheelSkinFolder = NormalizePath(Path.Combine(skinFolder, SKIN_WHEELS_PREFAB_FOLDER));
                SetAllMaterialsFromFolder(characterSkinFolder, characterSkins[i].CharacterMaterials);
                SetAllMaterialsFromFolder(chassisSkinFolder, characterSkins[i].ChasisMaterials);
                SetAllMaterialsFromFolder(wheelSkinFolder, characterSkins[i].WheelMaterials);
            }
        }

        private static void SetAllMaterialsFromFolder(string folderPath,Material[] newMaterials)
        {
            Material[] materialsFromFolder = GetAllMaterialsFromFolder(folderPath);
            List<int> materialsIndexToChange = new List<int>();
            for(int i = 0; i < materialsFromFolder.Length && i<newMaterials.Length; i++)
            {
                if (materialsFromFolder[i] != newMaterials[i])
                {
                    materialsIndexToChange.Add(i);
                }
            }
            if (materialsIndexToChange.Count > 0)
            {
                string[] assetsPath = new string[materialsIndexToChange.Count];
                Material[] adbNewMaterials = newMaterials;
                newMaterials = new Material[materialsIndexToChange.Count];
                for (int i = 0; i < materialsIndexToChange.Count; i++)
                {
                    Material material = materialsFromFolder[materialsIndexToChange[i]];
                    assetsPath[i] = NormalizePath(Path.Combine(folderPath, material.name + ".mat"));
                    newMaterials[i] = adbNewMaterials[materialsIndexToChange[i]];
                }
                bool deleted = DeleteAsset(assetsPath);
            }
            SaveAssets(newMaterials, folderPath, ".mat");
        }

        private static bool DeleteAsset(string assetPath)
        {
            bool deleted = AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }
        private static bool DeleteAsset(string[] assetsPath)
        {
            List<string> outFailedPaths = new List<string>();
            bool deleted = AssetDatabase.DeleteAssets(assetsPath,outFailedPaths);
            foreach(string outFailedPath in outFailedPaths)
            {
                Debug.Log("File " + outFailedPath + " couldn't been deleted due to path not existing.");
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }

        private static void SaveAssets<T>(T[] objectsUnity,string folderPath,string extension) where T : Object
        {
            foreach (T objectUnity in objectsUnity)
            {

                string assetPath = Path.Combine(folderPath, objectUnity.name + extension);
                string normalizedAssetPath = NormalizePath(assetPath);
                AssetDatabase.CreateAsset(objectUnity,normalizedAssetPath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static Material[] GetAllMaterialsFromFolder(string folderPath)
        {
            string normalizedFolderPath = NormalizePath(folderPath);
           return Resources.LoadAll<Material>(normalizedFolderPath);
        }

        private static bool DirectoryExist(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Debug.LogError("Folder " + directoryPath + " doesn't exist");
                return false;
            }
            return true;
        }


        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/');
        }
    }
    }