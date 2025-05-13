using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Assets.SimpleLocalization.Scripts;
using Enums;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace FastAndFractured
{
    public class IngameEventsManager : AbstractSingleton<IngameEventsManager>
    {
        private GameObject eventTextContainer;
        private List<CharacterIcon> inGameCharactersTopIcons = new();
        [SerializeField] private GameObject tomatoAlertUI;
        [SerializeField] private GameObject tomatoScreenEfect;
        public bool IsAlertActive {get => _isAlertActive;}
        private bool _isAlertActive = false;
        private ITimer _timerReference;
        private LocalizedText _localizedTextReference;
        private const string RELASE_SCENE_NAME = "Release";

        private void Start()
        {
            foreach (CharacterIcon playerIcon in HUDManager.Instance.GetUIElement(UIDynamicElementType.PLAYER_ICONS).gameObject.GetComponentsInChildren<CharacterIcon>(true)){
                inGameCharactersTopIcons.Add(playerIcon);
            }
            eventTextContainer = HUDManager.Instance.GetUIElement(UIDynamicElementType.EVENT_TEXT).gameObject;
            _localizedTextReference = eventTextContainer.GetComponent<LocalizedText>();
            if (SceneManager.GetActiveScene() == SceneManager.GetSceneByName(RELASE_SCENE_NAME))
            {
                TimerSystem.Instance.CreateTimer(0.5f, onTimerDecreaseComplete: () =>
                {
                    CreateEvent("Events.Start", 5f);
                });
            }
        }

        public void CreateEvent(string eventText, float timeInScreen)
        {
            if (_localizedTextReference != null)
            {
                if (_localizedTextReference.LocalizationKey != string.Empty)
                {
                    _timerReference.StopTimer();
                    _timerReference = null;
                }
                _localizedTextReference.LocalizationKey = eventText;
                _localizedTextReference.Localize();
                _timerReference = TimerSystem.Instance.CreateTimer(timeInScreen, onTimerDecreaseComplete: () =>
                {
                    _localizedTextReference.LocalizationKey = string.Empty;
                    _localizedTextReference.Localize();
                });
            }
        }
        public void SetCharactersTopElements()
        {
            int i = 0;
            foreach (GameObject character in LevelController.Instance.InGameCharacters)
            {
                inGameCharactersTopIcons[i].GetComponent<CharacterIcon>().SetCharacterIcon(character, LevelController.Instance.InGameCharactersNameCodes[i]);
                inGameCharactersTopIcons[i].gameObject.SetActive(true);
                i++;
            }
            LevelController.Instance.characterIcons = inGameCharactersTopIcons;
        }
        public void SetAlert()
        {
            tomatoAlertUI.SetActive(true);
            _isAlertActive = true;
        }
        public void RemoveAlert()
        {
            tomatoAlertUI.SetActive(false);
            _isAlertActive = false;
        }

        public void UltEvent(){
            
        }

    }
}
