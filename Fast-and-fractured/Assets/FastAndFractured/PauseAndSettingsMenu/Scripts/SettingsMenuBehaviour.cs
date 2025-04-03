using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FastAndFractured
{
    public class SettingsMenuBehaviour : MonoBehaviour
    {
        [Header("Sub menus ui")]
        [SerializeField] private GameObject audioSettingsUI;
        [SerializeField] private GameObject videoSettingsUI;
        [SerializeField] private GameObject accessibilitySettingsUI;
        [Header("Settings audio")]
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [Header("Settings video")]
        [SerializeField] private TMP_Dropdown fpsDropdown;
        [SerializeField] private TMP_Dropdown resolutionDropdown;
        [SerializeField] private TMP_Dropdown vsyncDropdown;
        [SerializeField] private TMP_Dropdown antiAliasingDropdown;
        [SerializeField] private TMP_Dropdown sharpeningDropdown;
        [SerializeField] private TMP_Dropdown rayTracingDropdown;
        [SerializeField] private Slider brightnessSlider;
        [Header("Settings accesibility")]
        [SerializeField] private TMP_Dropdown colorblindDropdown;
        [SerializeField] private TMP_Dropdown languageDropdown;
        [SerializeField] private TMP_Dropdown subtitlesDropdown;
        void Start()
        {
            SetStartValues();
            fpsDropdown.onValueChanged.AddListener(delegate { CapFPS(fpsDropdown.value); });
            resolutionDropdown.onValueChanged.AddListener(delegate { SetResolution(resolutionDropdown.value); });
            vsyncDropdown.onValueChanged.AddListener(delegate { SetVsync(vsyncDropdown.value); });
            antiAliasingDropdown.onValueChanged.AddListener(delegate { SetAntiAliasing(antiAliasingDropdown.value); });
            sharpeningDropdown.onValueChanged.AddListener(delegate { SetSharpening(sharpeningDropdown.value); });
            rayTracingDropdown.onValueChanged.AddListener(delegate { SetRayTracing(rayTracingDropdown.value); });
            brightnessSlider.onValueChanged.AddListener(delegate { SetBrightness(brightnessSlider.value); });
            colorblindDropdown.onValueChanged.AddListener(delegate { SetColorblind(colorblindDropdown.value); });
            languageDropdown.onValueChanged.AddListener(delegate { SetLanguage(languageDropdown.value); });
            subtitlesDropdown.onValueChanged.AddListener(delegate { SetSubtitles(subtitlesDropdown.value); });
            masterVolumeSlider.onValueChanged.AddListener(delegate { SetMasterVolume(masterVolumeSlider.value); });
            musicVolumeSlider.onValueChanged.AddListener(delegate { SetMusicVolume(musicVolumeSlider.value); });
            sfxVolumeSlider.onValueChanged.AddListener(delegate { SetSFXVolume(sfxVolumeSlider.value); });
        }
        private void SetStartValues()
        {
            //Master volume
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.5f);
            masterVolumeSlider.value = masterVolume;
            //Music volume 
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
            musicVolumeSlider.value = musicVolume;
            //SFX volume
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);
            sfxVolumeSlider.value = sfxVolume;
            // Max FPS
            int maxFPS = PlayerPrefs.GetInt("MaxFPS", 120);
            for (int i = 0; i < fpsDropdown.options.Count; i++)
            {
                if (fpsDropdown.options[i].text == maxFPS.ToString())
                {
                    fpsDropdown.value = i;
                    fpsDropdown.RefreshShownValue();
                    break;
                }
            }
            //Resolution
            string resolution = PlayerPrefs.GetString("Resolution","1920x1080");
            for (int i = 0; i < resolutionDropdown.options.Count; i++)
            {
                if (resolutionDropdown.options[i].text == resolution)
                {
                    resolutionDropdown.value = i;
                    resolutionDropdown.RefreshShownValue();
                    break;
                }
            }
            //VSync
            string vsync = PlayerPrefs.GetString("Vsync", "No");
            for (int i = 0; i < vsyncDropdown.options.Count; i++)
            {
                if (vsyncDropdown.options[i].text == vsync)
                {
                    vsyncDropdown.value = i;
                    vsyncDropdown.RefreshShownValue();
                    break;
                }
            }
            //Anti-Aliasing
            string antiAliasing = PlayerPrefs.GetString("Anti-Aliasing", "No");
            for (int i = 0; i < antiAliasingDropdown.options.Count; i++)
            {
                if (antiAliasingDropdown.options[i].text == antiAliasing)
                {
                    antiAliasingDropdown.value = i;
                    antiAliasingDropdown.RefreshShownValue();
                    break;
                }
            }
            //Sharpening
            string sharpening = PlayerPrefs.GetString("Sharpening", "No");
            for (int i = 0; i < sharpeningDropdown.options.Count; i++)
            {
                if (sharpeningDropdown.options[i].text == sharpening)
                {
                    sharpeningDropdown.value = i;
                    sharpeningDropdown.RefreshShownValue();
                    break;
                }
            }
            //Ray Tracing
            string rayTracing = PlayerPrefs.GetString("RayTracing", "No");
            for (int i = 0; i < rayTracingDropdown.options.Count; i++)
            {
                if (rayTracingDropdown.options[i].text == rayTracing)
                {
                    rayTracingDropdown.value = i;
                    rayTracingDropdown.RefreshShownValue();
                    break;
                }
            }
            //Brightness
            float brightness = PlayerPrefs.GetFloat("Brightness", 1f);
            brightnessSlider.value=brightness;
            //Colorblind
            string colorblind = PlayerPrefs.GetString("Colorblind", "No");
            for (int i = 0; i < colorblindDropdown.options.Count; i++)
            {
                if (colorblindDropdown.options[i].text == colorblind)
                {
                    colorblindDropdown.value = i;
                    colorblindDropdown.RefreshShownValue();
                    break;
                }
            }
            //Language
            string language = PlayerPrefs.GetString("Language", "Español");
            for (int i = 0; i < languageDropdown.options.Count; i++)
            {
                if (languageDropdown.options[i].text == language)
                {
                    languageDropdown.value = i;
                    languageDropdown.RefreshShownValue();
                    break;
                }
            }
            //Subtitles
            string subtitles = PlayerPrefs.GetString("Subtitles", "No");
            for (int i = 0; i < subtitlesDropdown.options.Count; i++)
            {
                if (subtitlesDropdown.options[i].text == subtitles)
                {
                    subtitlesDropdown.value = i;
                    subtitlesDropdown.RefreshShownValue();
                    break;
                }
            }
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

        //Audio settings
        private void SetMasterVolume(float value)
        {
            PlayerPrefs.SetFloat("MasterVolume", value);
            PlayerPrefs.Save();
            //TODO set master volume in game
        }
        private void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat("MusicVolume", value);
            PlayerPrefs.Save();
            //TODO set music volume in game
        }
        private void SetSFXVolume(float value)
        {
            PlayerPrefs.SetFloat("SFXVolume", value);
            PlayerPrefs.Save();
            //TODO set sfx volume in game
        }

        //Video settings
        private void SetBrightness(float value)
        {
            PlayerPrefs.SetFloat("Brightness",value);
            PlayerPrefs.Save();
            //TODO set brightness in game
        }
        private void SetVsync(int option)
        {
            string selectedOption = vsyncDropdown.options[option].text;
            PlayerPrefs.SetString("Vsync", selectedOption);
            PlayerPrefs.Save();
            //TODO set vsync in game
        }
        private void CapFPS(int option)
        {
            string selectedOption = fpsDropdown.options[option].text;
            int maxFPS=int.Parse(selectedOption);
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
            //TODO set resolution in game
        }
        private void SetRayTracing(int option)
        {
            string selectedOption = rayTracingDropdown.options[option].text;
            PlayerPrefs.SetString("RayTracing", selectedOption);
            PlayerPrefs.Save();
            //TODO set ray tracing in game
        }
        private void SetSharpening(int option)
        {
            string selectedOption = sharpeningDropdown.options[option].text;
            PlayerPrefs.SetString("Sharpening", selectedOption);
            PlayerPrefs.Save();
            //TODO set sharpening in game
        }

        //Accesibility settings
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
    }
}