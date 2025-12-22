using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class CharacterCreator : EditorWindow
    {
        private Vector2 _scrollPosition;

        //Logic variables
        private GameObject _baseCar;
        private GameObject _characterModel;
        private GameObject _chasisModel;
        private GameObject _wheelModel;
        private string _characterName;

        //Project Hierarchy paths
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


        [MenuItem("Tools/CharacterRelated/CharacterCreator", false, 50)]
        public static void ShowWindow()
        {
            GetWindow<CharacterCreator>("CharacterCreator");
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("Character Creator",
            EditorStyles.boldLabel);
            _characterName = EditorGUILayout.TextField("Character Name",_characterName);
            _characterModel = EditorGUILayout.ObjectField("Character Model",_characterModel, typeof(GameObject), false) as GameObject;
            _chasisModel = EditorGUILayout.ObjectField("Chasis Model", _chasisModel, typeof(GameObject), false) as GameObject;
            _wheelModel = EditorGUILayout.ObjectField("Wheel Model", _wheelModel, typeof(GameObject), false) as GameObject;
            if (GUILayout.Button("Create", GUILayout.Height(35)))
            {
                if (CreateNewCharacter())
                {
                    Debug.Log("Character " +  _characterName + " Created Successfully");
                    CharacterSkinsCountFileGenerator.CreateNewCharacterInCharacterSkinsFolder(_characterName);
                }
                else
                {
                    Debug.Log("Character creation failed");
                }
            }
            EditorGUILayout.EndScrollView();
        }

        bool CreateNewCharacter()
        {
            if (_characterModel != null && _chasisModel != null && _wheelModel != null && _characterName != string.Empty && _characterName != null)
            {
                _characterModel.name = _characterName + CHARACTER_MODEL_NAME;
                _characterModel.name = _characterName + CHASIS_MODEL_NAME;
                _wheelModel.name = WHEEL_MODEL_NAME;
                try
                {
                    AssetDatabase.StartAssetEditing();

                    string pathToBaseCar = Path.Combine(PATH_TO_CHARACTERS, PATH_TO_BASE_CAR_FROM_CHARACTERS);
                    string pathToNewCharacterParentFolder = Path.Combine(PATH_TO_CHARACTERS, _characterName);
                    if (!File.Exists(pathToBaseCar))
                    {
                        Debug.LogError("File base car doesn't exit in " + pathToBaseCar);
                        return false;
                    }

                    CheckAndCreateDirectory(pathToNewCharacterParentFolder);

                    string prefabsDirectory = Path.Combine(pathToNewCharacterParentFolder, PREFABS_FOLDER);
                    string pathToCreateNewCharacter = Path.Combine(prefabsDirectory, _characterName + ".prefab");

                    CheckAndCreateDirectory(prefabsDirectory);

                    _baseCar = AssetDatabase.LoadAssetAtPath(pathToBaseCar, typeof(GameObject)) as GameObject;
                    GameObject newBaseCar = PrefabUtility.InstantiatePrefab(_baseCar) as GameObject;
                    newBaseCar.name = _characterName + GAMEPLAY_CAR_NAME;
                    Transform characterHolder = newBaseCar.transform.Find(CHARACTER_PATH);
                    Transform chassisHolder = newBaseCar.transform.Find(CHASSIS_PATH);
                    Transform frontLeftWheelHolder = newBaseCar.transform.Find(FRONT_LEFT_WHEEL_PATH);
                    Transform frontRightWheelHolder = newBaseCar.transform.Find(FRONT_RIGHT_WHEEL_PATH);
                    Transform backLeftWheelHolder = newBaseCar.transform.Find(BACK_LEFT_WHEEL_PATH);
                    Transform backRightWheelHolder = newBaseCar.transform.Find(BACK_RIGHT_WHEEL_PATH);
                    try
                    {
                        PrefabUtility.InstantiatePrefab(_characterModel, characterHolder);
                        PrefabUtility.InstantiatePrefab(_chasisModel, chassisHolder);
                        PrefabUtility.InstantiatePrefab(_wheelModel, frontLeftWheelHolder);
                        PrefabUtility.InstantiatePrefab(_wheelModel, frontRightWheelHolder);
                        PrefabUtility.InstantiatePrefab(_wheelModel, backLeftWheelHolder);
                        PrefabUtility.InstantiatePrefab(_wheelModel, backRightWheelHolder);
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
                        characterData.name = _characterName + CAR_DATA_SO_NAME;
                        characterData.CharacterName = _characterName;
                        AssetDatabase.CreateAsset(characterData, carDataSOPath);
                        newBaseCar.GetComponent<StatsController>().CharacterData = characterData;
                        Debug.Log("File " + carDataSOPath + " created");
                    }

                    string uniqueAbilitySOPath = Path.Combine(scriptableObjectDirectory, UNIQUE_ABILITY_SO_NAME);

                    if (!File.Exists(scriptableObjectDirectory))
                    {
                        AbilityData abilityData = new AbilityData();
                        abilityData.name = _characterName + UNIQUE_ABILITY_SO_NAME;
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
       private void CheckAndCreateDirectory(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                Debug.Log("Folder " + folderPath + " created");
            }
        }
    }
}
