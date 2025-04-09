using Assets.SimpleLocalization.Scripts;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured
{
    public class AccesibilitySettings : MonoBehaviour
    {
        [Header("Lenguage")]
        [SerializeField] private Button nextLenguageButton;
        [SerializeField] private Button previousLenguageButton;
        private List<string> _languages = new List<string>();
        private int _languageIndex = 0;
        private const string LEANGUAGE_INDEX_KEY = "LanguageIndex";

        [Header("ColorBlind")]
        [SerializeField] ColorBlindModeController colorBlindModeController;
        [SerializeField] private Toggle colorBlindToggle;
        [SerializeField] private Button nextColorblindModeButton;
        [SerializeField] private Button previousColorblindModeButton;
        [SerializeField] private TextMeshProUGUI colorBlindTypeText;
        private int _colorblindModeIndex = 0;
        private const int COLORBLIND_MODES_COUNT = 3;
        private const string COLORBLIND_KEY = "ColorBlindMode";
        private const string COLORBLIND_INDEX_KEY = "ColorBlindModeIndex";
        private bool _isColorBlindModeOn = false;

        [Header("Subtitles")]
        [SerializeField] private Toggle subtitlesToggle;
        private const string SUBTITLES_KEY = "Substitles";


        private void Awake()
        {
            // if we decide to add more lenguages we can create a public array to fill all lenguages
            _languages.Add("Catala");
            _languages.Add("Espanol");
            _languages.Add("English");

            if (PlayerPrefs.HasKey(LEANGUAGE_INDEX_KEY))
            {
                _languageIndex = PlayerPrefs.GetInt(LEANGUAGE_INDEX_KEY);
            }
            LocalizationManager.Read();
            SelectLanguage();

            _isColorBlindModeOn = PlayerPrefs.GetInt(COLORBLIND_KEY, 0) == 1;
            _colorblindModeIndex = PlayerPrefs.GetInt(COLORBLIND_INDEX_KEY);
            colorBlindToggle.isOn = _isColorBlindModeOn;
            UpdateColorblindModeText();
            HandleColorblindButtons();

            bool isSubtitlesOn = PlayerPrefs.GetInt(SUBTITLES_KEY, 0) == 1;
            subtitlesToggle.isOn = isSubtitlesOn;
        }

        private void OnEnable()
        {
            nextLenguageButton?.onClick.AddListener(NextLanguage);
            previousLenguageButton?.onClick.AddListener(PreviousLanguage);
            colorBlindToggle?.onValueChanged.AddListener(OnColorBlindToggleChanged);
            subtitlesToggle?.onValueChanged.AddListener(OnSubtitlesToggleChanged);
            previousColorblindModeButton?.onClick.AddListener(PreviousColorblindMode);
            nextColorblindModeButton?.onClick.AddListener(NextColorblindMode);
        }

        private void OnDisable()
        {
            nextLenguageButton?.onClick?.RemoveAllListeners();
            previousLenguageButton?.onClick.RemoveAllListeners();
            colorBlindToggle?.onValueChanged.RemoveAllListeners();
            subtitlesToggle?.onValueChanged.RemoveAllListeners();
            previousColorblindModeButton?.onClick.RemoveAllListeners();
            nextColorblindModeButton?.onClick.RemoveAllListeners();
            PlayerPrefs.Save();
        }

        #region lenguage
        private void SelectLanguage()
        {
            switch (_languageIndex)
            {
                case 0:
                    LocalizationManager.Language = "English";
                    break;
                case 1:
                    LocalizationManager.Language = "Espanol";
                    break;
                case 2:
                    LocalizationManager.Language = "Catala";
                    break;
            }
        }

        public void NextLanguage()
        {
            _languageIndex++;
            if (_languageIndex >= _languages.Count)
            {
                _languageIndex = 0;
            }
            SelectLanguage();
            PlayerPrefs.SetInt(LEANGUAGE_INDEX_KEY, _languageIndex);
        }
        public void PreviousLanguage()
        {
            _languageIndex--;
            if (_languageIndex < 0)
            {
                _languageIndex = _languages.Count - 1;
            }
            SelectLanguage();
            PlayerPrefs.SetInt(LEANGUAGE_INDEX_KEY, _languageIndex);
        }
        #endregion

        #region Color Blind

        private void OnColorBlindToggleChanged(bool isOn)
        {
            _isColorBlindModeOn = isOn;
            PlayerPrefs.SetInt(COLORBLIND_KEY, isOn ? 1 : 0); // 1 true
            PlayerPrefs.Save();
            NotifyColorblindMode();
            HandleColorblindButtons();
        }

        public void NextColorblindMode()
        {
            _colorblindModeIndex++;
            if (_colorblindModeIndex >= COLORBLIND_MODES_COUNT)
            {
                _colorblindModeIndex = 0;
            }
            colorBlindModeController.UpdateColorblindModeIndex(_colorblindModeIndex);
            NotifyColorblindMode();
            PlayerPrefs.SetInt(COLORBLIND_INDEX_KEY, _colorblindModeIndex);
        }

        public void PreviousColorblindMode()
        {
            _colorblindModeIndex--;
            if (_languageIndex < 0)
            {
                _colorblindModeIndex = COLORBLIND_MODES_COUNT - 1;
            }
            colorBlindModeController.UpdateColorblindModeIndex(_colorblindModeIndex);
            NotifyColorblindMode();
            PlayerPrefs.SetInt(COLORBLIND_INDEX_KEY, _colorblindModeIndex);
        }

        private void NotifyColorblindMode()
        {
            colorBlindModeController.UpdateColorblindModeIndex(_colorblindModeIndex);
            colorBlindModeController.UpdateColorBlindMode(_isColorBlindModeOn);
            UpdateColorblindModeText();
        }

        private void HandleColorblindButtons()
        {
            if (!_isColorBlindModeOn)
            {
                nextColorblindModeButton.enabled = false;
                previousColorblindModeButton.enabled = false;
            }
            else
            {
                nextColorblindModeButton.enabled = true;
                previousColorblindModeButton.enabled = true;
            }
        }

        public void UpdateColorblindModeText()
        {
            switch(_colorblindModeIndex)
            {
                case 0:
                    colorBlindTypeText.text = "Protanopia";
                    break;

                case 1:
                    colorBlindTypeText.text = "Deuteranopia";
                    break;

                case 2:
                    colorBlindTypeText.text = "Tritanopia";
                    break;
            }
        }


        #endregion

        #region Substitles

        private void OnSubtitlesToggleChanged(bool isOn)
        {
            PlayerPrefs.SetInt(SUBTITLES_KEY, isOn ? 1 : 0);
            PlayerPrefs.Save();
            SubtitlesManager.Instance.ToggleSubtitles(isOn);
        }
        #endregion
    }
}

