using Assets.SimpleLocalization.Scripts;
using Enums;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;

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

        public const float EVENT_START_DELAY = 0.5f;

        private void Start()
        {
            foreach (CharacterIcon playerIcon in HUDManager.Instance.GetUIElement(UIDynamicElementType.PLAYER_ICONS).gameObject.GetComponentsInChildren<CharacterIcon>(true)){
                inGameCharactersTopIcons.Add(playerIcon);
            }
            eventTextContainer = HUDManager.Instance.GetUIElement(UIDynamicElementType.EVENT_TEXT).gameObject;
            _localizedTextReference = eventTextContainer.GetComponent<LocalizedText>();
            if (SceneManager.GetActiveScene() == SceneManager.GetSceneByName(RELASE_SCENE_NAME))
            {
            }
        }

        private void OnDisable()
        {
         }

      
        public void CreateEvent(string eventText, float timeInScreen,Action onEventComplete=null)
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
                    onEventComplete?.Invoke();
                });
            }
        }
        public void SetCharactersTopElements()
        {
            int i = 0;
            foreach (GameObject character in LevelControllerButBetter.Instance.InGameCharacters)
            {
                inGameCharactersTopIcons[i].GetComponent<CharacterIcon>().SetCharacterIcon(character, LevelControllerButBetter.Instance.InGameCharactersNameCodes[i]);
                inGameCharactersTopIcons[i].gameObject.SetActive(true);
                i++;
            }
            LevelControllerButBetter.Instance.characterIcons = inGameCharactersTopIcons;
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
