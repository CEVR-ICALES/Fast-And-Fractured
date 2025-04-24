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
        [SerializeField] private GameObject[] inGameCharactersTopIcons;
        [SerializeField] private GameObject tomatoAlertUI;
        [SerializeField] private GameObject tomatoScreenEfect;
        public bool IsTomatoAlertActive {get => _isTomatoAlertActive;}
        private bool _isTomatoAlertActive = false;
        private ITimer _timerReference;

        public void CreateEvent(string eventText, float timeInScreen)
        {
            if(eventTextContainer.GetComponent<LocalizedText>().LocalizationKey != string.Empty)
            {
                _timerReference.StopTimer();
                _timerReference = null;
            }
            eventTextContainer.GetComponent<LocalizedText>().LocalizationKey = eventText;
            eventTextContainer.GetComponent<LocalizedText>().Localize();
            _timerReference = TimerSystem.Instance.CreateTimer(timeInScreen, onTimerDecreaseComplete: () =>
            {
                eventTextContainer.GetComponent<LocalizedText>().LocalizationKey = string.Empty;
                eventTextContainer.GetComponent<LocalizedText>().Localize();
            });
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
