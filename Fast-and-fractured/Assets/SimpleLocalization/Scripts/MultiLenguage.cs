using Assets.SimpleLocalization.Scripts;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MultiLanguage : MonoBehaviour
{
    private List<string> language = new List<string>();
    private int languageIndex = 0;
    private string languageIndexKey = "LanguageIndex";
    private void Awake()
    {
        
        language.Add("Catal�");
        language.Add("Espa�ol");
        language.Add("English");

        if (PlayerPrefs.HasKey(languageIndexKey))
        {
            languageIndex = PlayerPrefs.GetInt(languageIndexKey);
        }

        LocalizationManager.Read();

        SelectLanguage();
    }
    private void SelectLanguage()
    {
        switch (languageIndex)
        {
            case 0:
                LocalizationManager.Language = "English";
                break;
            case 1:
                LocalizationManager.Language = "Espa�ol";
                break;
            case 2:
                LocalizationManager.Language = "Catal�";
                break;
        }
    }
    public void NextLanguage()
    {
        languageIndex++;
        if (languageIndex >= language.Count)
        {
            languageIndex = 0;
        }
        SelectLanguage();
        PlayerPrefs.SetInt(languageIndexKey, languageIndex);
    }
    public void PreviousLanguage()
    {
        languageIndex--;
        if (languageIndex < 0)
        {
            languageIndex = language.Count - 1;
        }
        SelectLanguage();
        PlayerPrefs.SetInt(languageIndexKey, languageIndex);
    }
    private void OnDisable()
    {
        PlayerPrefs.Save();
    }
}
