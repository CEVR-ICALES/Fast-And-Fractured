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
        public bool IsAlertActive { get => _isAlertActive; }
        private bool _isAlertActive = false;
        private ITimer _timerReference;
        private LocalizedText _localizedTextReference;
        private const string RELASE_SCENE_NAME = "Release";

        public const float EVENT_START_DELAY = 0.5f;
        protected override void Construct()
        {
            base.Construct();
            
            // If this instance is disabled (duplicate), or HUDManager is missing (server), abort.
            if (!this.enabled || !HUDManager.HasInstance) return;

            var playerIconsElement = HUDManager.Instance.GetUIElement(UIDynamicElementType.PLAYER_ICONS);
            if (playerIconsElement != null)
            {
                foreach (CharacterIcon playerIcon in playerIconsElement.gameObject.GetComponentsInChildren<CharacterIcon>(true))
                {
                    inGameCharactersTopIcons.Add(playerIcon);
                }
            }
            
            var eventTextElement = HUDManager.Instance.GetUIElement(UIDynamicElementType.EVENT_TEXT);
            if (eventTextElement != null)
            {
                eventTextContainer = eventTextElement.gameObject;
                _localizedTextReference = eventTextContainer.GetComponent<LocalizedText>();
            }
        }
        protected override void Initialize()
        {
            base.Initialize();
            if (LevelController.HasInstance)
            {
                LevelController.Instance.onLevelPreStart.AddListener(
                SetCharactersTopElements);
            }
        }
      

        [ContextMenu("Create debug event")]
        public void DebugEvent()
        {
            CreateEvent("Debug event",5f);
        }

        public void CreateEvent(string eventText, float timeInScreen, Action onEventComplete = null)
        {
            if (_localizedTextReference == null) return;
            
            if (_timerReference != null && _localizedTextReference.LocalizationKey != string.Empty)
            {
                _timerReference.StopTimer();
                _timerReference = null;
            }
            _localizedTextReference.LocalizationKey = eventText;
            _localizedTextReference.Localize();
            if (!TimerSystem.HasInstance)
            {
                onEventComplete?.Invoke();
                return;
            }
            _timerReference = TimerSystem.Instance.CreateTimer(timeInScreen, onTimerDecreaseComplete: () =>
            {
                if (_localizedTextReference != null && _localizedTextReference.gameObject != null)
                {
                    _localizedTextReference.LocalizationKey = string.Empty;
                    _localizedTextReference.Localize();
                }
                onEventComplete?.Invoke();
            });
        }
        public void SetCharactersTopElements()
        {
            if (!LevelController.HasInstance || inGameCharactersTopIcons == null || inGameCharactersTopIcons.Count == 0)
            {
                return;
            }
            var levelController = LevelController.Instance;
            if (levelController.InGameCharacters == null || levelController.InGameCharactersNameCodes == null)
            {
                return;
            }
            int i = 0;
            foreach (GameObject character in levelController.InGameCharacters)
            {
                if (i >= inGameCharactersTopIcons.Count || i >= levelController.InGameCharactersNameCodes.Count)
                {
                    break;
                }
                inGameCharactersTopIcons[i].GetComponent<CharacterIcon>().SetCharacterIcon(character, levelController.InGameCharactersNameCodes[i]);
                inGameCharactersTopIcons[i].gameObject.SetActive(true);
                i++;
            }
            levelController.characterIcons = inGameCharactersTopIcons;
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

        public void UltEvent()
        {

        }

    }
}
