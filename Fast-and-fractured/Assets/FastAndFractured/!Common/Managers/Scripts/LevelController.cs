using System;
using System.Collections;
using System.Collections.Generic;
using StateMachine;
using UnityEngine;
using UnityEngine.SceneManagement;
using Utilities;
using Utilities.Managers.PauseSystem;
using Enums;
using UnityEngine.Events;
using UnityEngine.TextCore.Text;
using Random = UnityEngine.Random;

namespace FastAndFractured
{
    public class LevelController : AbstractSingleton<LevelController>
    {
        public bool usingController;
        [Header("Character Spawn")]
        public UnityEvent charactersCustomStart;
        [SerializeField] private List<CharacterData> charactersData;
        [SerializeField] private string playerCharacter = "Pepe_0";
        private int _currentPlayers = 1;

        public int MaxCharactersInGame { get => maxCharactersInGame; set => maxCharactersInGame = value; }
        [SerializeField]
        private int maxCharactersInGame = 8;
        private List<string> _allCharactersNameCode;
        private Dictionary<string, int> _characterSelectedLimit;
        public List<string> InGameCharactersNameCodes { get => _inGameCharactersNameCodes; }
        private List<string> _inGameCharactersNameCodes;
        public List<GameObject> InGameCharacters { get => _inGameCharacters; }
        private List<GameObject> _inGameCharacters;

        private CarMovementController _playerBindingInputs;
        [SerializeField]
        private CameraBehaviours playerCamera;

        [SerializeField]
        private GameObject[] spawnPoints;
        [SerializeField]
        private List<GameObject> safeZones;

        [Header("Game Loop")]
        [SerializeField]
        private float _timeToCallTheStorm = 190f;
        [SerializeField]
        private float _playerDeadReductionTime = 40f;
        private ITimer _callStormTimer;
        [SerializeField]
        private SandstormController _sandStormController;


        [Header("Injector prefabs")]
        [SerializeField] CarInjector PlayerPrefab;
        [SerializeField] CarInjector AIPrefab;

        [Header("Testing Values (Old Level Controller)")]
        private EnemyAIBrain[] _ais;
        [Tooltip("Debug mode allow to have characters in scene spawned. If you desactive this bool, remove all characters in scene or it will not work.")]
        [SerializeField] private bool debugMode = true;
        [Tooltip("Setting to false, will mean that the characters will be spawned in the Start, setting to true, you can use characters you place in the scene.")]
        [SerializeField] private bool useMyCharacters = false;
        [SerializeField] private bool stormInDebugMode = false;
        [SerializeField] private bool justSpawnAI = false;
        private GameObject _playerReference;
        public GameObject playerReference { get => _playerReference; }
        public bool HasPlayerWon { get => _hasPlayerWon; }
        private bool _hasPlayerWon = false;

        private int _aliveCharacterCount;
        public List<CharacterIcon> characterIcons;
        [SerializeField]
        private float endGameDelayTime = 0.5f;
        [SerializeField] private GameEndData gameEndDataScriptableObject;

        private const float DISTANCE_TO_CHANGE_TO_KM = 1000f;
        private const string METERS_TEXT = " m";
        private const string KILOMETERS_TEXT = " km";
        private const char DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE = '_';
        private const int LENGHT_RESULT_OF_SPLITTED_CHARACTER_NAME = 2;
        private const int DEFAULT_SKIN = 0;
        private const string ERROR_STRING_MESSAGE = "empty list";
        // Default values is 2. If you want to add more of two types of the same character,
        // increse this value. If you are trying to add only one type of character, set the same value as allCharactersNum. 
        private const int LIMIT_OF_SAME_CHARACTER_SPAWNED = 3;
        protected override void Awake()
        {
            base.Awake();
            //Provisional For Debug
            if (debugMode)
            {
                PlayerPrefs.SetString("Selected_Player", playerCharacter);
                PlayerPrefs.SetInt("Player_Num", 1);
                StatsController[] debugControllers = FindObjectsOfType<StatsController>();
                if (!useMyCharacters)
                {
                    foreach (var character in debugControllers)
                    {
                        character.gameObject.transform.parent.gameObject.SetActive(false);
                    }
                }
                else
                {
                    if (_ais == null)
                    {
                        _ais = FindObjectsOfType<EnemyAIBrain>();
                    }
                }
            }
            else
                useMyCharacters = false;
        }

        //Maybe in Onenable?
        void Start()
        {
            _aliveCharacterCount = maxCharactersInGame;
            Cursor.lockState = CursorLockMode.Locked;
            if (!useMyCharacters)
            {
                StartLevelWithSpawnedCharacters();
                GameObject nearestCharacter = GetNearestCharacterToCharacter(InGameCharacters[0].gameObject);
                EnemyAIBrain enemyAIBrain = nearestCharacter.GetComponentInParent<EnemyAIBrain>();
                if (enemyAIBrain)
                {
                    enemyAIBrain.ChoosePlayer();
                    enemyAIBrain.GetComponent<AIDebugStateChanger>().NextState();
                }
            }
            else
            {
                StartLevelWithOwnCharacters();
            }
        }

        private void Update()
        {

        }

        private void OnEnable()
        {
        }

        private void OnDisable()
        {
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;

        }

        private void StartLevelWithOwnCharacters()
        {
            _inGameCharacters = new List<GameObject>();

            EnemyAIBrain[] aIBrains = FindObjectsOfType<EnemyAIBrain>();


            PlayerInputController playerCar = FindObjectOfType<PlayerInputController>();
            _playerReference = FindObjectOfType<PlayerInputController>().GetComponentInChildren<StatsController>().gameObject;
            foreach (var aiBrain in aIBrains)
            {
                _inGameCharacters.Add(aiBrain.gameObject);

            }
            if (playerCar)
            {
                foreach (var aiBrain in aIBrains)
                {
                    aiBrain.Player = playerCar.gameObject;

                }
                _inGameCharacters.Add(playerCar.gameObject);


            }
            SetStormParameters(stormInDebugMode);
            charactersCustomStart?.Invoke();
        }

        private void StartLevelWithSpawnedCharacters()
        {
            SpawnInGameCharacters(out bool succeded, debugMode && justSpawnAI);
            if (!succeded)
            {
                Debug.LogError("Characters can't be spawned, read the warning messages for more information.");
            }
            else
            {
                IngameEventsManager.Instance.SetCharactersTopElements();
                if (debugMode)
                    SetStormParameters(stormInDebugMode);
                else
                    SetStormParameters(true);
            }
        }

        private void SetStormParameters(bool callStorm)
        {
            if (callStorm)
            {
                _sandStormController.SetSpawnPoints(debugMode);
                _callStormTimer = TimerSystem.Instance.CreateTimer(_timeToCallTheStorm, TimerDirection.DECREASE, onTimerDecreaseComplete: () => { CallStorm(); _callStormTimer = null; });
            }
        }

        #region SpawnCharacters
        private void SpawnInGameCharacters(out bool succeded, bool onlyAIs)
        {
            _inGameCharactersNameCodes = new List<string>();
            succeded = CreateAllCharactersNameCodesList();
            bool ignoreRepeatedCharacters;
            if (ignoreRepeatedCharacters = _allCharactersNameCode.Count < maxCharactersInGame)
            {
                Debug.LogWarning("Caution, there is not sufficient variety of characters on the characterData to spawn only " + LIMIT_OF_SAME_CHARACTER_SPAWNED + " skins of a same character. Game will run ignoring the limit of same character spawned.");
            }
            if (!onlyAIs)
            {
                string selectedPlayer = PlayerPrefs.GetString("Selected_Player");
                if (!succeded)
                    return;
                if (succeded = CheckIfCharacterExistInList(selectedPlayer, ignoreRepeatedCharacters))
                {
                    _inGameCharactersNameCodes.Add(selectedPlayer);
                }
                else
                    return;
            }
            int totalAICharacters = !onlyAIs ? maxCharactersInGame - _currentPlayers : maxCharactersInGame;
            for (int aiCharacterCount = 0; aiCharacterCount < totalAICharacters && succeded; aiCharacterCount++)
            {
                string aiName = GetRandomValueFromShuffleList(_allCharactersNameCode, ERROR_STRING_MESSAGE);
                if (aiName == ERROR_STRING_MESSAGE)
                {
                    Debug.LogWarning("Error, all characters form list _allCharactersNameCode where deleted. " +
                        "Make sure that you are adding more variety of characters or give LIMIT_OF_SAME_CHARACTER_SPAWNED and maxCharactersInGame the same value.");
                }
                if (succeded = CheckIfCharacterExistInList(aiName, ignoreRepeatedCharacters))
                {
                    _inGameCharactersNameCodes.Add(aiName);
                }
            }
            if (succeded)
            {
                SpawnCharactersInScene(onlyAIs);
            }
        }
        private void SpawnCharactersInScene(bool onlyAIs)
        {
            if (spawnPoints.Length >= maxCharactersInGame)
            {
                _inGameCharacters = new List<GameObject>();
                int allCharacters = _inGameCharactersNameCodes.Count;
                int charactersCount = 0;
                GameObject playerCar = null;
                GameObject player = null;
                ShuffleList(spawnPoints);
                for (; charactersCount < _currentPlayers && charactersCount < allCharacters && !onlyAIs; charactersCount++)
                {
                    CarInjector carInjector = Instantiate(PlayerPrefab, spawnPoints[charactersCount].transform.position, Quaternion.identity);
                    player = SearchCharacterInList(_inGameCharactersNameCodes[charactersCount]);
                    playerCar = carInjector.Install(player);

                    _inGameCharacters.Add(playerCar);
                    _playerBindingInputs = player.GetComponentInChildren<CarMovementController>();
                    _playerReference = playerCar;
                }
                for (; charactersCount < allCharacters; charactersCount++)
                {
                    var aiCharacter = SearchCharacterInList(_inGameCharactersNameCodes[charactersCount]);

                    CarInjector carInjector = Instantiate(AIPrefab, spawnPoints[charactersCount].transform.position, Quaternion.identity);
                    GameObject injectedCar = carInjector.Install(aiCharacter);
                    _inGameCharacters.Add(injectedCar);
                    //Provisional
                    carInjector.GetComponent<EnemyAIBrain>().Player = playerCar;
                }
                charactersCustomStart?.Invoke();

            }
        }

        private GameObject SearchCharacterInList(string nameCode)
        {
            DivideNameCode(nameCode, out string name, out int skinNum);
            foreach (var character in charactersData)
            {
                if (character.CharacterName == name)
                {
                    if (skinNum == DEFAULT_SKIN)
                    {
                        return character.CarDefaultPrefab;
                    }
                    if (skinNum - 1 < character.CarWithSkinsPrefabs.Count)
                    {
                        return character.CarWithSkinsPrefabs[skinNum - 1];
                    }
                }
            }
            return null;
        }

        private bool CreateAllCharactersNameCodesList()
        {
            _allCharactersNameCode = new List<string>();
            _characterSelectedLimit = new Dictionary<string, int>();
            int characterSkinCount;

            foreach (var character in charactersData)
            {
                characterSkinCount = DEFAULT_SKIN + 1;
                _allCharactersNameCode.Add(character.CharacterName + DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + DEFAULT_SKIN.ToString());
                _characterSelectedLimit.Add(character.CharacterName, 0);

                foreach (var characterSkin in character.CarWithSkinsPrefabs)
                {
                    _allCharactersNameCode.Add(character.CharacterName + DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + characterSkinCount.ToString());
                    characterSkinCount++;
                }

                if ((character.CarWithSkinsPrefabs.Count + 1) < LIMIT_OF_SAME_CHARACTER_SPAWNED)
                {
                    int differenceBetween = LIMIT_OF_SAME_CHARACTER_SPAWNED - (character.CarWithSkinsPrefabs.Count + 1);
                    for (int i = 0; i < differenceBetween; i++)
                    {
                        _allCharactersNameCode.Add(character.CharacterName + DELIMITER_CHAR_FOR_CHARACTER_NAMES_CODE + DEFAULT_SKIN.ToString());
                    }
                }
            }
            return true;
        }

        private void RemoveSelectedCharacterFromAllCharactersNameCodes(string nameCode, string nameWithoutCode)
        {
            _characterSelectedLimit[nameWithoutCode]++;
            _allCharactersNameCode.Remove(nameCode);
            if (_characterSelectedLimit[nameWithoutCode] >= LIMIT_OF_SAME_CHARACTER_SPAWNED)
            {
                List<string> copyOfAllCharactersNameCode = new List<string>(_allCharactersNameCode);
                for (int notCurrentInstanceCharacterCount = 0; notCurrentInstanceCharacterCount < copyOfAllCharactersNameCode.Count; notCurrentInstanceCharacterCount++)
                {
                    if (copyOfAllCharactersNameCode[notCurrentInstanceCharacterCount].Contains(nameWithoutCode) &&
                        _allCharactersNameCode.Contains(copyOfAllCharactersNameCode[notCurrentInstanceCharacterCount]))
                    {
                        _allCharactersNameCode.Remove(copyOfAllCharactersNameCode[notCurrentInstanceCharacterCount]);
                    }
                }
            }
        }

        private bool CheckIfCharacterExistInList(string nameCode, bool ignoreRepeated)
        {
            if (_allCharactersNameCode.Contains(nameCode))
            {
                DivideNameCode(nameCode, out string name);
                if (!ignoreRepeated)
                {
                    RemoveSelectedCharacterFromAllCharactersNameCodes(nameCode, name);
                }
                return true;
            }
            Debug.LogWarning("Name code " + nameCode + " given for the character don't exist or was not saved. Make sure the format 'Josefino_0' is correct or the character is in the charactersData list.");
            return false;
        }
        #endregion

        public void OnPlayerDead(float delayTime, GameObject character, bool isPlayer)
        {
            if (_callStormTimer != null)
            {
                if (TimerSystem.Instance.HasTimer(_callStormTimer))
                {
                    TimerData stormTimer = _callStormTimer.GetData();
                    float newTime = stormTimer.CurrentTime - _playerDeadReductionTime;
                    Mathf.Clamp(newTime, 0, _timeToCallTheStorm);
                    TimerSystem.Instance.ModifyTimer(_callStormTimer, newCurrentTime: newTime);
                }
            }
            TimerSystem.Instance.CreateTimer(delayTime, onTimerDecreaseComplete: () =>
            {
                if (!isPlayer)
                {
                    _inGameCharacters.Remove(character);

                    foreach (CharacterIcon icon in characterIcons)
                    {
                        if (icon.Character == character)
                        {
                            icon.SetPlayerDeadIconIsActive(true);
                        }
                    }

                    Destroy(character.transform.parent.gameObject);
                    _aliveCharacterCount--;
                    if (_aliveCharacterCount == 1)
                    {
                        _hasPlayerWon = true;
                        GetPlayerFinalStatsAndChangeScene();
                    }
                }
                else
                {
                    Debug.Log("Player Dead.");
                    _hasPlayerWon = false;
                    GetPlayerFinalStatsAndChangeScene();
                }
            });
        }

        private void CallStorm()
        {
            Debug.Log("Storm Called");
            if (!_sandStormController.StormSpawnPointsSetted)
                _sandStormController.SetSpawnPoints(debugMode);
            _sandStormController.SpawnFogs();
            _sandStormController.MoveSandStorm = true;
        }

        public bool IsInsideSandstorm(GameObject target)
        {
            return _callStormTimer == null ? _sandStormController.IsInsideStormCollider(target, 0f) : false;
        }

        public bool IsInsideSandstorm(GameObject target, float marginError)
        {
            return _callStormTimer == null ? _sandStormController.IsInsideStormCollider(target, marginError) : false;
        }

        public List<GameObject> SafeZonesOutsideSandstorm()
        {
            List<GameObject> safeZonesOutsideSandstorm = new List<GameObject>();
            foreach (var safeZone in safeZones)
            {
                if (!IsInsideSandstorm(safeZone))
                {
                    safeZonesOutsideSandstorm.Add(safeZone);
                }
            }
            return safeZonesOutsideSandstorm;
        }

        public bool AreAllThisGameElementsInsideSandstorm(GameElement gameElement)
        {
            if (_callStormTimer != null)
                return false;
            List<GameObject> interactablesList = new List<GameObject>();
            if (gameElement == GameElement.INTERACTABLE)
            {
                foreach (var item in InteractableHandler.Instance.GetStatBoostItems())
                {
                    interactablesList.Add(item.gameObject);
                }
            }
            switch (gameElement)
            {
                case GameElement.CHARACTER:
                    return CheckIfList1ElementsAreInList2(_inGameCharacters, _sandStormController.CharactersInsideSandstorm);
                case GameElement.INTERACTABLE:
                    return CheckIfList1ElementsAreInList2(interactablesList, _sandStormController.ItemsInsideSandstorm);
                case GameElement.SAFE_ZONES:
                    return CheckIfList1ElementsAreInList2(safeZones, _sandStormController.SafeZonesInsideSandstorm);
            }
            return false;
        }

        private bool CheckIfList1ElementsAreInList2<T>(List<T> list1, List<T> list2)
        {
            if (list1 == null || list2 == null) return false;
            if (list1.Count == 0 || list2.Count == 0) return false;
            foreach (T item in list1)
            {
                if (!list2.Contains(item))
                {
                    return false;
                }
            }
            return true;
        }

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

        public void ShuffleList<T>(IList<T> list)
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
        private void GetPlayerFinalStatsAndChangeScene()
        {
            float distance = _playerReference.GetComponent<StatsController>().totalDistanceDriven;
            string totalDistanceText;
            if (distance < DISTANCE_TO_CHANGE_TO_KM)
            {
                distance = Mathf.Round(distance);
                totalDistanceText = distance.ToString() + METERS_TEXT;
            }
            else
            {
                distance = Mathf.Round(distance / DISTANCE_TO_CHANGE_TO_KM * 10) / 10;
                totalDistanceText = distance.ToString() + KILOMETERS_TEXT;
            }
            GameObject finalAnimation;
            if (_hasPlayerWon)
            {
                finalAnimation = _playerReference.GetComponent<StatsController>().GetWinObjectByString(PlayerPrefs.GetString("Selected_Player"));
            }
            else
            {
                finalAnimation = _playerReference.GetComponent<StatsController>().GetLoseObjectByString(PlayerPrefs.GetString("Selected_Player"));
            }
            gameEndDataScriptableObject.isWin = _hasPlayerWon;
            gameEndDataScriptableObject.totalDamageDealt = _playerReference.GetComponent<StatsController>().totalDamageDealt.ToString(); ;
            gameEndDataScriptableObject.totalDamageTaken = _playerReference.GetComponent<StatsController>().totalDamageTaken.ToString();
            gameEndDataScriptableObject.totalDistanceTraveled = totalDistanceText;
            gameEndDataScriptableObject.finalAnimation = finalAnimation;
            MainMenuManager.Instance.LoadScene(0);
        }
        public GameObject GetNearestCharacterToCharacter(GameObject sourceCharacter)
        {
            List<GameObject> inGameCharacters = this.InGameCharacters;
            if (inGameCharacters.Count > 1)
            {
                GameObject nearestTarget = inGameCharacters[0].gameObject != sourceCharacter.gameObject ? inGameCharacters[0] : inGameCharacters[1];
                float nearestOne = float.MaxValue;

                foreach (GameObject character in inGameCharacters)
                {
                    if (!character) continue;
                    float characterDistance = (character.transform.position - sourceCharacter.transform.position).sqrMagnitude;
                    if (characterDistance < nearestOne && character.gameObject != sourceCharacter.gameObject)
                    {
                        nearestOne = characterDistance;
                        nearestTarget = character;
                    }
                }
                return nearestTarget;
            }
            return null;
        }
    }


}

