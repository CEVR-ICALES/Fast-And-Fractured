using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectorManager : MonoBehaviour
{
    public CharacterMenuData[] allCharacters;

    // all information we want to show...
    public Image charIcon;
    public TextMeshProUGUI charName;
    public TextMeshProUGUI charDescription;
    public TextMeshProUGUI charCarWeight;
    public TextMeshProUGUI charCarMaxSpeed;
    public TextMeshProUGUI charCarMaxEndurance;
    public TextMeshProUGUI charCarBaseForce;
    public TextMeshProUGUI charCarAcceleration;
    public TextMeshProUGUI charCarManuver;

    [SerializeField] private Transform modelSpawnPosition;
    [SerializeField] private Button selectAndStartButton;

    private GameObject _currentModelInstance;
    private int _currentCharacterIndex;
    private int _currentSkinIndex;


    private const int  FULLY_UNLOCKED_VALUE = 5;
    private const string  SELECTED_PLAYER_KEY = "Selected_Player";


    private void OnEnable()
    {
        UpdateCharacterDisplay();
        _currentCharacterIndex = 0;
        _currentSkinIndex = 0;
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

    public void SelectNextSkin()
    {
        CharacterMenuData character = allCharacters[_currentCharacterIndex];
        _currentSkinIndex = _currentSkinIndex + 1;
        if (_currentSkinIndex >= character.Models.Length) _currentSkinIndex = 0;
        ChangeCurrentDisplayedModel(character);
        ChangePlayerIcon();
    }

    public void SelectPreviousSkin()
    {
        CharacterMenuData character = allCharacters[_currentCharacterIndex];
        _currentSkinIndex = _currentSkinIndex + -1;
        if (_currentSkinIndex >= character.Models.Length) _currentSkinIndex = character.Models.Length -1;
        ChangeCurrentDisplayedModel(character);
        ChangePlayerIcon();
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

        ChangeCurrentDisplayedModel(character);

        ChangePlayerIcon();

        UpdateInformationTexts(character);

        CheckIfSkinUnlocked();

    }

    private void UpdateInformationTexts(CharacterMenuData character) // PROVISIONAL
    {
        charName.text = character.CharacterName;
        charDescription.text = character.CharacterDescription;
        charCarWeight.text ="Weight: " + character.CharacterStats.Weight.ToString();
        charCarMaxSpeed.text = "MaxSpeed: " + character.CharacterStats.MaxSpeed.ToString();
        charCarMaxEndurance.text = "Endurance: " + character.CharacterStats.MaxEndurance.ToString();
        charCarBaseForce.text = "Base Force: " + character.CharacterStats.BaseForce.ToString();
        charCarAcceleration.text = "Acceleration: " + character.CharacterStats.Acceleration.ToString(); //create acceleration distinction 
        charCarManuver.text = "Manuver: " + character.CharacterStats.DriftingSmoothFactor.ToString(); //create manuver distinction 
    }

    private void ChangePlayerIcon()
    {
        Sprite icon = ResourcesManager.Instance.GetResourcesSprite(allCharacters[_currentCharacterIndex].Models[_currentSkinIndex].name);

        if(icon != null)
        {
            charIcon.sprite = icon;
        }
    }

    private void ChangeCurrentDisplayedModel(CharacterMenuData character)
    {
        if (_currentModelInstance != null)
            Destroy(_currentModelInstance);
        _currentModelInstance = Instantiate(character.Models[_currentSkinIndex], modelSpawnPosition); // instantiate new model
    }
    private void CheckIfSkinUnlocked()
    {
        int skinUnlockedValue = PlayerPrefs.GetInt(_currentModelInstance.name);

        if(skinUnlockedValue == FULLY_UNLOCKED_VALUE)
        {
            selectAndStartButton.enabled = true;
        } else
        {
            selectAndStartButton.enabled = false;
        }
    }

    public void SaveCurrentSelected()
    {
        CharacterMenuData character = allCharacters[_currentCharacterIndex];
        PlayerPrefs.SetString(SELECTED_PLAYER_KEY, character.name + "_" + _currentSkinIndex);
    }



}
