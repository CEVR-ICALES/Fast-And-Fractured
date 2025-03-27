using System.Collections;
using System.Collections.Generic;
using StateMachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class LevelController : AbstractSingleton<LevelController>
    {
        public bool usingController;
        [SerializeField] private List<ObjectPoolSO> poolSOList;
        [SerializeField] private List<StatsController> characters;
        [SerializeField] private EnemyAIBrain ai;
        [SerializeField] private List<KillCharacterNotify> killCharacterHandles;
        [SerializeField] private List<Controller> controllers;
        // Start is called before the first frame update
        protected override void Awake()
        {
            base.Awake();
            if (!ai)
            {
                ai = FindObjectOfType<EnemyAIBrain>();
            }
            StartLevel();
        }

        void Start()
        {

        }
        // this will be moved to gameManaager once its created

        private void OnEnable()
        {
            PlayerInputController.OnInputDeviceChanged += HandleInputChange;
        }

        private void OnDisable()
        {
            PlayerInputController.OnInputDeviceChanged -= HandleInputChange;
        }

        public void HandleInputChange(InputDeviceType inputType)
        {
            if (inputType == InputDeviceType.KEYBOARD_MOUSE)
            {
                usingController = false;
            }
            else if (inputType == InputDeviceType.XBOX_CONTROLLER || inputType == InputDeviceType.PS_CONTROLLER)
            {
                usingController = true;
            }

            foreach (StatsController character in characters) // for now when inoput changed its detected it will only notify the carMovementController of the player
            {
                if (character.CompareTag("Player"))
                {
                    character.gameObject.GetComponent<CarMovementController>().HandleInputChange(usingController);
                }
            }

        }
        // will be moved to gameManager

        // Update is called once per frame
        void Update()
        {

        }
        public void StartLevel()
        {
            Cursor.lockState = CursorLockMode.Locked;
            ObjectPoolManager.Instance.CustomStart();
            foreach (var poolSO in poolSOList)
            {
                ObjectPoolManager.Instance.CreateObjectPool(poolSO);
            }
            foreach (var character in characters)
            {
                character.CustomStart();
                if (character.CompareTag("Player"))
                {
                    ai.Player = character.gameObject;
                }
            }
            foreach (var killCharacterHandle in killCharacterHandles)
            {
                killCharacterHandle.onTouchCharacter += EliminatePlayer;
            }
            foreach (var controller in controllers)
            {
                controller.CustomStart();
            }
        }

        public void EliminatePlayer(StatsController characterstats)
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            float delay = characterstats.Dead();
            TimerSystem.Instance.CreateTimer(delay,TimerDirection.DECREASE, onTimerDecreaseComplete:  () => {
                if (IsThePlayer(characterstats.gameObject))
                {
                    SceneManager.LoadScene(currentSceneName);
                }
                else
                    characterstats.gameObject.SetActive(false);
            });
    }

        private bool IsThePlayer(GameObject character)
        {
            if (character.CompareTag("Player"))
                return true;
            return false;
        }
    }
}

