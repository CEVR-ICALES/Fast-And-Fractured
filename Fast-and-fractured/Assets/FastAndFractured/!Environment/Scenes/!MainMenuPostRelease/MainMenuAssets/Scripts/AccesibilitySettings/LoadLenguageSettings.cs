using Assets.SimpleLocalization.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadLenguageSettings : MonoBehaviour
{
    private const string LEANGUAGE_INDEX_KEY = "LanguageIndex";
    private int _languageIndex = 0;
    private void Start()
    {
        if (PlayerPrefs.HasKey(LEANGUAGE_INDEX_KEY))
        {
            _languageIndex = PlayerPrefs.GetInt(LEANGUAGE_INDEX_KEY);
        }
        LocalizationManager.Read();
        SelectLanguage();
    }

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
}
