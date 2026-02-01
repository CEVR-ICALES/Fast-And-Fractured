using FastAndFractured;
using UnityEditor;
using UnityEngine;

public class CharacterDelete : EditorWindow
{
    private Vector2 _scrollPosition;
    private string _characterName;
    private string _currentCharacterName;
    private string[] _charactersWithSkins;
    private int _selectedCharacter;

    private int _previousCharacter = DEFAULT_PREVIOUS_CHARACTER_VALUE;

    private const int DEFAULT_PREVIOUS_CHARACTER_VALUE = -1;

    [MenuItem("Tools/CharacterRelated/CharacterEraser", false, 50)]
    public static void ShowWindow()
    {
        GetWindow<CharacterDelete>("CharacterEraser");
    }

    private void CreateGUI()
    {
        _charactersWithSkins = CharacterCreatorAndSkinsToolsLogic.ReturnCharactersInCharacterSkinsFolder();
    }
    private void OnGUI()
    {
        _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
        EditorGUILayout.LabelField("CharacterSkinCreation",
         EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Select the character you want an skin for ");
        _selectedCharacter = EditorGUILayout.Popup("Character", _selectedCharacter, _charactersWithSkins);
        _currentCharacterName = _charactersWithSkins[_selectedCharacter];
        if (GUILayout.Button("Delete Character", GUILayout.Height(35)))
        {
            CharacterCreatorAndSkinsToolsLogic.DeleteACharacter(_currentCharacterName);
            _charactersWithSkins = CharacterCreatorAndSkinsToolsLogic.ReturnCharactersInCharacterSkinsFolder();
        }
        EditorGUILayout.EndScrollView();
    }
}
