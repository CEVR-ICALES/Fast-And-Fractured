using System;
using System.Collections.Generic;
using Enums;
using FastAndFractured;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Utilities;

public class CharacterSkinCreation : EditorWindow
{
   

    private Vector2 _scrollPosition;
    private string _characterName;
    private string _currentCharacterName;
    private int _selectedCharacter;
    private int _previousCharacter = DEFAULT_PREVIOUS_CHARACTER_VALUE;
    private int _toalSkinCount;
    private float _skinGroupValue = 0;

    private List<bool> _foldoutSkinsToogle = new List<bool>();
    private string[] _charactersWithSkins;

    private const int DEFAULT_PREVIOUS_CHARACTER_VALUE = -1;
    private const bool DEFAULT_FOLDOUT_SKIN_TOGGLE = false;
    private const int SKIN_NUM_MOD_TO_SHOW = 1;
    private CharacterCreatorAndSkinsToolsLogic.CharacterSkin[] _characterSkin = new CharacterCreatorAndSkinsToolsLogic.CharacterSkin[0];
    [MenuItem("Tools/CharacterRelated/CharacterSkinCreation", false, 50)]
    public static void ShowWindow()
    {
        GetWindow<CharacterSkinCreation>("CharacterSkinCreation");
    }

    [MenuItem("Tools/CharacterRelated/GenerateCharacterSkinCountFile")]
    public static void ShowWindowGenerateCharacterSkinCountFile()
    {
        CharacterCreatorAndSkinsToolsLogic.GenerateCharacterSkinCountFile();
    }

    [MenuItem("Tools/CharacterRelated/UpdateAllCharactersMenuModels")]
    public static void ShowWindowUpdate()
    {
        CharacterCreatorAndSkinsToolsLogic.UpdateAllCharactersMenuModels();
    }

    private void CreateGUI()
    {
        _charactersWithSkins = CharacterCreatorAndSkinsToolsLogic.ReturnCharactersInCharacterSkinsFolder();
        foreach (var character in _charactersWithSkins) {
            _foldoutSkinsToogle.Add(DEFAULT_FOLDOUT_SKIN_TOGGLE);
        }
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.LabelField("CharacterSkinCreation",
        EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Welcome to the character skin creation tool. Here you can modify or create new skins for already existing characters", MessageType.Info);
        EditorGUILayout.LabelField("Select the character you want an skin for ");
        _selectedCharacter = EditorGUILayout.Popup("Character", _selectedCharacter, _charactersWithSkins);
        _currentCharacterName = _charactersWithSkins[_selectedCharacter];
        EditorGUILayout.HelpBox("All character skins will be numerated starting from the skin num 1. To add new skins, modify the int label 'Skin size'.", MessageType.Info);
        EditorGUILayout.HelpBox("Is not posible to delete an already existing skin using this tool to prevent compilation errors or common mistakes. You have to do it manually on the Resources folder " +
            "'Assets/FastAndFractured/Resources/CharactersSkins/"+_currentCharacterName+"'", MessageType.Warning);
        if (_previousCharacter!=_selectedCharacter)
        {
            _previousCharacter = _selectedCharacter;
           _skinGroupValue = 1;
           if (_characterName != _currentCharacterName)
              {
                _characterSkin = Array.Empty<CharacterCreatorAndSkinsToolsLogic.CharacterSkin>();
                _characterName = _currentCharacterName;
              }
        }

        EditorGUILayout.BeginFadeGroup(_skinGroupValue);
        if (_skinGroupValue==1)
        {
                _toalSkinCount = CharacterCreatorAndSkinsToolsLogic.ReturnSkinCountOfACharacter(_characterName);
                int characterSkinSize = _characterSkin.Length;
                characterSkinSize = EditorGUILayout.IntField("Skin Size", characterSkinSize);
                characterSkinSize = characterSkinSize < _toalSkinCount ? _toalSkinCount : characterSkinSize;
                _characterSkin = ResizeArray(_characterSkin, characterSkinSize);
            EditorGUILayout.HelpBox("Each skin, will have a material label for a prefab part. Some parts could have more than one material, so you can modify the 'Material num' to allow it.", MessageType.Info);
            if (_characterSkin.Length > 0)
            {
                if (_characterSkin[0] == null)
                {
                    for (int i = 0; i < _toalSkinCount; i++)
                    {
                        _characterSkin[i] = CreateACharacterSkinFromAlreadyExistingSkin(_characterSkin[i], _characterName, i);
                    }
                }
                for (int i = 0; i < characterSkinSize; i++)
                {
                    int skinNumToShow = i + SKIN_NUM_MOD_TO_SHOW;
                    _foldoutSkinsToogle[i] = EditorGUILayout.Foldout(_foldoutSkinsToogle[i], "Skin num " + skinNumToShow);
                    if (_foldoutSkinsToogle[i])
                    {
                        _characterSkin[i] = CharacterSkinLabels(_characterSkin[i]);
                    }
                }
                if (GUILayout.Button("Create", GUILayout.Height(35)))
                {
                    CharacterCreatorAndSkinsToolsLogic.SaveNewSkinsInCharacterSkinDirectory(_characterSkin, _characterName);
                }
            }

            }
            
        EditorGUILayout.EndFadeGroup();

        EditorGUILayout.EndScrollView();
    }

    private CharacterCreatorAndSkinsToolsLogic.CharacterSkin CharacterSkinLabels(CharacterCreatorAndSkinsToolsLogic.CharacterSkin characterSkin)
    {
        if (characterSkin==null)
        {
            characterSkin = new CharacterCreatorAndSkinsToolsLogic.CharacterSkin();
        }
        EditorGUILayoutArrays.ArrayFieldSettings characterMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Character Materials [in order]");
        characterSkin.CharacterMaterials = EditorGUILayoutArrays.MaterialArrayField(characterMaterialSettings, characterSkin.CharacterMaterials);
        EditorGUILayoutArrays.ArrayFieldSettings chassisMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Chasis Materials [in order]");
        characterSkin.ChasisMaterials = EditorGUILayoutArrays.MaterialArrayField(chassisMaterialSettings, characterSkin.ChasisMaterials);
        EditorGUILayoutArrays.ArrayFieldSettings wheelMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Wheel Materials [in order]");
        characterSkin.WheelMaterials = EditorGUILayoutArrays.MaterialArrayField(wheelMaterialSettings, characterSkin.WheelMaterials);
        return characterSkin;
    }

    private CharacterCreatorAndSkinsToolsLogic.CharacterSkin CreateACharacterSkinFromAlreadyExistingSkin(CharacterCreatorAndSkinsToolsLogic.CharacterSkin characterSkin,string name,int skinCount)
    {
        characterSkin = new CharacterCreatorAndSkinsToolsLogic.CharacterSkin();
        characterSkin.CharacterMaterials = CharacterCreatorAndSkinsToolsLogic.ReturnMaterialsOfCharacterPrefabPart(name,skinCount,CharacterPrefabParts.Character);
        characterSkin.ChasisMaterials = CharacterCreatorAndSkinsToolsLogic.ReturnMaterialsOfCharacterPrefabPart(name, skinCount, CharacterPrefabParts.Chassis);
        characterSkin.WheelMaterials = CharacterCreatorAndSkinsToolsLogic.ReturnMaterialsOfCharacterPrefabPart(name, skinCount, CharacterPrefabParts.Wheel);
        return characterSkin;
    }

    private static T[] ResizeArray<T>(T[] array, int size)
    {
        T[] newArray = new T[size];

        for (var i = 0; i < size; i++)
        {
            if (i < array.Length)
            {
                newArray[i] = array[i];
            }
        }

        return newArray;
    }

}
