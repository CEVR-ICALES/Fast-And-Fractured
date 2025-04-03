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
        [Header("Testing Values")]
        [SerializeField] private List<StatsController> characters;
        [SerializeField] private EnemyAIBrain ai;
        [SerializeField] private List<KillCharacterNotify> killCharacterHandles;
        [SerializeField] private List<Controller> controllers;
        [Tooltip("Setting to false, will mean that the characters will be spawned, setting to true, you can use characters you place in the scene.")]
        [SerializeField] private bool testing = false;
        [Header("Character Spawn")]
        public UnityEvent charactersCustomStart;
        [SerializeField] private List<CharacterData> charactersData;
        private int _currentPlayers = 1;

        public int AllCharactersNum { get => allCharactersNum; set => allCharactersNum = value; }
        [SerializeField]
        private int allCharactersNum = 8;
        private List<string> _allCharactersNameCode;
        private Dictionary<string, int> _characterSelectedLimit;

        private List<string> _inGameCharactersNameCodes;
        public List<GameObject> InGameCharacters { get => _inGameCharacters; }
        private List<GameObject> _inGameCharacters;

        private CarMovementController _playerBindingInputs;
        [SerializeField]
        private CameraBehaviours playerCamera;

        [SerializeField]
        private GameObject[] spawnPoints;

        private const char DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE = '_';
        private const int LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME = 2;
        private const int DEFAULT_SKIN = 0;
        private const string ERROR_STRING_MESSAGE = "empty list";
        // Default values is 2. If you want to add more of two types of the same character,
        // increse this value. If you are trying to add only one type of character, set the same value as allCharactersNum. 
        private const int LIMIT_OF_SAME_CHARACTER_SPAWNED = 8;

        // Start is called before the first frame update
        protected override void Awake()
        {
            base.Awake();
            if (!ai)
            {
                ai = FindObjectOfType<EnemyAIBrain>();
            }
        }

        //Maybe in Onenable?
        void Start()
        {
            if (!testing)
            {
                PlayerPrefs.SetString("Selected_Player", "Josefino_0");
                PlayerPrefs.SetInt("Player_Num", 1);
                SpawnInGameCharacters(out bool succeded);
                if (!succeded)
                    Debug.LogError("Characters can't be spawned, read the warning messages for more information.");
            }
            else
            {
                StartLevel();
            }
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
            _playerBindingInputs.HandleInputChange(usingController);
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
                Controller controller = character.GetComponentInParent<Controller>();
               if (controller && controller.CompareTag("Player"))
                {
                    _playerBindingInputs = character.GetComponentInChildren<CarMovementController>();
                    ai.Player = character.transform.gameObject;
                }
            }
            foreach (var killCharacterHandle in killCharacterHandles)
            {
                killCharacterHandle.onTouchCharacter += EliminatePlayer;
            }
             charactersCustomStart?.Invoke();
        }

        private void SpawnInGameCharacters(out bool succeded)
        {
            _inGameCharactersNameCodes = new List<string>();
           succeded = CreateNotInstanceCharactersListFromPlayerList();
           string selectedPlayer = PlayerPrefs.GetString("Selected_Player");
            if (!succeded)
                return;
            if (succeded = CheckIfCharacterExistInList(selectedPlayer))
            {
                _inGameCharactersNameCodes.Add(selectedPlayer);
            }
            else
                return;
            int totalIaCharacters = allCharactersNum - _currentPlayers;
            for(int iaCharacterCount = 0; iaCharacterCount < totalIaCharacters&&succeded; iaCharacterCount++)
            {
                string iaName = GetRandomValueFromShuffleList(_allCharactersNameCode, ERROR_STRING_MESSAGE);
                if (iaName==ERROR_STRING_MESSAGE)
                {
                    Debug.LogWarning("Error, all characters form list " + _allCharactersNameCode + " where deleted. " +
                        "Make sure that you are adding more variety of characters or give LIMIT_OF_SAME_CHARACTER_SPAWNED and allCharactersNum the same value.");
                }
                if (succeded = CheckIfCharacterExistInList(iaName))
                {
                    _inGameCharactersNameCodes.Add(iaName);
                }
            }
            if (succeded)
            {
                _currentPlayers = PlayerPrefs.GetInt("Player_Num");
                SpawnCharactersInScene();
            }
        }

        #region SpawnCharacters

        private void SpawnCharactersInScene()
        {
            if (spawnPoints.Length >= allCharactersNum)
            {
                _inGameCharacters = new List<GameObject>();
                int allCharacters = _inGameCharactersNameCodes.Count;
                int charactersCount = 0;
                GameObject player = null;
                ShuffleList(spawnPoints);
                for (; charactersCount < _currentPlayers&&charactersCount<allCharacters; charactersCount++)
                {
                    player = SearchCharacterInList(_inGameCharactersNameCodes[charactersCount],true);
                    player = Instantiate(player, spawnPoints[charactersCount].transform.position,Quaternion.identity);
                    _inGameCharacters.Add(player);
                    _playerBindingInputs = player.GetComponentInChildren<CarMovementController>();
                }
                for(;charactersCount < allCharacters; charactersCount++)
                {
                    GameObject ia = SearchCharacterInList(_inGameCharactersNameCodes[charactersCount], false);
                   ia = Instantiate(ia, spawnPoints[charactersCount].transform.position, Quaternion.identity);
                    _inGameCharacters.Add(ia);
                    //Provisional
                    ia.GetComponent<EnemyAIBrain>().Player = player;
                }
                charactersCustomStart?.Invoke();
                playerCamera.SetCameraParameters(player.transform, player.transform.Find("CameraLookAtPoint"));
            }
        }

        private GameObject SearchCharacterInList(string nameCode, bool player)
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
            _allCharactersNameCode = new List<string>();
            _characterSelectedLimit = new Dictionary<string, int>();
            int characterSkinCount;

            foreach (var character in charactersData)
            {
                characterSkinCount = DEFAULT_SKIN + 1;
                _allCharactersNameCode.Add(character.CharacterName + DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + DEFAULT_SKIN.ToString());
                _characterSelectedLimit.Add(character.CharacterName, 0);
                if (character.IA_Skins.Count != character.Player_skins.Count)
                {
                    Debug.LogWarning("Player skins and Ia skins of characterData " + character.name + " are different size. Make sure they are the same size.");
                    return false;
                }
                foreach (var characterSkin in character.Player_skins)
                {
                    _allCharactersNameCode.Add(character.CharacterName + DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + characterSkinCount.ToString());
                    characterSkinCount++;
                }
            }
            return true;
        }

        private void RemoveSelectedCharacterFromNotInstanceCharacters(string nameCode, string nameWithoutCode)
        {
            _characterSelectedLimit[nameWithoutCode]++;
            _allCharactersNameCode.Remove(nameCode);
            if (_characterSelectedLimit[nameWithoutCode] >= LIMIT_OF_SAME_CHARACTER_SPAWNED)
            {
                List<string> copyOfAllCharactersNameCode = new List<string>(_allCharactersNameCode);
                for (int notCurrentInstanceCharacterCount = 0; notCurrentInstanceCharacterCount < copyOfAllCharactersNameCode.Count; notCurrentInstanceCharacterCount++)
                {
                    if (copyOfAllCharactersNameCode[notCurrentInstanceCharacterCount].Contains(nameWithoutCode)&&
                        _allCharactersNameCode.Contains(copyOfAllCharactersNameCode[notCurrentInstanceCharacterCount]))
                    {
                        _allCharactersNameCode.Remove(copyOfAllCharactersNameCode[notCurrentInstanceCharacterCount]);
                    }
                }
            }
        }

        private bool CheckIfCharacterExistInList(string nameCode)
        {
            if (_allCharactersNameCode.Contains(nameCode))
            {
                DivideNameCode(nameCode,out string name);
                RemoveSelectedCharacterFromNotInstanceCharacters(nameCode, name);
                return true;
            }
            Debug.LogWarning("Name code " + nameCode + " given for the character don't exist or was not saved. Make sure the format 'Josefino_0' is correct or the character is in the charactersData list.");
            return false;
        }
        #endregion
        #region Resources
        private T GetRandomValueFromShuffleList<T>(List<T> list, T defaultValue)
        {
            if (list.Count > 0)
            {
                //Shuffle the list
                ShuffleList(list);
                return list[Random.Range(0, _allCharactersNameCode.Count)];
            }
            return defaultValue;
        }

        private void ShuffleList<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private void DivideNameCode(string nameCode, out string name, out int skinNum)
        {
            name = " ";
            skinNum = -1;
            string[] dividedNameCode = nameCode.Split(DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE);
            if (dividedNameCode.Length == LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME)
            {
                name = dividedNameCode[0];
                if (_characterSelectedLimit.ContainsKey(name))
                {
                    if (!int.TryParse(dividedNameCode[1], out skinNum))
                    {
                        skinNum = -1;
                    }
                }
                else
                {
                    name = " ";
                }
            }
        }

        private void DivideNameCode(string nameCode, out string name)
        {
            name = " ";
            string[] dividedNameCode = nameCode.Split(DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE);
            if (dividedNameCode.Length == LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME)
            {
                name = dividedNameCode[0];
                if (!_characterSelectedLimit.ContainsKey(name))
                {
                    name = " ";
                }
            }
        }
        #endregion

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

