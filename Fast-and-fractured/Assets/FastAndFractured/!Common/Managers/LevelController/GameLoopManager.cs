using Enums;
using StateMachine;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Utilities;  
using Utilities.Managers.PauseSystem;  
namespace FastAndFractured
{
    public class GameLoopManager
    {
        private float _timeToCallTheStorm;
        private float _playerDeadReductionTime;
        private ITimer _callStormTimer;
        private ITimer _delayUntilGameStartsTimer;
        private SandstormController _sandStormController;  
        
        public bool HasPlayerWon { get; private set; }
        public int AliveCharacterCount { get; private set; }

        private List<GameObject> _inGameCharacters;  
        private GameObject _playerReference;
        private UnityEvent _charactersCustomStartEvent;

        public GameLoopManager(float timeToCallTheStorm, float playerDeadReductionTime, SandstormController sandstormController)
        {
            _timeToCallTheStorm = timeToCallTheStorm;
            _playerDeadReductionTime = playerDeadReductionTime;
            _sandStormController = sandstormController;
            HasPlayerWon = false;
        }

        public void InitializeSession(int maxCharacters, List<GameObject> inGameCharacters, GameObject playerReference, UnityEvent charactersCustomStartEvent)
        {
            AliveCharacterCount = maxCharacters;   
            _inGameCharacters = inGameCharacters;
            _playerReference = playerReference;
            _charactersCustomStartEvent = charactersCustomStartEvent;

        }

   
        public void StartGameInitializationDelay(float delay, System.Action onTimerComplete)
        {
            if (TimerSystem.Instance != null)
            {
                _delayUntilGameStartsTimer = TimerSystem.Instance.CreateTimer(delay, onTimerDecreaseComplete: () => {
                    onTimerComplete?.Invoke();
                    _delayUntilGameStartsTimer = null;


                });
            }
            else
            {
                Debug.LogError("TimerSystem.Instance is null. Cannot start game delay timer.");
                onTimerComplete?.Invoke();
            }
        }
        public void ActivateCharacterControls()
        {
            if (_playerReference != null)
            {
                var playerInputCtrl = _playerReference.GetComponentInParent<PlayerInputController>();
                if (playerInputCtrl) playerInputCtrl.enabled = true;
                var playerBaseCtrl = _playerReference.GetComponentInParent<Controller>();
                if(playerBaseCtrl) playerBaseCtrl.enabled = true;
            }

            foreach (var character in _inGameCharacters)
            {
                if (character == _playerReference) continue;  
                var aiBaseCtrl = character.GetComponentInParent<Controller>();
                if (aiBaseCtrl) aiBaseCtrl.enabled = true;
            }
            _charactersCustomStartEvent?.Invoke();
        }


        public void SetupStorm(bool callStormInitially, bool debugModeForStormSpawns)
        {
            if (callStormInitially)
            {
                if (_sandStormController != null)
                {
                    _sandStormController.SetSpawnPoints(debugModeForStormSpawns);
                    if (TimerSystem.Instance != null)
                    {
                         _callStormTimer = TimerSystem.Instance.CreateTimer(_timeToCallTheStorm, TimerDirection.DECREASE, onTimerDecreaseComplete: () => { 
                             HandleStormCall(); 
                             _callStormTimer = null; 
                         });
                    }
                    else
                    {
                        Debug.LogError("TimerSystem.Instance is null. Cannot start storm timer.");
                    }
                }
                else
                {
                    Debug.LogError("_sandStormController is null. Cannot set up storm.");
                }
            }
        }

        private void HandleStormCall()
        {
            if (_sandStormController != null)
            {
                if (!_sandStormController.StormSpawnPointsSetted) 
                    _sandStormController.SetSpawnPoints(false);    //todo put the correct value here
                _sandStormController.SpawnFogs();
                _sandStormController.MoveSandStorm = true;
            }
        }

        public void OnCharacterDied(GameObject character, bool isPlayer, float despawnDelay, 
                                    List<CharacterIcon> characterIcons,
                                    System.Action onPlayerWin, System.Action onPlayerLoss)
        {
            if (isPlayer && _callStormTimer != null && TimerSystem.Instance != null && TimerSystem.Instance.HasTimer(_callStormTimer))
            {
                TimerData stormTimerData = _callStormTimer.GetData();
                float newTime = Mathf.Max(0, stormTimerData.CurrentTime - _playerDeadReductionTime);
                TimerSystem.Instance.ModifyTimer(_callStormTimer, newCurrentTime: newTime);
            }

            if (TimerSystem.Instance != null)
            {
                TimerSystem.Instance.CreateTimer(despawnDelay, onTimerDecreaseComplete: () => {
                    ProcessCharacterDeathAfterDelay(character, isPlayer, characterIcons, onPlayerWin, onPlayerLoss);
                });
            }
            else
            {
                ProcessCharacterDeathAfterDelay(character, isPlayer, characterIcons, onPlayerWin, onPlayerLoss);
            }
        }

        private void ProcessCharacterDeathAfterDelay(GameObject character, bool isPlayer, 
                                                    List<CharacterIcon> characterIcons,
                                                    System.Action onPlayerWin, System.Action onPlayerLoss)
        {
            if (!isPlayer)
            {
                if (_inGameCharacters != null) _inGameCharacters.Remove(character);

                if (characterIcons != null)
                {
                    foreach (CharacterIcon icon in characterIcons)
                    {
                        if (icon.Character == character)  
                        {
                            icon.SetPlayerDeadIconIsActive(true);  
                            break; 
                        }
                    }
                }
                
                if (character != null && character.transform.parent != null)  
                {
                    UnityEngine.Object.Destroy(character.transform.parent.gameObject);
                }
                else if (character != null)
                {
                    UnityEngine.Object.Destroy(character);
                }

                AliveCharacterCount--;

                Debug.Log("AliveCharacterCount " + AliveCharacterCount);
                Debug.Log("_inGameCharacters Count" + _inGameCharacters.Count);
                if (AliveCharacterCount <= 1 && _playerReference != null && _inGameCharacters.Contains(_playerReference))  
                {
                    HasPlayerWon = true;
                    onPlayerWin?.Invoke();
                }
                else if (AliveCharacterCount <= 0 && _playerReference == null)  
                {
                     Debug.Log("All AIs eliminated, no player was active or player already lost.");
                }

            }
            else  
            {
                HasPlayerWon = false;
                onPlayerLoss?.Invoke();
            }
        }
    }
}