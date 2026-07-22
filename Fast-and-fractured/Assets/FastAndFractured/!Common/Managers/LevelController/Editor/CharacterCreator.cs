using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.Utilities;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class CharacterCreator : EditorWindow
    {
        private Vector2 _scrollPosition;

        //Logic variables
        private GameObject _characterModel;
        private GameObject _chasisModel;
        private GameObject _wheelModel;
        private GameObject _turretModel;
        private string _characterName;
        private GUILayoutOption _layoutOption;
        private bool _correctName = false;
        private string[] _charactersNames;


        [MenuItem("Tools/CharacterRelated/CharacterCreator", false, 50)]
        public static void ShowWindow()
        {
            GetWindow<CharacterCreator>("CharacterCreator");
        }
        private void CreateGUI()
        {
            _charactersNames = CharacterCreatorAndSkinsToolsLogic.ReturnCharactersInCharacterSkinsFolder();
        }
        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            EditorGUILayout.LabelField("Character Creator",
            EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Welcome to the Character CreatorTool. Any character you create, will be created on the characters project folder. A menuVariant and a character skin folder will be created as well.",MessageType.Info);
            EditorGUILayout.HelpBox("First, set a none existing name for the character.", MessageType.Info);
            _characterName = EditorGUILayout.TextField("Character Name",_characterName);
            _correctName = true;
            if (_charactersNames.Contains(_characterName))
            {
                _correctName = false;
                EditorGUILayout.HelpBox("Name selected already exist",MessageType.Error);
            }
            EditorGUILayout.HelpBox("Models should be created using the FBX To Prefab tool. Since the structure Prefab->Visual->Model is needed.", MessageType.Warning);
            _characterModel = EditorGUILayout.ObjectField("Character Model",_characterModel, typeof(GameObject), false) as GameObject;
            _chasisModel = EditorGUILayout.ObjectField("Chasis Model", _chasisModel, typeof(GameObject), false) as GameObject;
            _wheelModel = EditorGUILayout.ObjectField("Wheel Model", _wheelModel, typeof(GameObject), false) as GameObject;
            _turretModel = EditorGUILayout.ObjectField("Turret Model", _turretModel, typeof(GameObject), false) as GameObject;
            if (GUILayout.Button("Create", GUILayout.Height(35)))
            {
                if (_characterModel != null && _chasisModel != null && _wheelModel != null && _turretModel != null)
                {
                    if (_correctName)
                    {
                        if (CharacterCreatorAndSkinsToolsLogic.CreateNewCharacter(_characterModel, _chasisModel, _wheelModel, _turretModel, _characterName))
                        {
                            Debug.Log("Character " + _characterName + " Created Successfully");
                            CharacterCreatorAndSkinsToolsLogic.CreateNewCharacterInCharacterSkinsFolder(_characterName);
                        }
                        else
                        {
                            Debug.Log("Character creation failed");
                        }
                        _correctName = false;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox("Warning: some or all models labels are empty.", MessageType.Error);
                }

            }
            EditorGUILayout.EndScrollView();
        }
    }
}
