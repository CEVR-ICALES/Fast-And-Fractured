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
        [SerializeField] private List<CharacterData> charactersData;
        [SerializeField] private GameObject playerBasePrefab;
        [SerializeField] private GameObject iaBasePrefab;
        public int CurrentPlayers { get=> _currentPlayers; set { _currentPlayers = value; }}
        private int _currentPlayers = 1;
        public int TotalCharacters { get => totalCharacters; set => totalCharacters = value; }
        [SerializeField]
        private int totalCharacters = 8;
        private List<string> _notcurrentInstanceCharacters;
        private Dictionary<string, int> _characterSelectedLimit;

        private List<GameObject> _inGameCharacters;

        [SerializeField]
        private GameObject[] spawnPoints;

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

            //foreach (var character in characters)
            //{
            //    //character.CustomStart();
            //    Controller controller = character.GetComponentInParent<Controller>();
            //    if (controller && controller.CompareTag("Player"))
            //    {
            //        ai.Player = character.transform.gameObject;
            //    }
            //}
            //foreach (var killCharacterHandle in killCharacterHandles)
            //{
            //    killCharacterHandle.onTouchCharacter += EliminatePlayer;
            //}
            //foreach (var controller in controllers)
            //{
            //    controller.CustomStart();
            //}
        }

        #region Characters

        private void LoadInGameCharacters()
        {
            _inGameCharacters = new List<GameObject>();
            CreateNotInstanceCharactersListFromPlayerList();
           string selectedPlayer = PlayerPrefs.GetString("Selected_Player");
            GameObject player = GetCharacterWithNameCode(selectedPlayer, out bool succeded);
            _inGameCharacters.Add(player);
            int totalIaCharacters = totalCharacters - _currentPlayers;
            for(int iaCharacterCount = 0; iaCharacterCount < totalIaCharacters&&succeded; iaCharacterCount++)
            {
                GameObject IA = GetCharacterWithNameCode(GetRandomValueFromShuffleList(_notcurrentInstanceCharacters, "empty List"),out succeded);
                if (IA != null) { 
                    _inGameCharacters.Add(IA);
                }
            }
            if(succeded)
            {
                SpawnCharactersInScene();
            }
        }

        private void SpawnCharactersInScene()
        {
           // if (spawnPoints.Length == totalCharacters)
            {
                for (int playersCount = 0; playersCount < _currentPlayers; playersCount++)
                {
                   Transform baseForPlayer = Instantiate(playerBasePrefab, spawnPoints[playersCount].transform.position,Quaternion.identity).transform;
                    Instantiate(_inGameCharacters[playersCount], baseForPlayer);
                }
            }
        }

        private GameObject GetCharacterWithNameCode(string nameCode, out bool succeded)
        {
            ErrorTypeInGettingCharacters errorType = ErrorTypeInGettingCharacters.DONT_EXIST;
            if (_notcurrentInstanceCharacters.Contains(nameCode))
            {
                errorType = ErrorTypeInGettingCharacters.INCORRECT_NAME_CODE;
                string[] dividedNameCode = nameCode.Split(DEMILITER_CHAR_FOR_CHARACTER_NAMES);
                if (dividedNameCode.Length == LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME)
                {
                        string name = dividedNameCode[0];
                        if (int.TryParse(dividedNameCode[1], out int skinNum))
                        {
                        errorType = ErrorTypeInGettingCharacters.NOT_FOUND;
                        var characterPrefab = SearchCharacterInListOfCharacters(name, skinNum);
                            if (characterPrefab != null)
                            {
                                RemoveSelectedCharacterFromNotInstanceCharacters(nameCode, name);
                                succeded = true;
                                return characterPrefab;
                            }
                        }
                }
            }
            DebugWarningErrorsForCharacterList(errorType, nameCode, "Make sure the format 'Josefino_0' is correct or the character is in the charactersData list.");
            succeded = false;   
            return null;
        }

        private void DebugWarningErrorsForCharacterList(ErrorTypeInGettingCharacters errorType, string nameCode, string genericCorrection)
        {
            switch (errorType)
            {
                case ErrorTypeInGettingCharacters.DONT_EXIST:
                    Debug.LogWarning("Name code " + nameCode + " given for the character don't exist or was not saved. " + genericCorrection);
                    break;
                case ErrorTypeInGettingCharacters.INCORRECT_NAME_CODE:
                    Debug.LogWarning("The format of  " + nameCode + " is incorrect" + genericCorrection);
                    break;
                case ErrorTypeInGettingCharacters.NOT_FOUND:
                    Debug.LogWarning("Character " + nameCode + "was not found in characterData list. " + genericCorrection );
                    break;
            }
        }

        private T GetRandomValueFromShuffleList<T>(List<T> list, T defaultValue)
        {
            if (list.Count > 0)
            {
                //Shuffle the list
                ShuffleList(list);
                return list[UnityEngine.Random.Range(0, _notcurrentInstanceCharacters.Count)];
            }
            return defaultValue;
        }

        private void ShuffleList<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private GameObject SearchCharacterInListOfCharacters(string name, int skinNum)
        {
            foreach (var character in charactersData)
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

            foreach (var character in charactersData)
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
            _notcurrentInstanceCharacters.Remove(nameCode);
            if (_characterSelectedLimit[nameWithoutCode] >= LIMIT_OF_CHARACTER_SPAWNED) {
                List<string> copyOfnotCurrentInstanceCharacter = new List<string>(_notcurrentInstanceCharacters);
                for (int notCurrentInstanceCharacterCount = 0; notCurrentInstanceCharacterCount < copyOfnotCurrentInstanceCharacter.Count; notCurrentInstanceCharacterCount++)
                {
                    if (copyOfnotCurrentInstanceCharacter[notCurrentInstanceCharacterCount] == nameCode)
                    {
                        _notcurrentInstanceCharacters.Remove(copyOfnotCurrentInstanceCharacter[notCurrentInstanceCharacterCount]);
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

