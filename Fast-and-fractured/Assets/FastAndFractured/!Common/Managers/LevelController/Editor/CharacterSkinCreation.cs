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
    private int _toalSkinCount;
    private float _skinGroupValue = 0;

    private CharacterCreatorAndSkinsToolsLogic.CharacterSkin[] _characterSkin = new CharacterCreatorAndSkinsToolsLogic.CharacterSkin[0];
    [MenuItem("Tools/CharacterRelated/CharacterSkinCreation", false, 50)]
    public static void ShowWindow()
    {
        GetWindow<CharacterSkinCreation>("CharacterSkinCreation");
    }

    [MenuItem("Tools/GenerateCharacterSkinCountFile")]
    public static void ShowWindowGenerateCharacterSkinCountFile()
    {
        CharacterCreatorAndSkinsToolsLogic.GenerateCharacterSkinCountFile();
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.LabelField("CharacterSkinCreation",
        EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Select the character you want an skin for ");
        EditorGUILayoutArrays.ArrayFieldSettings arrayFieldSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Characters in folder");
        string[] charactersWithSkins = CharacterCreatorAndSkinsToolsLogic.ReturnCharactersInCharacterSkinsFolder();
        
        EditorGUILayoutArrays.LabelArrayField(arrayFieldSettings, charactersWithSkins);

        EditorGUILayout.LabelField("");
        _selectedCharacter = EditorGUILayout.Popup("Character you want an skin for", _selectedCharacter, charactersWithSkins);
        _currentCharacterName = charactersWithSkins[_selectedCharacter];
        
        if (GUILayout.Button("Select and continue", GUILayout.Height(35)))
        {
            _skinGroupValue = 0;
            if (_currentCharacterName == null)
            {
                EditorGUILayout.LabelField("No name set on the text field.");
            }
            else if (!CharacterCreatorAndSkinsToolsLogic.CharacterFolderExist(_currentCharacterName))
            {
                EditorGUILayout.LabelField("Character folder doesn't exist. Pls use the character creation tool or" +
                   "create the folder on Assets/FastAndFractured/Resources/CharacterSkins");
            }
            else
            {
                _skinGroupValue = 1;
                if (_characterName != _currentCharacterName)
                {
                    _characterSkin = Array.Empty<CharacterCreatorAndSkinsToolsLogic.CharacterSkin>();
                    _characterName = _currentCharacterName;
                }
            }
        }

        EditorGUILayout.BeginFadeGroup(_skinGroupValue);
        if (_skinGroupValue==1)
        {
                _toalSkinCount = CharacterCreatorAndSkinsToolsLogic.ReturnSkinCountOfACharacter(_characterName);
                int characterSkinSize = _characterSkin.Length;
                characterSkinSize = EditorGUILayout.IntField("Size", characterSkinSize);
                characterSkinSize = characterSkinSize < _toalSkinCount ? _toalSkinCount : characterSkinSize;
                _characterSkin = ResizeArray(_characterSkin, characterSkinSize);
                if (_characterSkin[0] == null)
                {
                    for (int i = 0; i < _toalSkinCount; i++)
                    {
                        _characterSkin[i] = CreateACharacterSkinFromAlreadyExistingSkin(_characterSkin[i], _characterName, i);
                    }
                }
                for (int i = 0; i < characterSkinSize; i++)
                {
                    bool open = true;
                    open = EditorGUILayout.Foldout(open, "Skin num _" + i + 1);
                    if (open)
                    {
                        _characterSkin[i] = CharacterSkinLabels(_characterSkin[i]);
                    }
                }
                if (GUILayout.Button("Create", GUILayout.Height(35)))
                {
                CharacterCreatorAndSkinsToolsLogic.SaveNewSkinsInCharacterSkinDirectory(_characterSkin,_characterName);
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
