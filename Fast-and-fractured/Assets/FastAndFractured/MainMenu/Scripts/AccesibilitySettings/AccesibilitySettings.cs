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
        private const string COLORBLIND_KEY = "ColorBlindMode";

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

            bool isColorBlindModeOn = PlayerPrefs.GetInt(COLORBLIND_KEY, 0) == 1;
            colorBlindToggle.isOn = isColorBlindModeOn;

            bool isSubtitlesOn = PlayerPrefs.GetInt(SUBTITLES_KEY, 0) == 1;
            subtitlesToggle.isOn = isSubtitlesOn;
        }

        private void OnEnable()
        {
            nextLenguageButton?.onClick.AddListener(NextLanguage);
            previousLenguageButton?.onClick.AddListener(PreviousLanguage);
            colorBlindToggle?.onValueChanged.AddListener(OnColorBlindToggleChanged);
            subtitlesToggle?.onValueChanged.AddListener(OnSubtitlesToggleChanged);
        }

        private void OnDisable()
        {
            nextLenguageButton?.onClick?.RemoveAllListeners();
            previousLenguageButton?.onClick.RemoveAllListeners();
            colorBlindToggle?.onValueChanged.RemoveAllListeners();
            subtitlesToggle?.onValueChanged.RemoveAllListeners();
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
            PlayerPrefs.SetInt(COLORBLIND_KEY, isOn ? 1 : 0); // 1 true
            PlayerPrefs.Save();
            colorBlindModeController.UpdateColorBlindMode(isOn);

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

