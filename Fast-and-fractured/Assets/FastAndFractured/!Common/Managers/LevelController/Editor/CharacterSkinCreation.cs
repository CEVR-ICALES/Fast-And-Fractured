using System;
using System.Collections.Generic;
using FastAndFractured;
using UnityEditor;
using UnityEngine;
using Utilities;
using Enums;

public class CharacterSkinCreation : EditorWindow
{
    private class CharacterSkin
    {
        public Material[] CharacterMaterials = new Material[1];
        public Material[] ChasisMaterials = new Material[1];
        public Material[] WheelMaterials = new Material[1];
    }

    private Vector2 _scrollPosition;
    private string _characterName;
    private int _toalSkinCount;

    private CharacterSkin[] _characterSkin = new CharacterSkin[0];
    [MenuItem("Tools/CharacterRelated/CharacterSkinCreation", false, 50)]
    public static void ShowWindow()
    {
        GetWindow<CharacterSkinCreation>("CharacterSkinCreation");
    }

    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.LabelField("CharacterSkinCreation",
        EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Select the character you want an skin for ");
        EditorGUILayoutArrays.ArrayFieldSettings arrayFieldSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Characters in folder");
        string[] charactersWithSkins = CharacterSkinsCountFileGenerator.ReturnCharactersInCharacterSkinsFolder();
        
        EditorGUILayoutArrays.LabelArrayField(arrayFieldSettings, charactersWithSkins);

        EditorGUILayout.LabelField("Character you want an skin for");
        _characterName = EditorGUILayout.TextField("Character Name", _characterName);
        if (_characterName != null)
        {
            if (CharacterSkinsCountFileGenerator.CharacterFolderExist(_characterName))
            {
                _toalSkinCount = CharacterSkinsCountFileGenerator.ReturnSkinCountOfACharacter(_characterName);
                int characterSkinSize = _characterSkin.Length;
                characterSkinSize = EditorGUILayout.IntField("Size", characterSkinSize);
                characterSkinSize = characterSkinSize < _toalSkinCount ? _toalSkinCount : characterSkinSize;
                _characterSkin = ResizeArray(_characterSkin, characterSkinSize);
                if (_characterSkin[0] == null)
                {
                    for(int i = 0; i < _toalSkinCount; i++)
                    {
                        _characterSkin[i] = CreateACharacterSkinFromAlreadyExistingSkin(_characterSkin[i],_characterName,i);
                    }
                }
                for (int i = 0; i < characterSkinSize; i++)
                {
                    bool open = true;
                    open = EditorGUILayout.Foldout(open, "Skin num " + i);
                    if (open)
                    {
                        _characterSkin[i] = CharacterSkinLabels(_characterSkin[i]);
                    }
                }
                if (GUILayout.Button("Create", GUILayout.Height(35)))
                {
                }
            }
            else
            {
                EditorGUILayout.LabelField("Character folder doesn't exist. Pls use the character creation tool or" +
                    "create the folder on Assets/FastAndFractured/Resources/CharacterSkins");
            }
        }
        else
        {
            EditorGUILayout.LabelField("No name set on the text field.");
        }
            EditorGUILayout.EndScrollView();
    }

    private CharacterSkin CharacterSkinLabels(CharacterSkin characterSkin)
    {
        if (characterSkin==null)
        {
            characterSkin = new CharacterSkin();
        }
        EditorGUILayoutArrays.ArrayFieldSettings characterMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Character Materials [in order]");
        characterSkin.CharacterMaterials = EditorGUILayoutArrays.MaterialArrayField(characterMaterialSettings, characterSkin.CharacterMaterials);
        EditorGUILayoutArrays.ArrayFieldSettings chassisMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Chasis Materials [in order]");
        characterSkin.ChasisMaterials = EditorGUILayoutArrays.MaterialArrayField(chassisMaterialSettings, characterSkin.ChasisMaterials);
        EditorGUILayoutArrays.ArrayFieldSettings wheelMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Wheel Materials [in order]");
        characterSkin.ChasisMaterials = EditorGUILayoutArrays.MaterialArrayField(wheelMaterialSettings, characterSkin.WheelMaterials);
        return characterSkin;
    }

    private CharacterSkin CreateACharacterSkinFromAlreadyExistingSkin(CharacterSkin characterSkin,string name,int skinCount)
    {
        characterSkin = new CharacterSkin();
        characterSkin.CharacterMaterials = CharacterSkinsCountFileGenerator.ReturnMaterialsOfCharacterPrefabPart(name,skinCount,CharacterPrefabParts.Character);
        characterSkin.ChasisMaterials = CharacterSkinsCountFileGenerator.ReturnMaterialsOfCharacterPrefabPart(name, skinCount, CharacterPrefabParts.Chassis);
        characterSkin.WheelMaterials = CharacterSkinsCountFileGenerator.ReturnMaterialsOfCharacterPrefabPart(name, skinCount, CharacterPrefabParts.Wheel);
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
