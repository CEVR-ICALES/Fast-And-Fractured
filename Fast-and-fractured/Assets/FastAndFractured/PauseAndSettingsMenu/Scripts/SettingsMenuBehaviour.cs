using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FastAndFractured
{
    public class SettingsMenuBehaviour : MonoBehaviour
    {
        [SerializeField] private GameObject settingsMenuUI;
        [SerializeField] private GameObject audioSettingsUI;
        [SerializeField] private GameObject videoSettingsUI;
        [SerializeField] private GameObject accessibilitySettingsUI;
        [SerializeField] private TMP_Dropdown fpsDropdown;
        void Start()
        {
            settingsMenuUI.SetActive(false);
            SetStartValues();
            fpsDropdown.onValueChanged.AddListener(delegate { CapFPS(fpsDropdown.value); });
        }
        private void SetStartValues()
        {
            int maxFPS = PlayerPrefs.GetInt("MaxFPS", 120);
            for (int i = 0; i < fpsDropdown.options.Count; i++)
            {
                if (fpsDropdown.options[i].text == maxFPS.ToString())
                {
                    fpsDropdown.value = i;
                    CapFPS(i);
                    fpsDropdown.RefreshShownValue();
                    break;
                }
            }
        }
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
        public void CapFPS(int option)
        {
            string selectedOption = fpsDropdown.options[option].text;
            int maxFPS=int.Parse(selectedOption);
            Application.targetFrameRate = maxFPS;
            PlayerPrefs.SetInt("MaxFPS", maxFPS);
            PlayerPrefs.Save();
            Debug.Log("FPS capped to: " + maxFPS);
        }
    }
}