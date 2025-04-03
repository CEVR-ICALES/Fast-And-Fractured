using System.Collections;
using System.Collections.Generic;
using StateMachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Enums;
using UnityEngine.Events;

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

        public int CurrentPlayers { get=> _currentPlayers; set { _currentPlayers = value; }}
        private int _currentPlayers = 1;
        public UnityEvent charactersCustomStart;
        public int TotalCharacters { get => totalCharacters; set => totalCharacters = value; }
        [SerializeField]
        private int totalCharacters = 8;
        private List<string> _notcurrentInstanceCharacters;
        private Dictionary<string, int> _characterSelectedLimit;

        private List<string> _inGameCharacters;

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
            _inGameCharacters = new List<string>();
            bool succeded = CreateNotInstanceCharactersListFromPlayerList();
           string selectedPlayer = PlayerPrefs.GetString("Selected_Player");
            if (succeded = CheckIfCharacterExistInList(selectedPlayer))
            {
                _inGameCharacters.Add(selectedPlayer);
            }
            int totalIaCharacters = totalCharacters - _currentPlayers;
            for(int iaCharacterCount = 0; iaCharacterCount < totalIaCharacters&&succeded; iaCharacterCount++)
            {
                string iaName = GetRandomValueFromShuffleList(_notcurrentInstanceCharacters, "empty List");
                if (succeded = CheckIfCharacterExistInList(iaName))
                {
                    _inGameCharacters.Add(iaName);
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
                int allCharacters = _inGameCharacters.Count;
                int charactersCount = 0;
                for (; charactersCount < _currentPlayers; charactersCount++)
                {
                    GameObject player = SearchCharacterInList(_inGameCharacters[charactersCount]);
                   Transform baseForPlayer = Instantiate(playerBasePrefab, spawnPoints[charactersCount].transform.position,Quaternion.identity).transform;
                    Instantiate(_inGameCharacters[charactersCount], baseForPlayer);
                }
                if (charactersCustomStart != null)
                {
                    charactersCustomStart.Invoke();
                }
            }
        }

        private void DivideNameCode(string nameCode, out string name, out int skinNum)
        {
            name = " ";
            skinNum = -1;
            string[] dividedNameCode = nameCode.Split(DEMILITER_CHAR_FOR_CHARACTER_NAMES);
            if (dividedNameCode.Length == LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME)
            {
                name = dividedNameCode[0];
                if (_characterSelectedLimit.ContainsKey(name))
                {
                    if (_characterSelectedLimit[name] < LIMIT_OF_CHARACTER_SPAWNED)
                    {
                        if (int.TryParse(dividedNameCode[1], out skinNum))
                        {

                        }
                    }
                }
            }
        }

        private bool CheckIfCharacterExistInList(string nameCode)
        {
            if (_notcurrentInstanceCharacters.Contains(nameCode))
            {
                return true;
            }
            Debug.LogWarning("Name code " + nameCode + " given for the character don't exist or was not saved. Make sure the format 'Josefino_0' is correct or the character is in the charactersData list.");
            return false;
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

        private GameObject SearchCharacterInList(string nameCode,bool player)
        {
            DivideNameCode(nameCode, out string name, out int skinNum);
            foreach (var character in charactersData)
            {
                if (character.CharacterName == name)
                {
                    if (skinNum == DEFAULT_SKIN)
                    {
                        return player ? character.Player_0 : character.IA_0;
                    }
                    if (skinNum - 1 < character.Player_skins.Count)
                    {
                        return player ? character.Player_skins[skinNum - 1] : character.IA_Skins[skinNum - 1];
                    }
                }
            }
            return null;
        }

        private bool CreateNotInstanceCharactersListFromPlayerList()
        {
            _notcurrentInstanceCharacters = new List<string>();
            _characterSelectedLimit = new Dictionary<string, int>();
            int characterSkinCount;

            foreach (var character in charactersData)
            {
                characterSkinCount = DEFAULT_SKIN + 1;
                _notcurrentInstanceCharacters.Add(character.CharacterName + DEMILITER_CHAR_FOR_CHARACTER_NAMES + DEFAULT_SKIN.ToString());
                _characterSelectedLimit.Add(character.CharacterName, 0);
                if (character.IA_Skins.Count != character.Player_skins.Count)
                {
                    Debug.LogWarning("Player skins and Ia skins of characterData " + character.name + " are different size. Make sure they are the same size.");
                    return false;
                }
                foreach (var characterSkin in character.Player_skins)
                {
                    _notcurrentInstanceCharacters.Add(character.CharacterName + DEMILITER_CHAR_FOR_CHARACTER_NAMES + characterSkinCount.ToString());
                    characterSkinCount++;
                }
            }
            return true;
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

