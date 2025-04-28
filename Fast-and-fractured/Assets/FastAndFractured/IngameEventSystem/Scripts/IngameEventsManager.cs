using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Assets.SimpleLocalization.Scripts;
namespace FastAndFractured
{
    public class IngameEventsManager : AbstractSingleton<IngameEventsManager>
    {
        [SerializeField] private GameObject eventTextContainer;
        public GameObject[] inGameCharactersTopIcons;
        [SerializeField] private GameObject tomatoAlertUI;
        [SerializeField] private GameObject tomatoScreenEfect;
        public bool IsTomatoAlertActive {get => _isTomatoAlertActive;}
        private bool _isTomatoAlertActive = false;
        private ITimer _timerReference;
        private LocalizedText _localizedTextReference;
        private void Start()
        {
            _localizedTextReference = eventTextContainer.GetComponent<LocalizedText>();
            IngameEventsManager.Instance.CreateEvent("¡¡¡¡Empuja a todos fuera del mapa antes de que llegue la tormenta!!!!", 5f);
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
                inGameCharactersTopIcons[i].SetActive(true);
                i++;
            }
            Debug.Log("Ingame Characters Top Icon: " + inGameCharactersTopIcons[4]);
            LevelController.Instance.characterIcons = inGameCharactersTopIcons;
        }
        public void SetTomatoAlert()
        {
            tomatoAlertUI.SetActive(true);
            _isTomatoAlertActive = true;
        }
        public void RemoveTomatoAlert()
        {
            tomatoAlertUI.SetActive(false);
            _isTomatoAlertActive = false;
        }
        public void SetTomatoScreenEffect(float time)
        {
            tomatoScreenEfect.SetActive(true);
            TimerSystem.Instance.CreateTimer(time, onTimerDecreaseComplete: () =>
            {
                tomatoScreenEfect.SetActive(false);
            });
        }
    }
}
