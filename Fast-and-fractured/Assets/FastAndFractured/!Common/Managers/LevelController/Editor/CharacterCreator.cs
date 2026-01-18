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
        private GameObject _characterModel;
        private GameObject _chasisModel;
        private GameObject _wheelModel;
        private string _characterName;
        private GUILayoutOption _layoutOption;


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
                if (CharacterCreatorAndSkinsToolsLogic.CreateNewCharacter(_characterModel,_chasisModel,_wheelModel,_characterName))
                {
                    Debug.Log("Character " +  _characterName + " Created Successfully");
                    CharacterCreatorAndSkinsToolsLogic.CreateNewCharacterInCharacterSkinsFolder(_characterName);
                }
                else
                {
                    Debug.Log("Character creation failed");
                }
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
