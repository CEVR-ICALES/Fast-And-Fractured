using System.IO;
using UnityEditor;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class CharacterCreator : EditorWindow
    {
        private Vector2 _scrollPosition;

        private GameObject _baseCar;
        private GameObject _characterModel;
        private GameObject _chasisModel;
        private GameObject _wheelModel;
        private string _characterName;

        private const string GAMEPLAY_CAR_NAME = "GameplayCar";
        private const string PATH_TO_CHARACTERS = "FastAndFractured/Characters";
        private const string PATH_TO_BASE_CAR_FROM_CHARACTERS = "/!Common/BaseCar.prefab";
        private const string CHARACTER_PATH = "Visuals/Character";
        private const string CHASSIS_PATH = "Visuals/Chassis";
        private const string FRONT_LEFT_WHEEL_PATH = "Visuals/WheelsVisuals/FrontLeftWheel";
        private const string FRONT_RIGHT_WHEEL_PATH = "Visuals/WheelsVisuals/FrontRightWheel";
        private const string BACK_LEFT_WHEEL_PATH = "Visuals/WheelsVisuals/BackLeftWheel";
        private const string BACK_RIGHT_WHEEL_PATH = "Visuals/WheelsVisuals/BackRightWheel";


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
                CreateNewCharacter();
            }
            EditorGUILayout.EndScrollView();
        }

        void CreateNewCharacter()
        {
            if( _characterModel != null && _characterName != string.Empty && _chasisModel != null && _wheelModel != null)
            {
                AssetDatabase.StartAssetEditing();
                string relativePathToBaseCar = "Assets/" + PATH_TO_CHARACTERS + PATH_TO_BASE_CAR_FROM_CHARACTERS;
                string baseCarPath = NormalizePath(relativePathToBaseCar);
                Debug.Log(baseCarPath);
                _baseCar = AssetDatabase.LoadAssetAtPath(baseCarPath, typeof(GameObject)) as GameObject;
                string characterModelPath = NormalizePath(AssetDatabase.GetAssetPath(_characterModel));
                string characterModelFolder = Path.GetDirectoryName(characterModelPath);
                string parentFolder = Directory.GetParent(characterModelPath)?.FullName;
                if (string.IsNullOrEmpty(parentFolder) || !parentFolder.StartsWith(Application.dataPath.Replace('/', '\\')))
                {
                    return;
                }
                GameObject newBaseCar = PrefabUtility.InstantiatePrefab(_baseCar) as GameObject;
                newBaseCar.name = _characterName + GAMEPLAY_CAR_NAME;
                Transform characterHolder = newBaseCar.transform.Find(CHARACTER_PATH);
                Transform chassisHolder = newBaseCar.transform.Find(CHASSIS_PATH);
                Transform frontLeftWheelHolder = newBaseCar.transform.Find(FRONT_LEFT_WHEEL_PATH);
                Transform frontRightWheelHolder = newBaseCar.transform.Find(FRONT_LEFT_WHEEL_PATH);
                Transform backLeftWheelHolder = newBaseCar.transform.Find(BACK_LEFT_WHEEL_PATH);
                Transform backRightWheelHolder = newBaseCar.transform.Find(BACK_RIGHT_WHEEL_PATH);
                (PrefabUtility.InstantiatePrefab(_characterModel) as GameObject).transform.parent = characterHolder;
                (PrefabUtility.InstantiatePrefab(_chasisModel) as GameObject).transform.parent = chassisHolder;
            }
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            return path.Replace('\\', '/');
        }
    }
}
