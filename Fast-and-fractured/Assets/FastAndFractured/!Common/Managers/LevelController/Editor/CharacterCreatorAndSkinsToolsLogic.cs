using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Enums;
using System;
namespace FastAndFractured {

    public class CharacterCreatorAndSkinsToolsLogic
    {
        //Folder Hierarchy
        private const string RESOURCES_FOLDER_PATH = "Assets/FastAndFractured/Resources";
        private const string LIST_OF_CHARACTER_SKINS = "/ListOfCharacterSkins.asset";
        private const string SKIN_PREFIX = "_";
        private const int SKIN_STARTING_INT = 1;
        private const string SKIN_CHARACTER_PREFAB_FOLDER = "Character";
        private const string SKIN_CHASSIS_PREFAB_FOLDER = "Chassis";
        private const string SKIN_WHEELS_PREFAB_FOLDER = "Wheels";
        private static string characterFolderPath = RESOURCES_FOLDER_PATH + "/" + LevelConstants.SKINS_LOADER_PATH;
        private const string GAMEPLAY_CAR_NAME = "GameplayCar";
        private const string PATH_TO_CHARACTERS = "Assets/FastAndFractured/Characters";
        private const string PATH_TO_BASE_CAR_FROM_CHARACTERS = "!Common/BaseCar.prefab";
        private const string SCRIPTABLE_OBJECT_FOlDER = "ScriptableObjects";
        private const string PREFABS_FOLDER = "Prefabs";
        private const string CAR_DATA_SO_NAME = "CarData.asset";
        private const string UNIQUE_ABILITY_SO_NAME = "UniqueAbilityData.asset";        

        //Prefab Hierarchy
        private const string CHARACTER_PATH = "Visuals/Character";
        private const string CHASSIS_PATH = "Visuals/Chassis";
        private const string FRONT_LEFT_WHEEL_PATH = "Visuals/WheelsVisuals/FrontLeftWheel";
        private const string FRONT_RIGHT_WHEEL_PATH = "Visuals/WheelsVisuals/FrontRightWheel";
        private const string BACK_LEFT_WHEEL_PATH = "Visuals/WheelsVisuals/BackLeftWheel";
        private const string BACK_RIGHT_WHEEL_PATH = "Visuals/WheelsVisuals/BackRightWheel";
        private const string CHARACTER_MODEL_NAME = "Character";
        private const string CHASIS_MODEL_NAME = "Chassis";
        private const string WHEEL_MODEL_NAME = "WheelVisuals";

        //CharacterCreatorRelated

        //Skins related
       

        #region character_creator_related
      public static bool CreateNewCharacter(GameObject characterModel, GameObject chasisModel, GameObject wheelModel, string characterName)
        {
            if (characterModel != null && chasisModel != null && wheelModel != null && characterName != string.Empty && characterName != null)
            {
                chasisModel.name = characterName + CHARACTER_MODEL_NAME;
                chasisModel.name = characterName + CHASIS_MODEL_NAME;
                wheelModel.name = WHEEL_MODEL_NAME;
                try
                {
                    AssetDatabase.StartAssetEditing();

                    string pathToBaseCar = Path.Combine(PATH_TO_CHARACTERS, PATH_TO_BASE_CAR_FROM_CHARACTERS);
                    string pathToNewCharacterParentFolder = Path.Combine(PATH_TO_CHARACTERS, characterName);
                    if (!File.Exists(pathToBaseCar))
                    {
                        Debug.LogError("File base car doesn't exit in " + pathToBaseCar);
                        return false;
                    }

                    CheckAndCreateDirectory(pathToNewCharacterParentFolder);

                    string prefabsDirectory = Path.Combine(pathToNewCharacterParentFolder, PREFABS_FOLDER);
                    string pathToCreateNewCharacter = Path.Combine(prefabsDirectory, characterName + ".prefab");

                    CheckAndCreateDirectory(prefabsDirectory);

                    GameObject baseCar = AssetDatabase.LoadAssetAtPath(pathToBaseCar, typeof(GameObject)) as GameObject;
                    GameObject newBaseCar = PrefabUtility.InstantiatePrefab(baseCar) as GameObject;
                    newBaseCar.name = characterName + GAMEPLAY_CAR_NAME;
                    Transform characterHolder = newBaseCar.transform.Find(CHARACTER_PATH);
                    Transform chassisHolder = newBaseCar.transform.Find(CHASSIS_PATH);
                    Transform frontLeftWheelHolder = newBaseCar.transform.Find(FRONT_LEFT_WHEEL_PATH);
                    Transform frontRightWheelHolder = newBaseCar.transform.Find(FRONT_RIGHT_WHEEL_PATH);
                    Transform backLeftWheelHolder = newBaseCar.transform.Find(BACK_LEFT_WHEEL_PATH);
                    Transform backRightWheelHolder = newBaseCar.transform.Find(BACK_RIGHT_WHEEL_PATH);
                    try
                    {
                        PrefabUtility.InstantiatePrefab(characterModel, characterHolder);
                        PrefabUtility.InstantiatePrefab(chasisModel, chassisHolder);
                        PrefabUtility.InstantiatePrefab(chasisModel, frontLeftWheelHolder);
                        PrefabUtility.InstantiatePrefab(wheelModel, frontRightWheelHolder);
                        PrefabUtility.InstantiatePrefab(wheelModel, backLeftWheelHolder);
                        PrefabUtility.InstantiatePrefab(wheelModel, backRightWheelHolder);
                        PrefabUtility.SaveAsPrefabAsset(newBaseCar, pathToCreateNewCharacter);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        return false;
                    }

                    string scriptableObjectDirectory = Path.Combine(pathToNewCharacterParentFolder, SCRIPTABLE_OBJECT_FOlDER);

                    CheckAndCreateDirectory(scriptableObjectDirectory);

                    string carDataSOPath = Path.Combine(scriptableObjectDirectory, CAR_DATA_SO_NAME);

                    if (!File.Exists(scriptableObjectDirectory))
                    {
                        CharacterData characterData = new CharacterData();
                        characterData.name = characterName + CAR_DATA_SO_NAME;
                        characterData.CharacterName = characterName;
                        AssetDatabase.CreateAsset(characterData, carDataSOPath);
                        newBaseCar.GetComponent<StatsController>().CharacterData = characterData;
                        Debug.Log("File " + carDataSOPath + " created");
                    }

                    string uniqueAbilitySOPath = Path.Combine(scriptableObjectDirectory, UNIQUE_ABILITY_SO_NAME);

                    if (!File.Exists(scriptableObjectDirectory))
                    {
                        AbilityData abilityData = new AbilityData();
                        abilityData.name = characterName + UNIQUE_ABILITY_SO_NAME;
                        AssetDatabase.CreateAsset(abilityData, uniqueAbilitySOPath);
                        Debug.Log("File " + abilityData + " created");
                    }

                    AssetDatabase.StopAssetEditing();
                }
                catch (Exception e)
                {
                    AssetDatabase.StopAssetEditing();
                    Debug.LogException(e);
                    return false;
                }
                return true;
            }
            Debug.LogError("No empty labelfields");
            return false;
        }
        #endregion

        #region skin_related
        public class CharacterSkin
        {
            public Material[] CharacterMaterials = new Material[1];
            public Material[] ChasisMaterials = new Material[1];
            public Material[] WheelMaterials = new Material[1];
        }
        public static void GenerateCharacterSkinCountFile()
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
            GenerateCharacterSkinCountFile();
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

        public static Material[] ReturnMaterialsOfCharacterPrefabPart(string name, int skinNum, CharacterPrefabParts characterPrefabParts)
        {
            int skinRealNum = skinNum + 1;
            string skinCountInFolder = SKIN_PREFIX + skinRealNum.ToString();
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
            string characterPath = Path.Combine(characterFolderPath, name);
            for(int i = 0; i < characterSkins.Length; i++)
            {
                string skinFolder = Path.Combine(characterPath,SKIN_PREFIX + (i + SKIN_STARTING_INT));
                string characterSkinFolder = NormalizePath(Path.Combine(skinFolder,SKIN_CHARACTER_PREFAB_FOLDER));
                string chassisSkinFolder = NormalizePath(Path.Combine(skinFolder, SKIN_CHASSIS_PREFAB_FOLDER));
                string wheelSkinFolder = NormalizePath(Path.Combine(skinFolder, SKIN_WHEELS_PREFAB_FOLDER));
                if(!DirectoryExist(skinFolder))
                {
                    Directory.CreateDirectory(skinFolder);
                    Directory.CreateDirectory(characterSkinFolder);
                    Directory.CreateDirectory(chassisSkinFolder);
                    Directory.CreateDirectory(wheelSkinFolder);
                }
                SetAllMaterialsFromFolder(characterSkinFolder, characterSkins[i].CharacterMaterials);
                SetAllMaterialsFromFolder(chassisSkinFolder, characterSkins[i].ChasisMaterials);
                SetAllMaterialsFromFolder(wheelSkinFolder, characterSkins[i].WheelMaterials);
            }
            GenerateCharacterSkinCountFile();
        }

        private static void SetAllMaterialsFromFolder(string folderPath,Material[] materialsGiven)
        {
            string resourcesPath = Path.GetRelativePath(RESOURCES_FOLDER_PATH, folderPath);
            Material[] materialsFromFolder = GetAllMaterialsFromFolder(resourcesPath);
            Material[] newMaterials = (Material[])materialsGiven.Clone(); ;
            
            bool noNewMaterials = true;
            if (materialsFromFolder.Length != newMaterials.Length)
                noNewMaterials = false;
            for(int i = 0; i < materialsFromFolder.Length && i<newMaterials.Length; i++)
            {
                if (materialsFromFolder[i] == newMaterials[i])
                {
                    newMaterials[i] = null;
                }
                else
                {
                    noNewMaterials = false;
                    string assetPath = Path.Combine(folderPath, materialsFromFolder[i].name + ".mat");
                    DeleteAsset(assetPath);
                }
            }
            if (!noNewMaterials) {
                SaveAssets(newMaterials, folderPath, ".mat");
            }
        }


        #endregion

        #region directory_related
        private static bool DeleteAsset(string assetPath)
        {
            string normalizedAssetPath = NormalizePath(assetPath);
            bool deleted = AssetDatabase.DeleteAsset(normalizedAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return deleted;
        }
        private static bool DeleteAsset(string[] assetsPath)
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

        private static void SaveAssets<T>(T[] objectsUnity, string folderPath, string extension) where T : UnityEngine.Object
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
        private static void CheckAndCreateDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log("Folder " + folderPath + " created");
            }
        }
        public static bool CharacterFolderExist(string name)
        {
            string characterPath = Path.Combine(characterFolderPath, name);
            return DirectoryExist(characterPath);
        }



        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/');
        }
        #endregion
    }

}