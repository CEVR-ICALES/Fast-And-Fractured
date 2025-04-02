using TMPro;
using UnityEngine;

public class CharacterSelectorManager : MonoBehaviour
{
    public CharacterMenuData[] allCharacters;

    // all information we want to show...
    public Sprite charIcon;
    public TextMeshProUGUI charName;
    public TextMeshProUGUI charDescription;
    public TextMeshProUGUI charCarWeight;
    public TextMeshProUGUI charCarMaxSpeed;
    public TextMeshProUGUI charCarMaxEndurance;
    public TextMeshProUGUI charCarBaseForce;

    [SerializeField] private Transform modelDisplayParent;

    private GameObject _currentModelInstance;
    private int _currentCharacterIndex;
    private int _currentSkinIndex;


    private void OnEnable()
    {
        // get character icons from the resourcews manager
    }

    public void SelectNextCharacter()
    {
        int newIndex = _currentCharacterIndex + 1;
        if (newIndex >= allCharacters.Length) newIndex = 0;
        ChangeCharacter(newIndex);
    }

    public void SelectPreviousCharacter()
    {
        int newIndex = _currentCharacterIndex - 1;
        if (newIndex < 0) newIndex = allCharacters.Length - 1;
        ChangeCharacter(newIndex);
    }

    private void ChangeCharacter(int newIndex)
    {
        _currentCharacterIndex = newIndex;
        _currentSkinIndex = 0;
        UpdateCharacterDisplay();
    }

    private void UpdateCharacterDisplay()
    {
        if (_currentModelInstance != null)
            Destroy(_currentModelInstance);

        CharacterMenuData character = allCharacters[_currentCharacterIndex];

        _currentModelInstance = Instantiate(character.Models[_currentSkinIndex], modelDisplayParent); // instantiate new model

        // change icon considering current skin and model by calling the resource manager

        charName.text = character.CharacterName;
        charDescription.text = character.CharacterDescription;
        charCarWeight.text = character.CharacterStats.Weight.ToString();
        charCarMaxSpeed.text = character.CharacterStats.MaxSpeed.ToString();
        charCarMaxEndurance.text = character.CharacterStats.MaxEndurance.ToString();
        charCarBaseForce.text = character.CharacterStats.BaseForce.ToString();
    }


}
