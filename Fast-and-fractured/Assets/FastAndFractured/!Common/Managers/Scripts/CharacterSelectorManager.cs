using Assets.SimpleLocalization.Scripts;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Utilities;

public class CharacterSelectorManager : AbstractSingleton<CharacterSelectorManager>
{
    public CharacterMenuData[] allCharacters;

    // all information we want to show...
    [Header("Canvas")]
    public Image charIcon;
    public TextMeshProUGUI charName;
    public TextMeshProUGUI charDescription;
    public TextMeshProUGUI charCarWeight;
    public TextMeshProUGUI charCarMaxSpeed;
    public TextMeshProUGUI charCarMaxEndurance;
    public TextMeshProUGUI charCarBaseForce;
    public TextMeshProUGUI charCarAcceleration;
    public TextMeshProUGUI charCarManuver;
    public Image charCarWeightImage;
    public Image charCarMaxSpeedImage;
    public Image charCarMaxEnduranceImage;
    public Image charCarBaseForceImage;
    public Image charCarAccelerationImage;
    public Image charCarManuverImage;

    public TextMeshProUGUI skinUnlockedValueText;
    public GameObject skinUnlockedLock;

    [Header("Car Anims Related")]
    [SerializeField] private Transform modelSpawnPosition;
    [SerializeField] private Button selectAndStartButton;
    [SerializeField] private Collider carStopCollider;
    [SerializeField] private float modelChangeTimerDuration;
    private ITimer _modelChangeTimer;


    private GameObject _currentModelInstance;
    private int _currentCharacterIndex;
    private int _currentSkinIndex;


    private const int  FULLY_UNLOCKED_VALUE = 5;
    private const string  SELECTED_PLAYER_KEY = "Selected_Player";


    private void OnEnable()
    {
        PlayerPrefs.SetInt("Josefino_0", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Carme_0", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Pepe_0", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("MariaAntonia_0", FULLY_UNLOCKED_VALUE);
        
        if(PlayerPrefs.HasKey(SELECTED_PLAYER_KEY)){
            foreach (CharacterMenuData character in allCharacters)
            {
                if (PlayerPrefs.GetString(SELECTED_PLAYER_KEY).Contains(character.CharacterName))
                {
                    _currentCharacterIndex = System.Array.IndexOf(allCharacters, character);
                    _currentSkinIndex = int.Parse(PlayerPrefs.GetString(SELECTED_PLAYER_KEY).Split('_')[1]);
                    break;
                }
            }
        }
        else
        {
            _currentCharacterIndex = 0;
            _currentSkinIndex = 0;
        }

        UpdateCharacterDisplay();
    }

    public void SelectNextCharacter()
    {
        if (_modelChangeTimer != null) return;
        int newIndex = _currentCharacterIndex + 1;
        if (newIndex >= allCharacters.Length) newIndex = 0;
        ChangeCharacter(newIndex);
    }

    public void SelectPreviousCharacter()
    {
        if (_modelChangeTimer != null) return;
        int newIndex = _currentCharacterIndex - 1;
        if (newIndex < 0) newIndex = allCharacters.Length - 1;
        ChangeCharacter(newIndex);
    }

    public void SelectNextSkin()
    {
        if (_modelChangeTimer != null) return;
        CharacterMenuData character = allCharacters[_currentCharacterIndex];
        _currentSkinIndex = _currentSkinIndex + 1;
        if (_currentSkinIndex >= character.Models.Length) _currentSkinIndex = 0;
        ChangeCurrentDisplayedModel(character);
        ChangePlayerIcon();
    }

    public void SelectPreviousSkin()
    {
        if (_modelChangeTimer != null) return;
        CharacterMenuData character = allCharacters[_currentCharacterIndex];
        _currentSkinIndex = _currentSkinIndex  - 1;
        if (_currentSkinIndex < 0) _currentSkinIndex = character.Models.Length -1;
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
        CharacterMenuData character = allCharacters[_currentCharacterIndex];

        ChangeCurrentDisplayedModel(character);

        ChangePlayerIcon();

        UpdateInformationTexts(character);
    }

    private void UpdateInformationTexts(CharacterMenuData character) // PROVISIONAL
    {
        charName.text = character.CharacterName;
        charDescription.GetComponent<LocalizedText>().LocalizationKey = character.CharacterDescription;
        charDescription.GetComponent<LocalizedText>().Localize();

        charCarWeight.text = Math.Round(Mathf.Clamp01(character.CharacterStats.Weight / 2000f) * 100f).ToString() + "%";
        charCarMaxSpeed.text = Math.Round(Mathf.Clamp01(character.CharacterStats.MaxSpeed / 250f) * 100f).ToString() + "%";
        charCarMaxEndurance.text = Math.Round(Mathf.Clamp01(character.CharacterStats.MaxEndurance / 1500f) * 100f).ToString() + "%";
        charCarBaseForce.text = Math.Round(Mathf.Clamp01(character.CharacterStats.BaseForce / 600000f) * 100f).ToString() + "%";
        charCarManuver.text = Math.Round(Mathf.Clamp01(character.CharacterStats.HandlingSmoothnes / 10f) * 100f).ToString() + "%";

        charCarWeightImage.fillAmount = Mathf.Clamp01(character.CharacterStats.Weight / 2000f);
        charCarMaxSpeedImage.fillAmount = Mathf.Clamp01(character.CharacterStats.MaxSpeed / 250f);
        charCarMaxEnduranceImage.fillAmount = Mathf.Clamp01(character.CharacterStats.MaxEndurance / 1500f);
        charCarBaseForceImage.fillAmount = Mathf.Clamp01(character.CharacterStats.BaseForce / 600000f);
        charCarManuverImage.fillAmount = Mathf.Clamp01(character.CharacterStats.HandlingSmoothnes / 10f);
        
        if(character.CharacterName == "Pepe"){
            charCarAcceleration.text = Math.Round((1f - Mathf.Clamp01(character.CharacterStats.Acceleration / 12000f)) * 100f).ToString() + "%";
            charCarAccelerationImage.fillAmount = 1f - Mathf.Clamp01(character.CharacterStats.Acceleration / 12000f);
        }
        else {
            charCarAcceleration.text = Math.Round(Mathf.Clamp01(character.CharacterStats.Acceleration / 8000f) * 100f).ToString() + "%";
            charCarAccelerationImage.fillAmount = Mathf.Clamp01(character.CharacterStats.Acceleration / 8000f);
        }
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
        {
            GameObject lastModelInstance = _currentModelInstance.gameObject;
            carStopCollider.enabled = false;
            _currentModelInstance.GetComponent<CharSelectionSimulatedMovement>().MoveCarForward();
            _modelChangeTimer = TimerSystem.Instance.CreateTimer(modelChangeTimerDuration, onTimerDecreaseComplete: () =>
            {
                carStopCollider.enabled = true;
                Destroy(lastModelInstance);
                _modelChangeTimer = null;
            });
        }
        _currentModelInstance = Instantiate(character.Models[_currentSkinIndex], modelSpawnPosition.position, Quaternion.identity); // instantiate new model
        _currentModelInstance.name = character.Models[_currentSkinIndex].name;
        _currentModelInstance.GetComponent<CharSelectionSimulatedMovement>().MoveCarForward();
        selectAndStartButton.interactable = CheckIfSkinUnlocked();
    }
    public bool CheckIfSkinUnlocked()
    {
        int skinUnlockedValue = PlayerPrefs.GetInt(_currentModelInstance.name);
        skinUnlockedValueText.text = $"{skinUnlockedValue} / {FULLY_UNLOCKED_VALUE}";
        
        bool isEnabled;
        isEnabled = skinUnlockedValue == FULLY_UNLOCKED_VALUE;
        skinUnlockedLock.SetActive(!isEnabled);

        return isEnabled;
    }

    public void SaveCurrentSelected()
    {
        CharacterMenuData character = allCharacters[_currentCharacterIndex];
        PlayerPrefs.SetString(SELECTED_PLAYER_KEY, character.CharacterName + "_" + _currentSkinIndex);
    }

    public void FullUnlockSkins()
    {
        PlayerPrefs.SetInt("Josefino_1", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Carme_1", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Pepe_1", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("MariaAntonia_1", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Josefino_2", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Carme_2", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("Pepe_2", FULLY_UNLOCKED_VALUE);
        PlayerPrefs.SetInt("MariaAntonia_2", FULLY_UNLOCKED_VALUE);
    }
}
