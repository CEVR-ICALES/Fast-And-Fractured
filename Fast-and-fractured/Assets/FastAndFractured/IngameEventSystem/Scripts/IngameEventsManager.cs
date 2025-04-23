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
        [SerializeField] private float eventDuration = 10f;
        [SerializeField] private GameObject[] inGameCharactersElements;
        private ITimer _timerReference;

        public void CreateEvent(string eventText)
        {
            if(eventTextContainer.GetComponent<LocalizedText>().LocalizationKey != string.Empty)
            {
                _timerReference.StopTimer();
                _timerReference = null;
            }
            eventTextContainer.GetComponent<LocalizedText>().LocalizationKey = eventText;
            eventTextContainer.GetComponent<LocalizedText>().Localize();
            _timerReference = TimerSystem.Instance.CreateTimer(eventDuration, onTimerDecreaseComplete: () =>
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
                inGameCharactersElements[i].GetComponent<CharacterIcon>().SetCharacterIcon(character, LevelController.Instance.InGameCharactersNameCodes[i]);
                inGameCharactersElements[i].SetActive(true);
                i++;
            }
        }
    }
}
