using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Assets.SimpleLocalization.Scripts;
namespace FastAndFractured
{
    public class LateralIngameEventsManager : AbstractSingleton<LateralIngameEventsManager>
    {
        [SerializeField] private GameObject normalEventContainer;
        [SerializeField] private GameObject urgentEventContainer;
        [SerializeField] private GameObject normalEventPrefab;
        [SerializeField] private GameObject urgentEventPrefab;
        [SerializeField] private float normalEventDuration = 5f;
        [SerializeField] private float urgentEventDuration = 10f;

        public void CreateNormalEvent(string eventText)
        {
            GameObject normalEvent = Instantiate(normalEventPrefab, normalEventContainer.transform);
            normalEvent.GetComponent<LocalizedText>().LocalizationKey = eventText;
            TimerSystem.Instance.CreateTimer(normalEventDuration, onTimerDecreaseComplete: () =>
            {
                Destroy(normalEvent);
            });
        }
        public void CreateUrgentEvent(string eventText)
        {
            GameObject urgentEvent = Instantiate(urgentEventPrefab, urgentEventContainer.transform);
            urgentEvent.GetComponent<LocalizedText>().LocalizationKey = eventText;
            TimerSystem.Instance.CreateTimer(urgentEventDuration, onTimerDecreaseComplete: () =>
            {
               Destroy(urgentEvent);
            });
        }
    }
}
