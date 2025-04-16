using TMPro;
using Enums;
using Utilities;
using UnityEngine;
using System.Linq;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.InputSystem.DualShock;

namespace FastAndFractured
{
    public class SettingsMenuBehaviour : MonoBehaviour
    {
        [Header("Menu Settings UI")]
        [SerializeField] private GameObject _audioSettingsUI;
        [SerializeField] private GameObject _videoSettingsUI;
        [SerializeField] private GameObject _accessibilitySettingsUI;

        [SerializeField] private GameObject _gamepadRemappingWindow;
        [SerializeField] private GameObject _keyboardRemappingWindow;

        [Header("Audio Settings")]
        [SerializeField] private Slider _generalVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("Video Settings")]
        [SerializeField] private Toggle _vsyncToggle;
        [SerializeField] private Slider _sharpeningSlider;
        [SerializeField] private Slider _brightnessSlider;
        [SerializeField] private TMP_Dropdown _fpsDropdown;
        [SerializeField] private TMP_Dropdown _resolutionDropdown;
        [SerializeField] private TMP_Dropdown _displayModeDropdown;
        [SerializeField] private TMP_Dropdown _antiAliasingDropdown;
        [SerializeField] private GameObject _sharpeningSliderContainer;

        [Header("Accesibility Settings ")]
        [SerializeField] private TMP_Dropdown _languageDropdown;
        [SerializeField] private TMP_Dropdown _subtitlesDropdown;
        [SerializeField] private TMP_Dropdown _colorblindDropdown;

        [Header("Delete Progress")]
        [SerializeField] private GameObject _deleteButton;
        [SerializeField] private GameObject _deletePopupUI;
        [SerializeField] private List<string> _deletedProgressList = new List<string>();

        #region Player Prefs String Constants
        private const string VSYNC_STRING = "Vsync";
        private const string RESOLUTION_STRING = "Resolution";
        private const string BRIGHTNESS_STRING = "Brightness";
        private const string DISPLAY_MODE_STRING = "DisplayMode";
        private const string ANTI_ALIASING_STRING = "Anti-Aliasing";
        private const string TAA_SHARPENING_STRING = "TAA_Sharpening";
        #endregion

        void Start()
        {
            SetStartValues();

            _fpsDropdown.onValueChanged.AddListener(delegate { CapFPS(_fpsDropdown.value); });
            _resolutionDropdown.onValueChanged.AddListener(delegate { SetResolution(_resolutionDropdown.value); });
            _antiAliasingDropdown.onValueChanged.AddListener(delegate { SetAntiAliasing(_antiAliasingDropdown.value); });
            _colorblindDropdown.onValueChanged.AddListener(delegate { SetColorblind(_colorblindDropdown.value); });
            _generalVolumeSlider.onValueChanged.AddListener(delegate { SetMasterVolume(_generalVolumeSlider.value); });
            _sharpeningSlider.onValueChanged.AddListener(delegate { UpdateTAASharpening(); });
            _musicVolumeSlider.onValueChanged.AddListener(delegate { SetMusicVolume(_musicVolumeSlider.value); });
            _sfxVolumeSlider.onValueChanged.AddListener(delegate { SetSFXVolume(_sfxVolumeSlider.value); });
            _displayModeDropdown.onValueChanged.AddListener(delegate { SetDisplayMode(_displayModeDropdown.value); });

            _brightnessSlider.onValueChanged.AddListener(delegate { SetBrightness(); });

            _vsyncToggle.onValueChanged.AddListener(delegate { ToggleVsync(_vsyncToggle.isOn); });

            if (SceneManager.GetActiveScene().buildIndex != 0)
            {
                _deleteButton.SetActive(false);
            }
            else
            {
                _deleteButton.SetActive(true);
            }
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
            string resolution = PlayerPrefs.GetString(RESOLUTION_STRING, "1920x1080");
            RefreshValue(_resolutionDropdown, resolution);
            LoadAvailableResolutions();

            //Display Mode
            LoadDisplayModeOptions();

            //VSync
            bool vsyncOn = PlayerPrefs.GetInt(VSYNC_STRING, 0) == 1;
            _vsyncToggle.isOn = vsyncOn;
            UpdateVSync(vsyncOn);

            //Anti-Aliasing
            LoadAntiAliasingOptions();

            //Sharpening
            if (PlayerPrefs.HasKey(TAA_SHARPENING_STRING))
            {
                float savedSharpening = PlayerPrefs.GetFloat(TAA_SHARPENING_STRING, 0.5f);
                _sharpeningSlider.SetValueWithoutNotify(savedSharpening);
            }

            //Brightness
            float brightness = PlayerPrefs.GetFloat(BRIGHTNESS_STRING, 1f);
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

        public void OpenAudioSettings()
        {
            _audioSettingsUI.SetActive(true);
            _videoSettingsUI.SetActive(false);
            _accessibilitySettingsUI.SetActive(false);
            _gamepadRemappingWindow.SetActive(false);
            _keyboardRemappingWindow.SetActive(false);
        }
        public void OpenVideoSettings()
        {
            _audioSettingsUI.SetActive(false);
            _videoSettingsUI.SetActive(true);
            _accessibilitySettingsUI.SetActive(false);
            _gamepadRemappingWindow.SetActive(false);
            _keyboardRemappingWindow.SetActive(false);

            float brightness = PlayerPrefs.GetFloat("Brightness", 1f);
            _brightnessSlider.SetValueWithoutNotify(brightness);
        }

        public void OpenAccesibilitySettings()
        {
            _audioSettingsUI.SetActive(false);
            _videoSettingsUI.SetActive(false);
            _accessibilitySettingsUI.SetActive(true);
            _gamepadRemappingWindow.SetActive(false);
            _keyboardRemappingWindow.SetActive(false);
        }

        public void OpenKeyboardRemapping()
        {
            _audioSettingsUI.SetActive(false);
            _videoSettingsUI.SetActive(false);
            _accessibilitySettingsUI.SetActive(false);
            _gamepadRemappingWindow.SetActive(false);
            _keyboardRemappingWindow.SetActive(true);
        }

        public void OpenControllerRemapping()
        {
            _audioSettingsUI.SetActive(false);
            _videoSettingsUI.SetActive(false);
            _accessibilitySettingsUI.SetActive(false);
            _gamepadRemappingWindow.SetActive(true);
            _keyboardRemappingWindow.SetActive(false);
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

        #region Accesibility settings
        private void SetColorblind(int option)
        {
            string selectedOption = _colorblindDropdown.options[option].text;
            PlayerPrefs.SetString("Colorblind", selectedOption);
            PlayerPrefs.Save();
            //TODO set colorblind in game
        }

        #endregion

        #region Video Settings
        public void SetBrightness()
        {
            BrightnessManager.Instance?.SetBrightness(_brightnessSlider.value);
        }

        private void UpdateVSync(bool isActive)
        {
            QualitySettings.vSyncCount = isActive ? 1 : 0;
            Application.targetFrameRate = isActive ? -1 : 60;

            PlayerPrefs.SetInt(VSYNC_STRING, isActive ? 1 : 0);
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

        #region Anti-Aliasing and Sharpening Methods
        private void LoadAntiAliasingOptions()
        {
            List<string> antiAliasingOptions = new List<string> { "None", "FXAA", "TAA" };

            _antiAliasingDropdown.ClearOptions();
            _antiAliasingDropdown.AddOptions(antiAliasingOptions);

            string savedOption = PlayerPrefs.GetString(ANTI_ALIASING_STRING, "None");
            int index = antiAliasingOptions.IndexOf(savedOption);

            if (index >= 0)
                _antiAliasingDropdown.value = index;
            else
                _antiAliasingDropdown.value = 0;

            _antiAliasingDropdown.RefreshShownValue();
        }

        private void SetAntiAliasing(int dropdownIndex)
        {
            string selectedOption = _antiAliasingDropdown.options[dropdownIndex].text;
            PlayerPrefs.SetString(ANTI_ALIASING_STRING, selectedOption);
            PlayerPrefs.Save();

            ApplyAntiAliasing(selectedOption);

            bool showSharpening = selectedOption == "TAA";
            _sharpeningSliderContainer.SetActive(showSharpening);

            if (showSharpening)
            {
                Camera camera = Camera.main;

                if (camera != null)
                {
                    HDAdditionalCameraData hdCameraData = camera.GetComponent<HDAdditionalCameraData>();

                    if (hdCameraData != null)
                        _sharpeningSlider.SetValueWithoutNotify(hdCameraData.taaSharpenStrength);
                }
            }
        }

        private void ApplyAntiAliasing(string selectedOption)
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            HDAdditionalCameraData hdCameraData = camera.GetComponent<HDAdditionalCameraData>();
            if (hdCameraData == null) return;

            switch (selectedOption)
            {
                case "None":
                    hdCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                    break;
                case "FXAA":
                    hdCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case "TAA":
                    hdCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                    float savedSharpening = PlayerPrefs.GetFloat(TAA_SHARPENING_STRING, 0.5f);
                    hdCameraData.taaSharpenStrength = savedSharpening;
                    break;
                default:
                    hdCameraData.antialiasing = HDAdditionalCameraData.AntialiasingMode.None;
                    break;
            }

            Debug.Log("Applied AntiAliasing: " + hdCameraData.antialiasing);
        }

        private void UpdateTAASharpening()
        {
            Camera camera = Camera.main;
            if (camera == null) return; 

            HDAdditionalCameraData hdCameraData = camera.GetComponent<HDAdditionalCameraData>();
            if (hdCameraData == null) return;   

            if(hdCameraData.antialiasing == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing)
            {
                hdCameraData.taaSharpenStrength = _sharpeningSlider.value;
                PlayerPrefs.SetFloat(TAA_SHARPENING_STRING, _sharpeningSlider.value);
                PlayerPrefs.Save();
            }
        }
        #endregion

        #region Resolution Methods
        private void SetResolution(int option)
        {
            string selectedOption = _resolutionDropdown.options[option].text;
            PlayerPrefs.SetString(RESOLUTION_STRING, selectedOption);
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

            string savedResolution = PlayerPrefs.GetString(RESOLUTION_STRING, "");
            if (!string.IsNullOrEmpty(savedResolution))
            {
                int index = optionsList.IndexOf(savedResolution);
                _resolutionDropdown.value = index != -1 ? index : currentResolutionIndex;
            }
            else
                _resolutionDropdown.value = currentResolutionIndex;

            _resolutionDropdown.RefreshShownValue();
        }
        #endregion

        #region Display Mode Methods
        private void SetDisplayMode(int option)
        {
            string selectedOption = _displayModeDropdown.options[option].text;
            PlayerPrefs.SetString(DISPLAY_MODE_STRING, selectedOption);
            PlayerPrefs.Save();

            ApplyDisplayMode(selectedOption);
        }

        private void ApplyDisplayMode(string selectedOption)
        {
            switch (selectedOption)
            {
                case "Full Screen":
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    break;
                case "Window":
                    Screen.fullScreenMode = FullScreenMode.Windowed;
                    break;
                case "Window without borders":
                    Screen.fullScreenMode = FullScreenMode.FullScreenWindow;
                    break;
                default:
                    Screen.fullScreenMode = FullScreenMode.ExclusiveFullScreen;
                    break;
            }
            Debug.Log($"Display Mode Applicated: {Screen.fullScreenMode}");
        }

        private void LoadDisplayModeOptions()
        {
            List<string> displayModes = new List<string> { "Full Screen", "Window", "Window without borders" };

            _displayModeDropdown.ClearOptions();
            _displayModeDropdown.AddOptions(displayModes);

            string savedMode = PlayerPrefs.GetString(DISPLAY_MODE_STRING, "Full Screen");
            int index = displayModes.IndexOf(savedMode);
            _displayModeDropdown.value = index >= 0 ? index : 0;
            _displayModeDropdown.RefreshShownValue();
        }
        #endregion

        #endregion

        #region Delete Progress Methods
        public void DeleteAllProgress()
        {
            _deletePopupUI.SetActive(false);
            for (int i = 0; i < _deletedProgressList.Count; i++)
            {
                PlayerPrefs.DeleteKey(_deletedProgressList[i]);
            }
            PlayerPrefs.Save();
        }

        public void CloseDeletePopup()
        {
            _deletePopupUI.SetActive(false);
        }

        public void OpenDeletePopup()
        {
            _deletePopupUI.SetActive(true);
        }

        #endregion
    }
}