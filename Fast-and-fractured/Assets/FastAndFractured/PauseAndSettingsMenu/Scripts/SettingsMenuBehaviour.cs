using Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utilities;

namespace FastAndFractured
{
    public class SettingsMenuBehaviour : MonoBehaviour
    {
        [Header("Settings Buttons")]
        [SerializeField] private Button audioSettingsButton;
        [SerializeField] private Button videoSettingsButton;
        [SerializeField] private Button accessibilitySettingsButton;

        [Header("Menu Settings UI")]
        [SerializeField] private GameObject audioSettingsUI;
        [SerializeField] private GameObject videoSettingsUI;
        [SerializeField] private GameObject accessibilitySettingsUI;

        [SerializeField] private GameObject gamepadRemappingWindow;
        [SerializeField] private GameObject keyboardRemappingWindow;

        [Header("Audio Settings")]
        [SerializeField] private Slider generalVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Video Settings")]
        [SerializeField] private Toggle vsyncToggle;
        [SerializeField] private Slider sharpeningSlider;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private TMP_Dropdown fpsDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown displayModeDropdown;
        [SerializeField] private TMP_Dropdown antiAliasingDropdown;
        [SerializeField] private GameObject sharpeningSliderContainer;

        [Header("Upscaling")]
        [SerializeField] private TMP_Dropdown upscalingDropdown;
        [SerializeField] private TMP_Dropdown upscalingResolutionDropdown;


        [Header("Delete Progress")]
        [SerializeField] private GameObject deleteButton;
        [SerializeField] private GameObject deletePopupUI;
        [SerializeField] private List<string> deletedProgressList = new List<string>();

        #region Player Prefs String Constants
        private const string VSYNC_STRING = "Vsync";
        private const string RESOLUTION_STRING = "Resolution";
        private const string BRIGHTNESS_STRING = "Brightness";
        private const string DISPLAY_MODE_STRING = "DisplayMode";
        private const string ANTI_ALIASING_STRING = "Anti-Aliasing";
        private const string TAA_SHARPENING_STRING = "TAA_Sharpening";
        private const float MASTER_VOLUME_DEFAULT = 0.5f;
        private const int VSYNC_DEFAULT_STATE = 0;
        private const float SHARPENING_DEFAULT = 0.5f;
        private const float BRIGHTNESS_DEFAULT = 0.5f;
        private const int FPS_DEFAULT = 60;
        private const string RESOLUTION_DEFAULT = "1920x1080";
        #endregion

        Camera _camera;
        HDAdditionalCameraData _hdAdditionalCameraData;

        private MenuScreen _menuScreen;

        void Start()
        {
            SetStartValues();
            _menuScreen = GetComponent<MenuScreen>();
            SetDefaultSelectedButton();

            fpsDropdown.onValueChanged.AddListener(delegate { CapFPS(fpsDropdown.value); });
            resolutionDropdown.onValueChanged.AddListener(delegate { SetResolution(resolutionDropdown.value); });
            antiAliasingDropdown.onValueChanged.AddListener(delegate { SetAntiAliasing(antiAliasingDropdown.value); });
            generalVolumeSlider.onValueChanged.AddListener(delegate { SetMasterVolume(generalVolumeSlider.value); });
            sharpeningSlider.onValueChanged.AddListener(delegate { UpdateTAASharpening(); });
            musicVolumeSlider.onValueChanged.AddListener(delegate { SetMusicVolume(musicVolumeSlider.value); });
            sfxVolumeSlider.onValueChanged.AddListener(delegate { SetSFXVolume(sfxVolumeSlider.value); });
            displayModeDropdown.onValueChanged.AddListener(delegate { SetDisplayMode(displayModeDropdown.value); });
            upscalingDropdown.onValueChanged.AddListener(delegate { SetUpscalingMode(upscalingDropdown.value); });
            upscalingResolutionDropdown.onValueChanged.AddListener(delegate { SetUpscalingResolutionMode(DictionaryLibrary.TranslationDynamicResolution[upscalingResolutionDropdown.value]); });

            brightnessSlider.onValueChanged.AddListener(delegate { SetBrightness(); });

            vsyncToggle.onValueChanged.AddListener(delegate { ToggleVsync(vsyncToggle.isOn); });

            if (SceneManager.GetActiveScene().buildIndex != 1)
            {
                deleteButton.SetActive(false);
            }
            else
            {
                deleteButton.SetActive(true);
            }
            
        }

        void OnEnable()
        {
            SetDefaultSelectedButton();
        }

        private void SetDefaultSelectedButton()
        {

            if (audioSettingsUI.activeSelf) { _menuScreen.defaultInteractable = audioSettingsButton; audioSettingsButton.onClick.Invoke(); return; }
            if (videoSettingsUI.activeSelf) { _menuScreen.defaultInteractable = videoSettingsButton; videoSettingsButton.onClick.Invoke(); return; }
            if (accessibilitySettingsUI.activeSelf) { _menuScreen.defaultInteractable = accessibilitySettingsButton; accessibilitySettingsButton.onClick.Invoke(); return; }

        }

        private void SetStartValues()
        {
            _camera = Camera.main;
            _hdAdditionalCameraData = _camera.GetComponent<HDAdditionalCameraData>();

            //Master volume
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", MASTER_VOLUME_DEFAULT);
            RefreshValue(generalVolumeSlider, masterVolume);

            //Music volume 
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            RefreshValue(musicVolumeSlider, musicVolume);

            //SFX volume
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
            RefreshValue(sfxVolumeSlider, sfxVolume);

            // Max FPS
            int maxFPS = PlayerPrefs.GetInt("MaxFPS", FPS_DEFAULT);
            string maxFPSString = maxFPS.ToString();
            RefreshValue(fpsDropdown, maxFPSString);

            //Resolution
            string resolution = PlayerPrefs.GetString(RESOLUTION_STRING, "1920x1080");
            RefreshValue(resolutionDropdown, resolution);
            LoadAvailableResolutions();

            //Display Mode
            LoadDisplayModeOptions();

            //VSync
            bool vsyncOn = PlayerPrefs.GetInt(VSYNC_STRING, VSYNC_DEFAULT_STATE) == 1;
            vsyncToggle.isOn = vsyncOn;
            UpdateVSync(vsyncOn);

            //Anti-Aliasing
            LoadAntiAliasingOptions();

            //Resolution & Upscaling
            LoadUpscalingResolutionOptions();
            LoadUpscalingOptions();

            //Sharpening
            if (PlayerPrefs.HasKey(TAA_SHARPENING_STRING))
            {
                float savedSharpening = PlayerPrefs.GetFloat(TAA_SHARPENING_STRING, SHARPENING_DEFAULT);
                sharpeningSlider.SetValueWithoutNotify(savedSharpening);
            }

            //Brightness
            float brightness = PlayerPrefs.GetFloat(BRIGHTNESS_STRING, BRIGHTNESS_DEFAULT);
            RefreshValue(brightnessSlider, brightness);
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
            audioSettingsUI.SetActive(true);
            videoSettingsUI.SetActive(false);
            accessibilitySettingsUI.SetActive(false);
            gamepadRemappingWindow.SetActive(false);
            keyboardRemappingWindow.SetActive(false);
        }
        public void OpenVideoSettings()
        {
            audioSettingsUI.SetActive(false);
            videoSettingsUI.SetActive(true);
            accessibilitySettingsUI.SetActive(false);
            gamepadRemappingWindow.SetActive(false);
            keyboardRemappingWindow.SetActive(false);

            float brightness = PlayerPrefs.GetFloat("Brightness", BRIGHTNESS_DEFAULT);
            brightnessSlider.SetValueWithoutNotify(brightness);
        }

        public void OpenAccesibilitySettings()
        {
            audioSettingsUI.SetActive(false);
            videoSettingsUI.SetActive(false);
            accessibilitySettingsUI.SetActive(true);
            gamepadRemappingWindow.SetActive(false);
            keyboardRemappingWindow.SetActive(false);
        }

        public void OpenKeyboardRemapping()
        {
            audioSettingsUI.SetActive(false);
            videoSettingsUI.SetActive(false);
            accessibilitySettingsUI.SetActive(false);
            gamepadRemappingWindow.SetActive(false);
            keyboardRemappingWindow.SetActive(true);
        }

        public void OpenControllerRemapping()
        {
            audioSettingsUI.SetActive(false);
            videoSettingsUI.SetActive(false);
            accessibilitySettingsUI.SetActive(false);
            gamepadRemappingWindow.SetActive(true);
            keyboardRemappingWindow.SetActive(false);
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

        #region Video Settings
        public void SetBrightness()
        {
            BrightnessManager.Instance?.SetBrightness(brightnessSlider.value);
        }

        private void UpdateVSync(bool isActive)
        {
            QualitySettings.vSyncCount = isActive ? 1 : VSYNC_DEFAULT_STATE;
            Application.targetFrameRate = isActive ? -1 : PlayerPrefs.GetInt("MaxFPS", FPS_DEFAULT);

            PlayerPrefs.SetInt(VSYNC_STRING, QualitySettings.vSyncCount);
            PlayerPrefs.Save();
        }

        private void ToggleVsync(bool value)
        {
            UpdateVSync(value);
        }

        private void CapFPS(int option)
        {
            string selectedOption = fpsDropdown.options[option].text;
            int maxFPS = int.Parse(selectedOption);
            Application.targetFrameRate = maxFPS;
            PlayerPrefs.SetInt("MaxFPS", maxFPS);
            PlayerPrefs.Save();
        }

        #region Anti-Aliasing and Sharpening Methods
        private void LoadAntiAliasingOptions()
        {
            List<string> antiAliasingOptions = new List<string> {
                Enum.GetName(typeof(HDAdditionalCameraData.AntialiasingMode),HDAdditionalCameraData.AntialiasingMode.None),
                Enum.GetName(typeof(HDAdditionalCameraData.AntialiasingMode),HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing),
                Enum.GetName(typeof(HDAdditionalCameraData.AntialiasingMode),HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing),
                Enum.GetName(typeof(HDAdditionalCameraData.AntialiasingMode),HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing) };
            antiAliasingDropdown.ClearOptions();
            antiAliasingDropdown.AddOptions(antiAliasingOptions);

            string savedOption = PlayerPrefs.GetString(ANTI_ALIASING_STRING, Enum.GetName(typeof(HDAdditionalCameraData.AntialiasingMode), HDAdditionalCameraData.AntialiasingMode.None));
            int index = antiAliasingOptions.IndexOf(savedOption);

            if (index >= 0)
                antiAliasingDropdown.value = index;
            else
                antiAliasingDropdown.value = 0;

            antiAliasingDropdown.RefreshShownValue();
        }

        private void SetAntiAliasing(int dropdownIndex)
        {
            string selectedOption = antiAliasingDropdown.options[dropdownIndex].text;
            PlayerPrefs.SetString(ANTI_ALIASING_STRING, selectedOption);
            PlayerPrefs.Save();
            HDAdditionalCameraData.AntialiasingMode antialiasingMode = (HDAdditionalCameraData.AntialiasingMode)Enum.Parse(typeof(HDAdditionalCameraData.AntialiasingMode), selectedOption);
            ApplyAntiAliasing(antialiasingMode);

            bool showSharpening = antialiasingMode == HDAdditionalCameraData.AntialiasingMode.None;
            sharpeningSliderContainer.SetActive(showSharpening);

            if (showSharpening)
            {

                if (_camera != null)
                {
                    HDAdditionalCameraData hdCameraData = _camera.GetComponent<HDAdditionalCameraData>();

                    if (hdCameraData != null)
                        sharpeningSlider.SetValueWithoutNotify(hdCameraData.taaSharpenStrength);
                }
            }

        }

        private void ApplyAntiAliasing(HDAdditionalCameraData.AntialiasingMode selectedOption)
        {
            if (_camera == null) return;

            HDAdditionalCameraData hdCameraData = _camera.GetComponent<HDAdditionalCameraData>();
            if (hdCameraData == null) return;

            hdCameraData.antialiasing = selectedOption;

            if (selectedOption == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing)
            {
                float savedSharpening = PlayerPrefs.GetFloat(TAA_SHARPENING_STRING, SHARPENING_DEFAULT);
                hdCameraData.taaSharpenStrength = savedSharpening;
            }
        }

        private void UpdateTAASharpening()
        {
            if (_camera == null) return;

            HDAdditionalCameraData hdCameraData = _camera.GetComponent<HDAdditionalCameraData>();
            if (hdCameraData == null) return;

            if (hdCameraData.antialiasing == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing)
            {
                hdCameraData.taaSharpenStrength = sharpeningSlider.value;
                PlayerPrefs.SetFloat(TAA_SHARPENING_STRING, sharpeningSlider.value);
                PlayerPrefs.Save();
            }
        }
        #endregion

        #region Resolution Methods
        private void SetResolution(int option)
        {
            string selectedOption = resolutionDropdown.options[option].text;
            PlayerPrefs.SetString(RESOLUTION_STRING, selectedOption);
            PlayerPrefs.Save();

            string[] dimensions = selectedOption.Split('x');
            int width = int.Parse(dimensions[0]);
            int height = int.Parse(dimensions[1]);
            Screen.SetResolution(width, height, Screen.fullScreen);
        }

        private void LoadAvailableResolutions()
        {
            resolutionDropdown.ClearOptions();

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

            resolutionDropdown.AddOptions(optionsList);

            string savedResolution = PlayerPrefs.GetString(RESOLUTION_STRING, RESOLUTION_DEFAULT);
            if (!string.IsNullOrEmpty(savedResolution))
            {
                int index = optionsList.IndexOf(savedResolution);
                resolutionDropdown.value = index != -1 ? index : currentResolutionIndex;
            }
            else
                resolutionDropdown.value = currentResolutionIndex;

            resolutionDropdown.RefreshShownValue();
        }
        #endregion

        #region Display Mode Methods
        private void SetDisplayMode(int option)
        {
            string selectedOption = displayModeDropdown.options[option].text;
            PlayerPrefs.SetString(DISPLAY_MODE_STRING, selectedOption);
            PlayerPrefs.Save();

            ApplyDisplayMode((FullScreenMode)Enum.Parse(typeof(FullScreenMode), selectedOption));
        }

        private void ApplyDisplayMode(FullScreenMode selectedOption)
        {
            Screen.fullScreenMode = selectedOption;
        }

        private void LoadDisplayModeOptions()
        {
            FullScreenMode mode = Screen.fullScreenMode;
            List<string> displayModes = new List<string> {
                Enum.GetName(typeof(FullScreenMode), FullScreenMode.ExclusiveFullScreen),
                Enum.GetName(typeof(FullScreenMode), FullScreenMode.Windowed),
                Enum.GetName(typeof(FullScreenMode),  FullScreenMode.FullScreenWindow),
               };

            displayModeDropdown.ClearOptions();
            displayModeDropdown.AddOptions(displayModes);

            string savedMode = PlayerPrefs.GetString(DISPLAY_MODE_STRING, Enum.GetName(typeof(FullScreenMode), FullScreenMode.ExclusiveFullScreen));
            int index = displayModes.IndexOf(savedMode);
            displayModeDropdown.value = index >= 0 ? index : 0;
            displayModeDropdown.RefreshShownValue();
        }
        #endregion

        #region DLSS/FSR2
        private void LoadUpscalingOptions()
        {
            List<string> upscalingModes = new List<string>();
            upscalingModes.Add("NONE");

            if (SystemInfo.graphicsDeviceName.ToLower().Contains("nvidia"))
            {
                upscalingModes.Add("DLSS");
            }

            if (SystemInfo.graphicsDeviceName.ToLower().Contains("amd"))
            {
                upscalingModes.Add("FSR2");
            }

            upscalingDropdown.ClearOptions();
            upscalingDropdown.AddOptions(upscalingModes);

            upscalingDropdown.value = upscalingModes.IndexOf(PlayerPrefs.GetString("UpscalingMode", "NONE"));
            SetUpscalingMode(upscalingDropdown.value);
            upscalingDropdown.RefreshShownValue();
        }

        private void LoadUpscalingResolutionOptions()
        {
            upscalingResolutionDropdown.ClearOptions();
            upscalingResolutionDropdown.AddOptions(DictionaryLibrary.DynamicResolutionMode.Keys.ToList());
            upscalingResolutionDropdown.value = PlayerPrefs.GetInt("upscalingResolutionMode", DictionaryLibrary.DynamicResolutionMode["Balanced"]);
            SetUpscalingResolutionMode(DictionaryLibrary.TranslationDynamicResolution[upscalingResolutionDropdown.value]);
        }

        public void SetUpscalingMode(int value)
        {
            switch (upscalingDropdown.options[value].text)
            {
                case "DLSS":
                    PlayerPrefs.SetString("UpscalingMode", "DLSS");
                    ToggleFSR2(false);
                    ToggleDLSS(true);
                    break;
                case "FSR2":
                    PlayerPrefs.SetString("UpscalingMode", "FSR2");
                    ToggleDLSS(false);
                    ToggleFSR2(true);
                    break;
                case "None":
                default:
                    PlayerPrefs.SetString("UpscalingMode", "NONE");
                    ToggleDLSS(false);
                    ToggleFSR2(false);
                    break;
            }
            
        }

        public void ToggleDLSS(bool value)
        {
            _camera.allowDynamicResolution = value;
            _hdAdditionalCameraData.allowDynamicResolution = value;
            _hdAdditionalCameraData.allowDeepLearningSuperSampling = value;
            _hdAdditionalCameraData.deepLearningSuperSamplingUseCustomAttributes = value;
            _hdAdditionalCameraData.deepLearningSuperSamplingUseCustomQualitySettings = value;
            _hdAdditionalCameraData.deepLearningSuperSamplingUseOptimalSettings = value;
            upscalingResolutionDropdown.interactable = value;
        }

        public void ToggleFSR2(bool value)
        {
            _camera.allowDynamicResolution = value;
            _hdAdditionalCameraData.allowDynamicResolution= value;
            _hdAdditionalCameraData.allowFidelityFX2SuperResolution = value;
            _hdAdditionalCameraData.fidelityFX2SuperResolutionUseCustomAttributes = value;
            _hdAdditionalCameraData.fidelityFX2SuperResolutionUseCustomQualitySettings = value;
            _hdAdditionalCameraData.fidelityFX2SuperResolutionUseOptimalSettings = value;
            upscalingResolutionDropdown.interactable = value;
        }


        public void SetUpscalingResolutionMode(int value)
        {
            switch (PlayerPrefs.GetString("UpscalingMode", "NONE"))
            {
                case "DLSS":
                    SetDLSSMode(value);
                    break;
                case "FSR2":
                    SetFSR2Mode(value);
                    break;
                case "NONE":
                default:
                    break;
            }
        }

        public void SetDLSSMode(int value)
        {
            _hdAdditionalCameraData.deepLearningSuperSamplingQuality = (uint)value;
            PlayerPrefs.SetInt("upscalingResolutionMode", value);
        }
        public void SetFSR2Mode(int value)
        {
            _hdAdditionalCameraData.fidelityFX2SuperResolutionQuality = (uint)value;
            PlayerPrefs.SetInt("upscalingResolutionMode", value);
        }

        #endregion

        #endregion

        #region Delete Progress Methods
        public void DeleteAllProgress()
        {
            deletePopupUI.SetActive(false);
            for (int i = 0; i < deletedProgressList.Count; i++)
            {
                PlayerPrefs.DeleteKey(deletedProgressList[i]);
            }
            PlayerPrefs.Save();
        }

        public void CloseDeletePopup()
        {
            deletePopupUI.SetActive(false);
        }

        public void OpenDeletePopup()
        {
            deletePopupUI.SetActive(true);
        }


        #endregion
        public void ChangeHeaderButtonsDirections(Selectable interactable)
        {
            Navigation navigation = audioSettingsButton.navigation;
            navigation.selectOnDown = interactable;
            audioSettingsButton.navigation = navigation;

            navigation = videoSettingsButton.navigation;
            navigation.selectOnDown = interactable;
            videoSettingsButton.navigation = navigation;

            navigation = accessibilitySettingsButton.navigation;
            navigation.selectOnDown = interactable;
            accessibilitySettingsButton.navigation = navigation;
        }
    }
}