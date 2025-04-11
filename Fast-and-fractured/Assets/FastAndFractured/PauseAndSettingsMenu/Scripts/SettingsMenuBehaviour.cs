using TMPro;
using Utilities;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;

namespace FastAndFractured
{
    public class SettingsMenuBehaviour : MonoBehaviour
    {
        [Header("Menu Settings UI")]
        [SerializeField] private GameObject _audioSettingsUI;
        [SerializeField] private GameObject _videoSettingsUI;
        [SerializeField] private GameObject _accessibilitySettingsUI;

        [Header("Audio Settings")]
        [SerializeField] private Slider _generalVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("Video Settings")]
        [SerializeField] private Toggle _vsyncToggle;
        [SerializeField] private Volume _globalVolume;
        [SerializeField] private Slider _brightnessSlider;
        [SerializeField] private TMP_Dropdown _fpsDropdown;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private TMP_Dropdown _antiAliasingDropdown;
        [SerializeField] private TMP_Dropdown _sharpeningDropdown;

        private LiftGammaGain _liftGammaGain;

        [Header("Accesibility Settings ")]
        [SerializeField] private TMP_Dropdown _colorblindDropdown;
        [SerializeField] private TMP_Dropdown _languageDropdown;
        [SerializeField] private TMP_Dropdown _subtitlesDropdown;

        void Start()
        {
            SetStartValues();
            _fpsDropdown.onValueChanged.AddListener(delegate { CapFPS(_fpsDropdown.value); });
            _resolutionDropdown.onValueChanged.AddListener(delegate { SetResolution(_resolutionDropdown.value); });
            _antiAliasingDropdown.onValueChanged.AddListener(delegate { SetAntiAliasing(_antiAliasingDropdown.value); });
            _sharpeningDropdown.onValueChanged.AddListener(delegate { SetSharpening(_sharpeningDropdown.value); });
            _colorblindDropdown.onValueChanged.AddListener(delegate { SetColorblind(_colorblindDropdown.value); });
            _languageDropdown.onValueChanged.AddListener(delegate { SetLanguage(_languageDropdown.value); });
            _subtitlesDropdown.onValueChanged.AddListener(delegate { SetSubtitles(_subtitlesDropdown.value); });
            _generalVolumeSlider.onValueChanged.AddListener(delegate { SetMasterVolume(_generalVolumeSlider.value); });
            _musicVolumeSlider.onValueChanged.AddListener(delegate { SetMusicVolume(_musicVolumeSlider.value); });
            _sfxVolumeSlider.onValueChanged.AddListener(delegate { SetSFXVolume(_sfxVolumeSlider.value); });

            _brightnessSlider.onValueChanged.AddListener(delegate { SetBrightness(_brightnessSlider.value); });

            _vsyncToggle.onValueChanged.AddListener(delegate { ToggleVsync(_vsyncToggle.isOn); });
        }

        private void SetStartValues()
        {
            //Master volume
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.5f);
            RefreshValue(_generalVolumeSlider, masterVolume);

            //Music volume 
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            RefreshValue(_musicVolumeSlider, musicVolume);

            //SFX volume
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
            RefreshValue(_sfxVolumeSlider, sfxVolume);

            // Max FPS
            int maxFPS = PlayerPrefs.GetInt("MaxFPS", 120);
            string maxFPSString = maxFPS.ToString();
            RefreshValue(_fpsDropdown, maxFPSString);

            //Resolution
            string resolution = PlayerPrefs.GetString("Resolution", "1920x1080");
            RefreshValue(_resolutionDropdown, resolution);
            LoadAvailableResolutions();

            //VSync
            bool vsyncOn = PlayerPrefs.GetInt("Vsync", 0) == 1;
            _vsyncToggle.isOn = vsyncOn; 
            UpdateVSync(vsyncOn);

            //Anti-Aliasing
            string antiAliasing = PlayerPrefs.GetString("Anti-Aliasing", "No");
            RefreshValue(_antiAliasingDropdown, antiAliasing);

            //Sharpening
            string sharpening = PlayerPrefs.GetString("Sharpening", "No");
            RefreshValue(_sharpeningDropdown, sharpening);

            //Brightness
            float brightness = PlayerPrefs.GetFloat("Brightness", 1f);
            RefreshValue(_brightnessSlider, brightness);

            //Colorblind
            string colorblind = PlayerPrefs.GetString("Colorblind", "No");
            RefreshValue(_colorblindDropdown, colorblind);

            //Language
            string language = PlayerPrefs.GetString("Language", "Español");
            RefreshValue(_languageDropdown, language);

            //Subtitles
            string subtitles = PlayerPrefs.GetString("Subtitles", "No");
            RefreshValue(_subtitlesDropdown, subtitles);
        }
        private void RefreshValue(TMP_Dropdown dropdown, string value)
        {
            for (int i = 0; i < dropdown.options.Count; i++)
            {
                if (dropdown.options[i].text == value)
                {
                    dropdown.value = i;
                    dropdown.RefreshShownValue();
                    break;
                }
            }
        }
        private void RefreshValue(Slider slider, float value)
        {
            slider.value = value;
        }

        //Change between settings ui
        public void OpenAudioSettings()
        {
            _audioSettingsUI.SetActive(true);
            _videoSettingsUI.SetActive(false);
            _accessibilitySettingsUI.SetActive(false);
        }
        public void OpenVideoSettings()
        {
            _audioSettingsUI.SetActive(false);
            _videoSettingsUI.SetActive(true);
            _accessibilitySettingsUI.SetActive(false);
        }
        public void OpenAccesibilitySettings()
        {
            _audioSettingsUI.SetActive(false);
            _videoSettingsUI.SetActive(false);
            _accessibilitySettingsUI.SetActive(true);
        }

        #region Audio Settings
        private void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat("MasterVolume", value);
            PlayerPrefs.Save();
            SoundManager.Instance.UpdateGeneralVolume();
        }
        private void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            PlayerPrefs.Save();
            SoundManager.Instance.UpdateMusicVolume();
        }
        private void SetSFXVolume(float value)
        {
            PlayerPrefs.SetFloat("SFXVolume", value);
            PlayerPrefs.Save();
            SoundManager.Instance.UpdateSFXVolume();
        }
        #endregion

        #region Video settings
        private void SetBrightness(float value)
        {
            PlayerPrefs.SetFloat("Brightness", value);
            PlayerPrefs.Save();
            //TODO set brightness in game
        }

        private void UpdateVSync(bool isActive)
        {
            QualitySettings.vSyncCount = isActive ? 1 : 0;
            Application.targetFrameRate = isActive ? -1 : 60;

            PlayerPrefs.SetInt("Vsync", isActive ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void ToggleVsync(bool value)
        {
            UpdateVSync(value);
        }

        private void CapFPS(int option)
        {
            string selectedOption = _fpsDropdown.options[option].text;
            int maxFPS = int.Parse(selectedOption);
            Application.targetFrameRate = maxFPS;
            PlayerPrefs.SetInt("MaxFPS", maxFPS);
            PlayerPrefs.Save();
        }

        private void SetAntiAliasing(int option)
        {
            string selectedOption = _antiAliasingDropdown.options[option].text;
            PlayerPrefs.SetString("Anti-Aliasing", selectedOption);
            PlayerPrefs.Save();
            //TODO set anti-aliasing in game
        }

        private void SetResolution(int option)
        {
            string selectedOption = _resolutionDropdown.options[option].text;
            PlayerPrefs.SetString("Resolution", selectedOption);
            PlayerPrefs.Save();

            string[] dimensions = selectedOption.Split('x');
            int width = int.Parse(dimensions[0]);
            int height = int.Parse(dimensions[1]);
            Screen.SetResolution(width, height, Screen.fullScreen);
        }

        private void LoadAvailableResolutions()
        {
            _resolutionDropdown.ClearOptions();

            Resolution[] resolutionList = Screen.resolutions
                .OrderByDescending(r => r.width * r.height)
                .ToArray();

            List<string> optionsList = new List<string>();
            int currentResolutionIndex = 0;

            for (int i = 0; i < resolutionList.Length; i++)
            {
                string option = $"{resolutionList[i].width}x{resolutionList[i].height}";

                if (!optionsList.Contains(option))
                    optionsList.Add(option);

                if (resolutionList[i].width == Screen.currentResolution.width &&
                    resolutionList[i].height == Screen.currentResolution.height)
                    currentResolutionIndex = optionsList.Count - 1;
            }

            _resolutionDropdown.AddOptions(optionsList);

            string savedResolution = PlayerPrefs.GetString("Resolution", "");
            if (!string.IsNullOrEmpty(savedResolution))
            {
                int index = optionsList.IndexOf(savedResolution);
                _resolutionDropdown.value = index != -1 ? index : currentResolutionIndex;
            }
            else
                _resolutionDropdown.value = currentResolutionIndex;

            _resolutionDropdown.RefreshShownValue();
        }

        private void SetSharpening(int option)
        {
            string selectedOption = _sharpeningDropdown.options[option].text;
            PlayerPrefs.SetString("Sharpening", selectedOption);
            PlayerPrefs.Save();
            //TODO set sharpening in game
        }
        #endregion

        #region Accesibility settings
        private void SetColorblind(int option)
        {
            string selectedOption = _colorblindDropdown.options[option].text;
            PlayerPrefs.SetString("Colorblind", selectedOption);
            PlayerPrefs.Save();
            //TODO set colorblind in game
        }
        private void SetLanguage(int option)
        {
            string selectedOption = _languageDropdown.options[option].text;
            PlayerPrefs.SetString("Language", selectedOption);
            PlayerPrefs.Save();
            //TODO set language in game
        }
        private void SetSubtitles(int option)
        {
            string selectedOption = _subtitlesDropdown.options[option].text;
            PlayerPrefs.SetString("Subtitles", selectedOption);
            PlayerPrefs.Save();
            //TODO set subtitles in game
        }
        public void OpenKeyboardRemapping()
        {
            //TODO
        }
        public void OpenControllerRemapping()
        {
            //TODO
        }

        #endregion
    }
}