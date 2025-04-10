using TMPro;
using Utilities;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FastAndFractured
{
    public class SettingsMenuBehaviour : MonoBehaviour
    {
        [Header("Menu Settings UI")]
        [SerializeField] private GameObject audioSettingsUI;
        [SerializeField] private GameObject videoSettingsUI;
        [SerializeField] private GameObject accessibilitySettingsUI;

        [Header("Audio Settings")]
        [SerializeField] private Slider generalVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;

        [Header("Video Settings")]
        [SerializeField] private Toggle vsyncDropdown;
        [SerializeField] private Slider brightnessSlider;
        [SerializeField] private TMP_Dropdown fpsDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown antiAliasingDropdown;
        [SerializeField] private TMP_Dropdown sharpeningDropdown;

        [Header("Accesibility Settings ")]
        [SerializeField] private TMP_Dropdown colorblindDropdown;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private TMP_Dropdown subtitlesDropdown;

        void Start()
        {
            SetStartValues();
            fpsDropdown.onValueChanged.AddListener(delegate { CapFPS(fpsDropdown.value); });
            resolutionDropdown.onValueChanged.AddListener(delegate { SetResolution(resolutionDropdown.value); });
            antiAliasingDropdown.onValueChanged.AddListener(delegate { SetAntiAliasing(antiAliasingDropdown.value); });
            sharpeningDropdown.onValueChanged.AddListener(delegate { SetSharpening(sharpeningDropdown.value); });
            brightnessSlider.onValueChanged.AddListener(delegate { SetBrightness(brightnessSlider.value); });
            colorblindDropdown.onValueChanged.AddListener(delegate { SetColorblind(colorblindDropdown.value); });
            languageDropdown.onValueChanged.AddListener(delegate { SetLanguage(languageDropdown.value); });
            subtitlesDropdown.onValueChanged.AddListener(delegate { SetSubtitles(subtitlesDropdown.value); });
            generalVolumeSlider.onValueChanged.AddListener(delegate { SetMasterVolume(generalVolumeSlider.value); });
            musicVolumeSlider.onValueChanged.AddListener(delegate { SetMusicVolume(musicVolumeSlider.value); });
            sfxVolumeSlider.onValueChanged.AddListener(delegate { SetSFXVolume(sfxVolumeSlider.value); });
        }

        private void SetStartValues()
        {
            //Master volume
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.5f);
            RefreshValue(generalVolumeSlider, masterVolume);

            //Music volume 
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            RefreshValue(musicVolumeSlider, musicVolume);

            //SFX volume
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
            RefreshValue(sfxVolumeSlider, sfxVolume);

            // Max FPS
            int maxFPS = PlayerPrefs.GetInt("MaxFPS", 120);
            string maxFPSString = maxFPS.ToString();
            RefreshValue(fpsDropdown, maxFPSString);

            //Resolution
            string resolution = PlayerPrefs.GetString("Resolution", "1920x1080");
            RefreshValue(resolutionDropdown, resolution);
            LoadAvailableResolutions();

            //VSync
            string vsync = PlayerPrefs.GetString("Vsync", "No");

            //Anti-Aliasing
            string antiAliasing = PlayerPrefs.GetString("Anti-Aliasing", "No");
            RefreshValue(antiAliasingDropdown, antiAliasing);

            //Sharpening
            string sharpening = PlayerPrefs.GetString("Sharpening", "No");
            RefreshValue(sharpeningDropdown, sharpening);

            //Brightness
            float brightness = PlayerPrefs.GetFloat("Brightness", 1f);
            RefreshValue(brightnessSlider, brightness);

            //Colorblind
            string colorblind = PlayerPrefs.GetString("Colorblind", "No");
            RefreshValue(colorblindDropdown, colorblind);

            //Language
            string language = PlayerPrefs.GetString("Language", "Español");
            RefreshValue(languageDropdown, language);

            //Subtitles
            string subtitles = PlayerPrefs.GetString("Subtitles", "No");
            RefreshValue(subtitlesDropdown, subtitles);
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
            audioSettingsUI.SetActive(true);
            videoSettingsUI.SetActive(false);
            accessibilitySettingsUI.SetActive(false);
        }
        public void OpenVideoSettings()
        {
            audioSettingsUI.SetActive(false);
            videoSettingsUI.SetActive(true);
            accessibilitySettingsUI.SetActive(false);
        }
        public void OpenAccesibilitySettings()
        {
            audioSettingsUI.SetActive(false);
            videoSettingsUI.SetActive(false);
            accessibilitySettingsUI.SetActive(true);
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
            if (isActive)
            {
                QualitySettings.vSyncCount = 1;
                Application.targetFrameRate = -1;
            }
            else
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 1; //will be changed to the current option from the fps cap
            }
        }

        private void CapFPS(int option)
        {
            string selectedOption = fpsDropdown.options[option].text;
            int maxFPS = int.Parse(selectedOption);
            Application.targetFrameRate = maxFPS;
            PlayerPrefs.SetInt("MaxFPS", maxFPS);
            PlayerPrefs.Save();
        }

        private void SetAntiAliasing(int option)
        {
            string selectedOption = antiAliasingDropdown.options[option].text;
            PlayerPrefs.SetString("Anti-Aliasing", selectedOption);
            PlayerPrefs.Save();
            //TODO set anti-aliasing in game
        }

        private void SetResolution(int option)
        {
            string selectedOption = resolutionDropdown.options[option].text;
            PlayerPrefs.SetString("Resolution", selectedOption);
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

            string savedResolution = PlayerPrefs.GetString("Resolution", "");
            if (!string.IsNullOrEmpty(savedResolution))
            {
                int index = optionsList.IndexOf(savedResolution);
                resolutionDropdown.value = index != -1 ? index : currentResolutionIndex;
            }
            else
                resolutionDropdown.value = currentResolutionIndex;

            resolutionDropdown.RefreshShownValue();
        }

        private void SetSharpening(int option)
        {
            string selectedOption = sharpeningDropdown.options[option].text;
            PlayerPrefs.SetString("Sharpening", selectedOption);
            PlayerPrefs.Save();
            //TODO set sharpening in game
        }
        #endregion

        #region Accesibility settings
        private void SetColorblind(int option)
        {
            string selectedOption = colorblindDropdown.options[option].text;
            PlayerPrefs.SetString("Colorblind", selectedOption);
            PlayerPrefs.Save();
            //TODO set colorblind in game
        }
        private void SetLanguage(int option)
        {
            string selectedOption = languageDropdown.options[option].text;
            PlayerPrefs.SetString("Language", selectedOption);
            PlayerPrefs.Save();
            //TODO set language in game
        }
        private void SetSubtitles(int option)
        {
            string selectedOption = subtitlesDropdown.options[option].text;
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