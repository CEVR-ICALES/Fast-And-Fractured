using System;
using System.Collections.Generic;
using FastAndFractured;
using UnityEditor;
using UnityEngine;
using Utilities;

public class CharacterSkinCreation : EditorWindow
{
    private class CharacterSkin
    {
        public Material[] CharacterMaterials = new Material[1];
        public Material[] ChasisMaterials = new Material[1];
        public Material[] WheelMaterials = new Material[1];
    }

    private Vector2 _scrollPosition;

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

        int characterSkinSize = _characterSkin.Length;
        characterSkinSize = EditorGUILayout.IntField("Size", characterSkinSize);
        characterSkinSize = characterSkinSize < 0 ? 0 : characterSkinSize;
        _characterSkin = ResizeArray(_characterSkin, characterSkinSize);
        for(int i = 0; i < characterSkinSize; i++)
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
        characterSkin.ChasisMaterials = EditorGUILayoutArrays.MaterialArrayField(characterMaterialSettings, characterSkin.ChasisMaterials);
        EditorGUILayoutArrays.ArrayFieldSettings wheelMaterialSettings = new EditorGUILayoutArrays.ArrayFieldSettings("Wheel Materials [in order]");
        characterSkin.ChasisMaterials = EditorGUILayoutArrays.MaterialArrayField(characterMaterialSettings, characterSkin.ChasisMaterials);
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
