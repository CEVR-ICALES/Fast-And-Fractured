using System.Collections;
using System.Collections.Generic;
using StateMachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Enums;
using System;

namespace FastAndFractured
{
    public class LevelController : AbstractSingleton<LevelController>
    {
        public bool usingController;
        [SerializeField] private List<StatsController> characters;
        [SerializeField] private EnemyAIBrain ai;
        [SerializeField] private List<KillCharacterNotify> killCharacterHandles;
        [SerializeField] private List<Controller> controllers;
        [SerializeField] private List<CharacterData> playersCharactersData;
        [SerializeField] private List<CharacterData> iaCharactersData;

        private List<string> _notcurrentInstanceCharacters;
        private Dictionary<string, int> _characterSelectedLimit;

        private List<GameObject> _inGameCharacters;

        private const char DEMILITER_CHAR_FOR_CHARACTER_NAMES = '_';
        private const int LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME = 2;
        private const int DEFAULT_SKIN = 0;
        private const int LIMIT_OF_CHARACTER_SPAWNED = 2;

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
            PlayerPrefs.SetString("Selected_Player","Josefino_0");
            LoadInGameCharacters();
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

            foreach (var character in characters)
            {
                character.CustomStart();
                Controller controller = character.GetComponentInParent<Controller>();
                if (controller && controller.CompareTag("Player"))
                {
                    ai.Player = character.transform.gameObject;
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

        #region Characters

        private void LoadInGameCharacters()
        {
            _inGameCharacters = new List<GameObject>();
            CreateNotInstanceCharactersListFromPlayerList();
           string selectedPlayer = PlayerPrefs.GetString("Selected_Player");
            GameObject player = GetCharacterWithNameCode(selectedPlayer,playersCharactersData);
           GameObject playerSkin = GetCharacterWithNameCode("Josefino_1",playersCharactersData);
            _inGameCharacters.Add(player);
            _inGameCharacters.Add(playerSkin);
        }

        private GameObject GetCharacterWithNameCode(string nameCode,List<CharacterData> listOfCharactersData)
        {
            string[] dividedNameCode = nameCode.Split(DEMILITER_CHAR_FOR_CHARACTER_NAMES);
            if (dividedNameCode.Length == LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME)
            {
                string name = dividedNameCode[0];
                if (_characterSelectedLimit.ContainsKey(name))
                {
                    if (_characterSelectedLimit[name] < LIMIT_OF_CHARACTER_SPAWNED)
                    {
                        if (int.TryParse(dividedNameCode[1], out int skinNum))
                        {
                            var characterPrefab = SearchCharacterInListOfCharacters(name, skinNum,listOfCharactersData);
                            if (characterPrefab != null)
                            {
                                RemoveSelectedCharacterFromNotInstanceCharacters(nameCode, name);
                                return characterPrefab;
                            }
                        }
                    }
                }
            }
            return null;
        }

        private GameObject SearchCharacterInListOfCharacters(string name, int skinNum, List<CharacterData> listOfCharactersData)
        {
            foreach (var character in listOfCharactersData)
            {
                if (character.CharacterName == name)
                {
                    if (skinNum == DEFAULT_SKIN)
                    {
                        return character.Character_0;
                    }
                    if (skinNum - 1 < character.Character_skins.Count)
                    {
                        return character.Character_skins[skinNum - 1];
                    }
                }
            }
            return null;
        }

        private void CreateNotInstanceCharactersListFromPlayerList()
        {
            _notcurrentInstanceCharacters = new List<string>();
            _characterSelectedLimit = new Dictionary<string, int>();
            int characterSkinCount;

            foreach (var character in playersCharactersData)
            {
                characterSkinCount = DEFAULT_SKIN + 1;
                _notcurrentInstanceCharacters.Add(character.CharacterName + DEMILITER_CHAR_FOR_CHARACTER_NAMES + DEFAULT_SKIN.ToString());
                _characterSelectedLimit.Add(character.CharacterName, 0);
                foreach (var characterSkin in character.Character_skins)
                {
                    _notcurrentInstanceCharacters.Add(character.CharacterName + DEMILITER_CHAR_FOR_CHARACTER_NAMES + characterSkinCount.ToString());
                    characterSkinCount++;
                }
            }
        }

        private void RemoveSelectedCharacterFromNotInstanceCharacters(string nameCode,string nameWithoutCode)
        {
            _characterSelectedLimit[nameWithoutCode]++;
            bool condition = _characterSelectedLimit[nameWithoutCode] >= LIMIT_OF_CHARACTER_SPAWNED;
            for(int notCurrentInstanceCharacterCount = 0; notCurrentInstanceCharacterCount < _notcurrentInstanceCharacters.Count; notCurrentInstanceCharacterCount++)
            {
                if (condition)
                {
                    if (_notcurrentInstanceCharacters[notCurrentInstanceCharacterCount].Contains(nameWithoutCode))
                    {
                        _notcurrentInstanceCharacters.RemoveAt(notCurrentInstanceCharacterCount);
                    }
                }
                else
                {
                    if (_notcurrentInstanceCharacters[notCurrentInstanceCharacterCount] == nameCode)
                    {
                        _notcurrentInstanceCharacters.RemoveAt(notCurrentInstanceCharacterCount);
                    }
                }
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
        #endregion
    }
}

