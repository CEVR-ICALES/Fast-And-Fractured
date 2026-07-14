using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Enums;
using Utilities;
using UnityEditor;
using UnityEngine;
namespace FastAndFractured
{

    public class CharacterCreatorAndSkinsToolsLogic
    {
        //Folder Hierarchy
        private const string RESOURCES_FOLDER_PATH = "Assets/FastAndFractured/Resources";
        private const string LIST_OF_CHARACTER_SKINS = "/ListOfCharacterSkins.asset";
        private const string LIST_OF_PROTECTED_CHARACTERS = "CharactersSkins/ListOfProtectedCharacters";
        private const string SKIN_PREFIX = "_";
        private const string BASE_SKIN = "0";
        private const int SKIN_STARTING_INT = 1;
        private const string SKIN_CHARACTER_PREFAB_FOLDER = "Character";
        private const string SKIN_CHASSIS_PREFAB_FOLDER = "Chassis";
        private const string SKIN_WHEELS_PREFAB_FOLDER = "Wheels";
        private static string characterFolderPath = RESOURCES_FOLDER_PATH + "/" + LevelConstants.SKINS_LOADER_PATH;
        private const string GAMEPLAY_CAR_NAME = "GameplayCar";
        private const string PATH_TO_CHARACTERS = "Assets/FastAndFractured/Characters";
        private const string PATH_TO_MENU = "Assets/FastAndFractured/!Environment/Scenes/!MainMenuPostRelease/MainMenuAssets";
        private const string PATH_TO_MENU_CHARACTERS = PATH_TO_MENU + "/Prefabs/MenuCarsPrefabs";
        private const string PATH_TO_MENU_CHARACTERS_SCRIPTABLE_OBJECTS = PATH_TO_MENU + "/ScriptableObjects";
        private const string PATH_TO_BASE_CAR_FROM_CHARACTERS = "!Common/BaseCar.prefab";
        private const string SCRIPTABLE_OBJECT_FOlDER = "ScriptableObjects";
        private const string PREFABS_FOLDER = "Prefabs";
        private const string CAR_DATA_SO_NAME = "CarData.asset";
        private const string MENU_DATA_SO_Name = "MenuData.asset";
        private const string UNIQUE_ABILITY_SO_NAME = "UniqueAbilityData.asset";

        private const string LIST_OF_CHARACTERS_DATA_SO_PATH = "CharactersSkins/ListOfCharactersData";
        private const string LIST_OF_MENU_CHARACTERS_DATA_SO_PATH = "CharactersSkins/MainMenuSelectionScreenCharacters";

        //Prefab Hierarchy
        private const string VISUAL_PATH = "Visuals";
        private const string CHARACTER_PATH = "Visuals/Character";
        private const string CHASSIS_PATH = "Visuals/Chassis";
        private const string FRONT_LEFT_WHEEL_PATH = "Visuals/WheelsVisuals/FrontLeftWheel";
        private const string FRONT_RIGHT_WHEEL_PATH = "Visuals/WheelsVisuals/FrontRightWheel";
        private const string BACK_LEFT_WHEEL_PATH = "Visuals/WheelsVisuals/BackLeftWheel";
        private const string BACK_RIGHT_WHEEL_PATH = "Visuals/WheelsVisuals/BackRightWheel";
        private const string TURRET_PATH = "Turret/Visuals/CanonVisuals";
        private const string WHEELS_COLLIDER_PATH = "WheelColliders";
        private const string CHARACTER_HOLDER_NAME = "Character";
        private const string CHARACTER_MODEL_NAME = "CharacterModel";
        private const string CHASIS_MODEL_NAME = "Chassis";
        private const string WHEEL_MODEL_NAME = "WheelVisuals";

        //CharacterCreatorRelated

        //Skins related


        #region character_creator_related
        public static bool CreateNewCharacter(GameObject characterModel, GameObject chasisModel, GameObject wheelModel, GameObject turretModel, string characterName)
        {
            if (characterModel != null && chasisModel != null && wheelModel != null && turretModel != null && characterName != string.Empty && characterName != null)
            {
                foreach (GameObject model in new GameObject[] { characterModel, chasisModel, wheelModel, turretModel })
                {
                    if (!CheckIfModelHaveVisuals(model))
                    {
                        Debug.LogError("The model " + model.name + " doesn't have a child called 'Visuals'. Use the FBX To Prefab tool to create the character prefab part.");
                        return false;
                    }
                }
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

                    FileUtils.CheckAndCreateDirectory(pathToNewCharacterParentFolder);

                    string prefabsDirectory = Path.Combine(pathToNewCharacterParentFolder, PREFABS_FOLDER);

                    FileUtils.CheckAndCreateDirectory(prefabsDirectory);

                    GameObject baseCar = AssetDatabase.LoadAssetAtPath(pathToBaseCar, typeof(GameObject)) as GameObject;
                    GameObject newBaseCar = PrefabUtility.InstantiatePrefab(baseCar) as GameObject;
                    GameObject newBaseCarFromSource;
                    newBaseCar.name = characterName + GAMEPLAY_CAR_NAME;
                    Transform characterHolder = newBaseCar.transform.Find(CHARACTER_PATH);
                    Transform chassisHolder = newBaseCar.transform.Find(CHASSIS_PATH);
                    Transform frontLeftWheelHolder = newBaseCar.transform.Find(FRONT_LEFT_WHEEL_PATH);
                    Transform frontRightWheelHolder = newBaseCar.transform.Find(FRONT_RIGHT_WHEEL_PATH);
                    Transform backLeftWheelHolder = newBaseCar.transform.Find(BACK_LEFT_WHEEL_PATH);
                    Transform backRightWheelHolder = newBaseCar.transform.Find(BACK_RIGHT_WHEEL_PATH);
                    Transform turretHolder = newBaseCar.transform.Find(TURRET_PATH);
                    GameObject[] wheelsMesh = new GameObject[4];
                    string pathToCreateNewCharacter = Path.Combine(prefabsDirectory, newBaseCar.name + ".prefab");
                    try
                    {
                        characterModel = PrefabUtility.InstantiatePrefab(characterModel, characterHolder) as GameObject;
                        characterModel.name = characterName + CHARACTER_HOLDER_NAME;
                        Transform model = characterModel.transform.Find(VISUAL_PATH).GetChild(0);
                        model.name = CHARACTER_MODEL_NAME;
                        chasisModel = PrefabUtility.InstantiatePrefab(chasisModel, chassisHolder) as GameObject;
                        chasisModel.name = characterName + CHASIS_MODEL_NAME;
                        wheelsMesh[0] = PrefabUtility.InstantiatePrefab(wheelModel, frontRightWheelHolder) as GameObject;
                        wheelsMesh[1] = PrefabUtility.InstantiatePrefab(wheelModel, backLeftWheelHolder) as GameObject;
                        wheelsMesh[2] = PrefabUtility.InstantiatePrefab(wheelModel, backRightWheelHolder) as GameObject;
                        wheelsMesh[3] = PrefabUtility.InstantiatePrefab(wheelModel, frontLeftWheelHolder) as GameObject;
                        turretModel = PrefabUtility.InstantiatePrefab(turretModel, turretHolder) as GameObject;
                        foreach (var wheelMesh in wheelsMesh)
                        {
                            wheelMesh.name = WHEEL_MODEL_NAME;
                        }
                        PrefabUtility.SaveAsPrefabAsset(newBaseCar, pathToCreateNewCharacter);
                        UnityEngine.Object.DestroyImmediate(newBaseCar);
                        AssetDatabase.StopAssetEditing();
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        AssetDatabase.StartAssetEditing();
                        newBaseCarFromSource = AssetDatabase.LoadAssetAtPath(pathToCreateNewCharacter, typeof(GameObject)) as GameObject;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                        return false;
                    }

                    string scriptableObjectDirectory = Path.Combine(pathToNewCharacterParentFolder, SCRIPTABLE_OBJECT_FOlDER);

                    FileUtils.CheckAndCreateDirectory(scriptableObjectDirectory);
                    CharacterData characterData = ScriptableObject.CreateInstance<CharacterData>();
                    if (!File.Exists(scriptableObjectDirectory))
                    {
                        characterData.name = characterName + CAR_DATA_SO_NAME;
                        characterData.CharacterName = characterName;
                        characterData.CarDefaultPrefab = newBaseCarFromSource;
                        string carDataSOPath = Path.Combine(scriptableObjectDirectory, characterData.name);
                        AssetDatabase.CreateAsset(characterData, carDataSOPath);
                        AddCharacterToListOfCharactersData(characterData);
                        Debug.Log("File " + carDataSOPath + " created");
                    }


                    if (!File.Exists(scriptableObjectDirectory))
                    {
                        AbilityData abilityData = ScriptableObject.CreateInstance<AbilityData>();
                        abilityData.name = characterName + UNIQUE_ABILITY_SO_NAME;
                        string uniqueAbilitySOPath = Path.Combine(scriptableObjectDirectory, abilityData.name);
                        AssetDatabase.CreateAsset(abilityData, uniqueAbilitySOPath);
                        Debug.Log("File " + abilityData + " created");
                    }

                    if (!CreateMenuVariant(newBaseCarFromSource, characterName, wheelsMesh, characterData))
                    {
                        Debug.LogError("Menu Variant of the character was not created");
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

        private static bool CheckIfModelHaveVisuals(GameObject model)
        {
            Transform modelVisual = model.transform.Find(VISUAL_PATH);
            return modelVisual != null;
        }

        private static bool CreateMenuVariant(GameObject gameplayCarParent, string characterName, GameObject[] wheelsMesh, CharacterData characterData)
        {
            if (!FileUtils.DirectoryExist(PATH_TO_MENU_CHARACTERS))
            {
                return false;
            }
            try
            {
                GameObject menuVariant = PrefabUtility.InstantiatePrefab(gameplayCarParent) as GameObject;
                menuVariant.name = characterName + SKIN_PREFIX + BASE_SKIN;
                MonoBehaviour[] monoBehavioursScripts = menuVariant.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour monoBehaviour in monoBehavioursScripts)
                {
                    UnityEngine.Object.DestroyImmediate(monoBehaviour);
                }
                Transform wheelsColliderParent = menuVariant.transform.Find(WHEELS_COLLIDER_PATH);
                MonoBehaviour[] monoBehavioursInWheelsCollider = wheelsColliderParent.GetComponentsInChildren<MonoBehaviour>();
                Collider[] collidersInWheelsCollider = wheelsColliderParent.GetComponentsInChildren<Collider>();
                foreach (MonoBehaviour monoBehaviour1 in monoBehavioursInWheelsCollider)
                {
                    UnityEngine.Object.DestroyImmediate(monoBehaviour1);
                }
                foreach (Collider collider in collidersInWheelsCollider)
                {
                    collider.gameObject.AddComponent<SphereCollider>().radius = 1f;
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                CharSelectionSimulatedMovement charSelectionSimulatedMovement = menuVariant.AddComponent<CharSelectionSimulatedMovement>();
                charSelectionSimulatedMovement.WheelsMesh = wheelsMesh;
                string pathToCreateMenuCharacter = Path.Combine(PATH_TO_MENU_CHARACTERS, menuVariant.name + ".prefab");
                PrefabUtility.SaveAsPrefabAsset(menuVariant, pathToCreateMenuCharacter);
                UnityEngine.Object.DestroyImmediate(menuVariant);
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                AssetDatabase.StartAssetEditing();
                GameObject assetMenuVariant = AssetDatabase.LoadAssetAtPath(pathToCreateMenuCharacter, typeof(GameObject)) as GameObject;


                string carDataSOPath = FileUtils.NormalizePath(Path.Combine(PATH_TO_MENU_CHARACTERS_SCRIPTABLE_OBJECTS, characterName + MENU_DATA_SO_Name));

                if (!File.Exists(carDataSOPath))
                {
                    CharacterMenuData characterMenuData = ScriptableObject.CreateInstance<CharacterMenuData>();
                    characterMenuData.name = characterName + CAR_DATA_SO_NAME;
                    characterMenuData.CharacterName = characterName;
                    characterMenuData.CharacterDescription = "Menu." + characterName;
                    characterMenuData.Models = new GameObject[] { assetMenuVariant };
                    characterMenuData.CharacterStats = characterData;
                    AssetDatabase.CreateAsset(characterMenuData, carDataSOPath);
                    AddCharacterToListOfMenuCharactersData(characterMenuData);
                    Debug.Log("File " + carDataSOPath + " created");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return false;
            }
            return true;
        }

        public static void DeleteACharacter(string characterName)
        {
            ListOfProtectedCharacters listOfProtectedCharacters = Resources.Load<ListOfProtectedCharacters>(LIST_OF_PROTECTED_CHARACTERS);
            if (listOfProtectedCharacters == null)
            {
                Debug.LogError("List of protected characters doesn't exist. To prevent any important character delete, the function will not procced. Check on the Resources file '" + listOfProtectedCharacters + "'");
                return;
            }
            if (listOfProtectedCharacters.ProtectedCharacters.Contains(characterName))
            {
                Debug.LogError("The character " + characterName + " is protected on the " + LIST_OF_PROTECTED_CHARACTERS + " from the Resources Folder. The function will not procced.");
                return;
            }
            RemoveCharacterFromListOfCharactersData(characterName);
            RemoveCharacterFromListOfMenuCharactersData(characterName);
            string carDataSOPath = Path.Combine(PATH_TO_MENU_CHARACTERS_SCRIPTABLE_OBJECTS, characterName + MENU_DATA_SO_Name);
            string characterMenuVariantPath = Path.Combine(PATH_TO_MENU_CHARACTERS, characterName + SKIN_PREFIX);

            FileUtils.DeleteAsset(carDataSOPath);
            int skinNum = ReturnSkinCountOfACharacter(characterName);
            for (int i = skinNum; i >= 0; i--)
            {
                string characterMenuVariantSkinPath = characterMenuVariantPath + i + ".prefab";
                FileUtils.DeleteAsset(characterMenuVariantSkinPath);
            }
            string pathToDeleteCharacterParentFolder = Path.Combine(PATH_TO_CHARACTERS, characterName);
            string pathToDeleteCharacterSkinParentFolder = Path.Combine(characterFolderPath, characterName);
            string[] pathsToDeleteCharacterFromGame = new string[] { pathToDeleteCharacterParentFolder, pathToDeleteCharacterSkinParentFolder};
          foreach(string pathToDeleteCharacterFromGame in pathsToDeleteCharacterFromGame)
          {
                if (!FileUtils.DeleteFolder(pathToDeleteCharacterFromGame))
                {
                    Debug.LogError("Folder " + pathToDeleteCharacterFromGame + "wasn't able to be deleted. Check if the folder already exist.");
                    return;
                }
          }
            Debug.Log("Character " + characterName + " deleted Succesfully");
            GenerateCharacterSkinCountFile();
        }

        private static void AddCharacterToListOfCharactersData(CharacterData characterData)
        {
            ListOfCharactersData listOfCharactersData = Resources.Load<ListOfCharactersData>(LIST_OF_CHARACTERS_DATA_SO_PATH);
            if(listOfCharactersData == null)
            {
                Debug.LogWarning("List of characters data doesn't exist. If this file doesn't exist, there will be no data available for the characters. Create it in the Resources/CharactersSkins folder.");
                return;
            }
            listOfCharactersData.listOfCharactersData.Add(characterData);
            EditorUtility.SetDirty(listOfCharactersData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RemoveCharacterFromListOfCharactersData(string characterName)
        {
            ListOfCharactersData listOfCharactersData = Resources.Load<ListOfCharactersData>(LIST_OF_CHARACTERS_DATA_SO_PATH);
            if (listOfCharactersData == null)
            {
                Debug.LogWarning("List of characters data doesn't exist. If this file doesn't exist, there will be no data available for the characters. Create it in the Resources/CharactersSkins folder.");
                return;
            }
            CharacterData characterDataToRemove = listOfCharactersData.listOfCharactersData.Find(characterData => characterData.CharacterName == characterName);
            if(characterDataToRemove != null)
            {
                listOfCharactersData.listOfCharactersData.Remove(characterDataToRemove);
                EditorUtility.SetDirty(listOfCharactersData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }

        private static void AddCharacterToListOfMenuCharactersData(CharacterMenuData characterMenuData)
        {
            MainMenuSelectionScreenCharacters listOfMenuCharactersData = Resources.Load<MainMenuSelectionScreenCharacters>(LIST_OF_MENU_CHARACTERS_DATA_SO_PATH);
            if (listOfMenuCharactersData == null)
            {
                Debug.LogWarning("List of menu characters data doesn't exist. If this file doesn't exist, there will be no data available for the character selection screen. Create it in the Resources/CharactersSkins folder.");
                return;
            }
            listOfMenuCharactersData.allMainMenuCharactersData.Add(characterMenuData);
            EditorUtility.SetDirty(listOfMenuCharactersData);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void RemoveCharacterFromListOfMenuCharactersData(string characterName)
        {
            MainMenuSelectionScreenCharacters listOfMenuCharactersData = Resources.Load<MainMenuSelectionScreenCharacters>(LIST_OF_MENU_CHARACTERS_DATA_SO_PATH);
            if (listOfMenuCharactersData == null)
            {
                Debug.LogWarning("List of menu characters data doesn't exist. If this file doesn't exist, there will be no data available for the character selection screen. Create it in the Resources/CharactersSkins folder.");
                return;
            }
            CharacterMenuData characterMenuDataToRemove = listOfMenuCharactersData.allMainMenuCharactersData.Find(characterMenuData => characterMenuData.CharacterDescription == "Menu." + characterName);
            if (characterMenuDataToRemove != null)
            {
                listOfMenuCharactersData.allMainMenuCharactersData.Remove(characterMenuDataToRemove);
                EditorUtility.SetDirty(listOfMenuCharactersData);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
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
            AssetDatabase.CreateFolder(characterFolderPath, CharacterName);
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
            if (!FileUtils.DirectoryExist(characterDirectory))
                return -1;
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
                    return FileUtils.GetAllMaterialsFromFolder(Path.Combine(characterSkinPath, SKIN_CHARACTER_PREFAB_FOLDER));
                case CharacterPrefabParts.Chassis:
                    return FileUtils.GetAllMaterialsFromFolder(Path.Combine(characterSkinPath, SKIN_CHASSIS_PREFAB_FOLDER));
                case CharacterPrefabParts.Wheel:
                    return FileUtils.GetAllMaterialsFromFolder(Path.Combine(characterSkinPath, SKIN_WHEELS_PREFAB_FOLDER));
            }
            return default;
        }

        public static void SaveNewSkinsInCharacterSkinDirectory(CharacterSkin[] characterSkins, string name)
        {
            string characterPath = Path.Combine(characterFolderPath, name);
            for (int i = 0; i < characterSkins.Length; i++)
            {
                string skinFolder = Path.Combine(characterPath, SKIN_PREFIX + (i + SKIN_STARTING_INT));
                string characterSkinFolder = FileUtils.NormalizePath(Path.Combine(skinFolder, SKIN_CHARACTER_PREFAB_FOLDER));
                string chassisSkinFolder = FileUtils.NormalizePath(Path.Combine(skinFolder, SKIN_CHASSIS_PREFAB_FOLDER));
                string wheelSkinFolder = FileUtils.NormalizePath(Path.Combine(skinFolder, SKIN_WHEELS_PREFAB_FOLDER));
                if (!FileUtils.DirectoryExist(skinFolder))
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
            UpdateCharacterMenuModel(name);
        }

        public static void UpdateAllCharactersMenuModels()
        {
            string[] allCharactersInSkinsFolder = ReturnCharactersInCharacterSkinsFolder();
            foreach (string character in allCharactersInSkinsFolder)
            {
                UpdateCharacterMenuModel(character);
            }
        }

        private static void UpdateCharacterMenuModel(string characterName)
        {
            int skinCount = ReturnSkinCountOfACharacter(characterName);
            GameObject[] newMenuList = new GameObject[skinCount + SKIN_STARTING_INT];
            if (skinCount < 0)
            {
                Debug.LogError("Character " + characterName + " provided doesn't exist. Maybe an error ocurred during the chaarcter creation tool or this tool wasn't use. " +
                    "Go to '" + characterFolderPath + "' to check if the directory " + characterName + " exist, if not, create it");
                return;
            }
            else if (skinCount == 0)
            {
                Debug.LogWarning("Character skin count is 0. Check if this statement is true on '" + Path.Combine(characterFolderPath, characterName) + "'. If is empty, the character doesn't have any skin. If you want to add one, use the character skin tool.");
                return;
            }
            string carDataSOPath = Path.Combine(PATH_TO_MENU_CHARACTERS_SCRIPTABLE_OBJECTS, characterName + MENU_DATA_SO_Name);
            string characterMenuVariantPath = Path.Combine(PATH_TO_MENU_CHARACTERS, characterName + SKIN_PREFIX);
            string characterMenuVariantBaseSkinPath = FileUtils.NormalizePath(characterMenuVariantPath + BASE_SKIN + ".prefab");
            if (!File.Exists(characterMenuVariantBaseSkinPath))
            {

                return;
            }
            AssetDatabase.StartAssetEditing();
            GameObject menuCharacterVariant = AssetDatabase.LoadAssetAtPath(characterMenuVariantBaseSkinPath, typeof(GameObject)) as GameObject;
            newMenuList[0] = menuCharacterVariant;

            for (int i = SKIN_STARTING_INT; i <= skinCount; i++)
            {
                string characterSkinVariantPath = FileUtils.NormalizePath(characterMenuVariantPath + i + ".prefab");
                GameObject menuCharacterSkinVariant;
                if (!File.Exists(characterSkinVariantPath))
                {
                    menuCharacterSkinVariant = PrefabUtility.InstantiatePrefab(menuCharacterVariant) as GameObject;
                    menuCharacterSkinVariant.name = characterName + SKIN_PREFIX + i;
                    SetCharacterSkin(menuCharacterSkinVariant.name, menuCharacterSkinVariant);
                    PrefabUtility.SaveAsPrefabAsset(menuCharacterSkinVariant, characterSkinVariantPath);
                    UnityEngine.Object.DestroyImmediate(menuCharacterSkinVariant);
                }
            }
              AssetDatabase.StopAssetEditing();
              AssetDatabase.SaveAssets();
              AssetDatabase.Refresh();
              AssetDatabase.StartAssetEditing();

            for (int i = SKIN_STARTING_INT; i <= skinCount; i++)
            {
                string characterSkinVariantPath = FileUtils.NormalizePath(characterMenuVariantPath + i + ".prefab");
                GameObject menuCharacterSkinVariantSource;
                menuCharacterSkinVariantSource = AssetDatabase.LoadAssetAtPath(characterSkinVariantPath, typeof(GameObject)) as GameObject;
                SetCharacterSkin(menuCharacterSkinVariantSource.name, menuCharacterSkinVariantSource);
                
                newMenuList[i] = menuCharacterSkinVariantSource;
            }

            string characterMenuDataSOPath = FileUtils.NormalizePath(Path.Combine(PATH_TO_MENU_CHARACTERS_SCRIPTABLE_OBJECTS, characterName + MENU_DATA_SO_Name));
            if (!File.Exists(characterMenuDataSOPath))
            {
                //Error Log
                return;
            }
            CharacterMenuData characterMenuData = AssetDatabase.LoadAssetAtPath(characterMenuDataSOPath, typeof(CharacterMenuData)) as CharacterMenuData;
            characterMenuData.Models = newMenuList;
            EditorUtility.SetDirty(characterMenuData);
            AssetDatabase.SaveAssets();
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            Debug.Log("Characters Skins in menu updated");
        }

        private static void SetAllMaterialsFromFolder(string folderPath, Material[] materialsGiven)
        {
            string resourcesPath = Path.GetRelativePath(RESOURCES_FOLDER_PATH, folderPath);
            Material[] materialsFromFolder = FileUtils.GetAllMaterialsFromFolder(resourcesPath);
            Material[] newMaterials = (Material[])materialsGiven.Clone(); ;

            bool noNewMaterials = true;
            if (materialsFromFolder.Length != newMaterials.Length)
                noNewMaterials = false;
            for (int i = 0; i < materialsFromFolder.Length && i < newMaterials.Length; i++)
            {
                if (materialsFromFolder[i] == newMaterials[i])
                {
                    newMaterials[i] = null;
                }
                else
                {
                    noNewMaterials = false;
                    string assetPath = Path.Combine(folderPath, materialsFromFolder[i].name + ".mat");
                    FileUtils.DeleteAsset(assetPath);
                }
            }
            if (!noNewMaterials)
            {
                FileUtils.SaveAssets(newMaterials, folderPath, ".mat");
            }
        }

        public static void SetCharacterSkin(string nameCode, GameObject instantiatedCar)
        {
            LevelUtilities.ParseCharacterNameCode(nameCode, out string name, out int skinNum);

            string skinPath = LevelConstants.SKINS_LOADER_PATH + "/" + name + "/" + "_" + skinNum;
            Transform visuals = instantiatedCar.transform.Find(LevelConstants.VISUAL_CHARACTER_PARTS);

            //Character Skin
            //Hierarchy for the character model '/Visuals/Character/{characterName}Character/Visuals/CharacterModel/{characterName}' 

            string characterModelPath = LevelConstants.CHARACTER_MATERIALS_FOLDER + "/" + name + LevelConstants.CHARACTER_MATERIALS_FOLDER + "/" + LevelConstants.CHARACTER_PREFAB_PATH;
            Transform character = visuals.Find(characterModelPath);

            if (!SetSkinPart(character, skinPath + "/" + LevelConstants.CHARACTER_MATERIALS_FOLDER))
            {
                Debug.LogError($"Character model to change the skin not found. Make sure the hierarchy to get the model is " + characterModelPath);
            }

            //Chassis Skin
            //Hierarchy for the chassis model '/Visuals/Chassis/{characterName}Chassis/Visuals/{characterName}Vehicle'

            string chassisPath = LevelConstants.CHASSIS_PREFAB_PATH + "/" + name + LevelConstants.CHASSIS_PREFAB_PATH + "/" + LevelConstants.VISUAL_CHARACTER_PARTS;
            Transform chassis = visuals.transform.Find(chassisPath);

            bool logError = false;

            if (chassis != null)
            {
                Transform chassisModel = chassis.GetChild(0);
                logError = !SetSkinPart(chassisModel, skinPath + "/" + LevelConstants.CHASSIS_MATERIALS_FOLDER);
            }
            else
                logError = true;
            if (logError)
                Debug.LogError($"Vehicle model to change the skin not found.Make sure the hierarchy to get the model is " + chassisPath);


            //Wheels Skin
            //Hierarchy for the wheels /Visuals/WheelsVisuals/[Front/Back][Left/Right]Wheel/WheelVisuals/Visuals/[anyName]

            Transform frontRightWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.FRONT_RIGHT_WHEEL_PATH);
            Transform backRightWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.BACK_RiGHT_WHEEL_PATH);
            Transform frontLeftWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.FRONT_LEFT_WHEEL_PATH);
            Transform backLeftWheel = visuals.transform.Find(LevelConstants.GENERIC_PREFAB_WHEEL_PATH + "/" + LevelConstants.BACK_LEFT_WHEEL_PATH);

            logError = false;

            if (frontRightWheel != null && backRightWheel != null && frontLeftWheel != null && backLeftWheel != null)
            {
                Transform[] wheels = new Transform[]
                {
                frontRightWheel.GetChild(0),
                backRightWheel.GetChild(0),
                frontLeftWheel.GetChild(0),
                backLeftWheel.GetChild(0),
                };
                logError = !SetSkinPart(wheels, skinPath + "/" + LevelConstants.WHEElS_MATERIALS_FOLDER);
            }
            else
                logError = true;
            if (logError)
                Debug.LogError($"Wheels models to change the skin not found.Make sure the hierarchy to get the models is /Visuals/WheelsVisuals/[Front / Back][Left / Right]Wheel/WheelVisuals/Visuals/[anyName]");
        }

        private static Material[] LoadSkinMaterials(string path)
        {
            return Resources.LoadAll<Material>(path);
        }

        private static bool SetSkinPart(Transform instantiatedCarPart, string skinPartPath)
        {
            Material[] skinPart = LoadSkinMaterials(skinPartPath);
            if (skinPart.Length != 0)
            {
                if (instantiatedCarPart == null)
                {
                    return false;
                }
                Renderer renderPart = instantiatedCarPart.GetComponent<Renderer>();
                if (renderPart == null)
                {
                    if((renderPart = instantiatedCarPart.GetComponentInChildren<Renderer>()) != null)
                    {
                        Debug.Log("Renderer not found on " + instantiatedCarPart.name + ". Renderer found in child " + renderPart.name);
                    }
                    else
                    {
                    Debug.LogError("Renderer not found on " + instantiatedCarPart.name);
                    return false;
                    }
                }
                Material[] defaultSkinMaterials = renderPart.sharedMaterials;
                for (int materialIterator = 0; materialIterator < defaultSkinMaterials.Length; materialIterator++)
                {
                    if (skinPart.Length > materialIterator)
                    {
                        defaultSkinMaterials[materialIterator] = skinPart[materialIterator];
                    }
                    else
                    {
                        defaultSkinMaterials[materialIterator] = skinPart[materialIterator - 1];
                    }
                }
                renderPart.materials = defaultSkinMaterials;
            }
            return true;
        }

        private static bool SetSkinPart(Transform[] instantiatedCarParts, string skinPartPath)
        {

            Material[] skinPart = LoadSkinMaterials(skinPartPath);
            if (skinPart.Length != 0)
            {
                foreach (Transform instantiatedCarPart in instantiatedCarParts)
                {
                    if (instantiatedCarPart == null)
                    {
                        return false;
                    }
                    Renderer renderPart = instantiatedCarPart.GetComponent<Renderer>();
                    if (renderPart == null)
                    {
                        if((renderPart = instantiatedCarPart.GetComponentInChildren<Renderer>()) != null)
                        {
                            Debug.Log("Renderer not found on " + instantiatedCarPart.name + ". Renderer found in child " + renderPart.name);
                        }
                        else
                        {
                            Debug.LogError("Renderer not found on " + instantiatedCarPart.name);
                            return false;
                        }
                    }
                    Material[] defaultSkinMaterials = renderPart.sharedMaterials;
                    for (int materialIterator = 0; materialIterator < defaultSkinMaterials.Length; materialIterator++)
                    {
                        if (skinPart.Length > materialIterator)
                        {
                            defaultSkinMaterials[materialIterator] = skinPart[materialIterator];
                        }
                        else
                        {
                            defaultSkinMaterials[materialIterator] = skinPart[materialIterator - 1];
                        }
                    }
                    renderPart.materials = defaultSkinMaterials;
                }
            }
            return true;
        }


        #endregion
    }

}