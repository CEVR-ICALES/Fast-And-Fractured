using Assets.SimpleLocalization.Scripts;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AccesibilitySettings : MonoBehaviour
{
    [Header("Lenguage")]
    private List<string> _languages = new List<string>();
    private int _languageIndex = 0;
    private string _languageIndexKey = "LanguageIndex";

    private void Awake()
    {
        // if we decide to add more lenguages we can create a public array to fill all lenguages
        _languages.Add("Catala");
        _languages.Add("Espanol");
        _languages.Add("English");

        if (PlayerPrefs.HasKey(_languageIndexKey))
        {
            _languageIndex = PlayerPrefs.GetInt(_languageIndexKey);
        }

        LocalizationManager.Read();

        SelectLanguage();
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
        PlayerPrefs.SetInt(_languageIndexKey, _languageIndex);
    }
    public void PreviousLanguage()
    {
        _languageIndex--;
        if (_languageIndex < 0)
        {
            _languageIndex = _languages.Count - 1;
        }
        SelectLanguage();
        PlayerPrefs.SetInt(_languageIndexKey, _languageIndex);
    }
    #endregion
    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
