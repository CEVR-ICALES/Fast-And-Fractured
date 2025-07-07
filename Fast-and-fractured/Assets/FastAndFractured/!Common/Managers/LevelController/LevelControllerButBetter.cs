using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class LevelControllerButBetter : AbstractSingleton<LevelControllerButBetter>
    {
        #region Original Variables 
        [Header("Character Spawn")]
        public UnityEvent onLevelPreStart;
        public UnityEvent charactersCustomStart;
        [SerializeField] private List<CharacterData> charactersData;
        [SerializeField] private string playerCharacter = "Pepe_0";
        public int MaxCharactersInGame { get => maxCharactersInGame; set => maxCharactersInGame = value; }
        [SerializeField] private int maxCharactersInGame = 8;

        public List<string> InGameCharactersNameCodes { get => _characterSpawner?.InGameCharactersNameCodes ?? new List<string>(); }


        [SerializeField] private GameObject[] spawnPoints;
        [SerializeField] private List<GameObject> safeZones;

        [Header("Game Loop")]
        [SerializeField] private float _timeToCallTheStorm = 190f;
        [SerializeField] private float _playerDeadReductionTime = 40f;
        [SerializeField] private SandstormController _sandStormController;


        [Header("Injector prefabs")]
        [SerializeField] CarInjector PlayerPrefab;
        [SerializeField] CarInjector AIPrefab;

        [Header("Testing Values (Old Level Controller)")]
        [Tooltip("Debug mode allow to have characters in scene spawned. If you desactive this bool, remove all characters in scene or it will not work.")]
        [SerializeField] private bool debugMode = true;
        [Tooltip("Setting to false, will mean that the characters will be spawned in the Start, setting to true, you can use characters you place in the scene.")]
        [SerializeField] private bool useMyCharacters = false;
        [SerializeField] private bool stormInDebugMode = false;
        [SerializeField] private bool justSpawnAI = false;


        public GameObject LocalPlayer { get => _localPlayer; }
        private GameObject _localPlayer;


        [SerializeField] private float delayUntilGameStarts = 3.5f;
        public List<CharacterIcon> characterIcons;
        [SerializeField] private float endGameDelayTime = 0.5f;
        [SerializeField] private GameEndData gameEndDataScriptableObject;
        #endregion


        private CharacterDataProvider _characterDataProvider;
        private CharacterSpawner _characterSpawner;
        private GameLoopManager _gameLoopManager;
        private SandstormInteractionManager _sandstormInteractionManager;
        private GameEndHandler _gameEndHandler;

        [SerializeField] public List<GameObject> availablePlayer;
        public List<GameObject> InGameCharacters => _characterSpawner?.InGameCharacters ?? new List<GameObject>();
        public bool HasPlayerWon => _gameLoopManager?.HasPlayerWon ?? false;
        private bool _isStormActive = false;

        [SerializeField] private List<string> _playersCharacterNameCodes;

        [SerializeField] private int defaultPlayerCount = 1;
        protected override void Construct()
        {
            base.Construct();
            InitializeHandlers();

            //Provisional For Debug 
            if (debugMode)
            {
                PlayerPrefs.SetString("Selected_Player", playerCharacter);
                PlayerPrefs.SetInt("Player_Num", defaultPlayerCount);

                if (!useMyCharacters)
                {
                    StatsController[] debugControllers = FindObjectsByType<StatsController>(FindObjectsSortMode.None);
                    foreach (var character in debugControllers)
                    {
                        if (character.transform.parent != null) character.transform.parent.gameObject.SetActive(false);
                        else character.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                useMyCharacters = false;
            }
        }

        private void InitializeHandlers()
        {
            _characterDataProvider = new CharacterDataProvider(charactersData);
            _characterSpawner = new CharacterSpawner(_characterDataProvider, PlayerPrefab, AIPrefab, spawnPoints);
            _gameLoopManager = new GameLoopManager(_timeToCallTheStorm, _playerDeadReductionTime, _sandStormController);
            _sandstormInteractionManager = new SandstormInteractionManager(_sandStormController, safeZones, false);
            _gameEndHandler = new GameEndHandler(gameEndDataScriptableObject);
        }

        protected override void Initialize()
        {
            Cursor.lockState = CursorLockMode.Locked;
            _isStormActive = false;

            if (useMyCharacters && debugMode)
            {
                StartLevelWithOwnCharacters();

            }

            else
            {
                StartLevelWithSpawnedCharacters();

            }
        }

        private void StartLevelWithOwnCharacters()
        {
            Debug.Log("Starting level with pre-placed characters (debug mode).");
            List<GameObject> foundCharacters = new List<GameObject>();
            if (availablePlayer.Count == 0)
            {
                PlayerInputController playerCtrl = FindObjectOfType<PlayerInputController>();

                if (playerCtrl != null)
                {
                    StatsController playerStats = playerCtrl.GetComponentInChildren<StatsController>();
                    if (playerStats != null)
                    {
                        _localPlayer = playerStats.gameObject;
                        foundCharacters.Add(playerCtrl.gameObject);
                    }
                    else Debug.LogError("PlayerInputController found, but no child StatsController.");
                }
                else if (!justSpawnAI)
                {
                    Debug.LogWarning("useMyCharacters is true, but no PlayerInputController found in scene.");
                }

            }
            else
            {


            }



            EnemyAIBrain[] aiBrains = FindObjectsOfType<EnemyAIBrain>();
            foreach (var aiBrain in aiBrains)
            {
                foundCharacters.Add(aiBrain.GetComponentInChildren<StatsController>().gameObject);
                aiBrain.Player = GetARandomPlayer();

            }

            _characterSpawner.InGameCharacters.Clear();
            _characterSpawner.InGameCharacters.AddRange(foundCharacters);
            _gameLoopManager.InitializeSession(foundCharacters.Count, foundCharacters, _localPlayer, charactersCustomStart);
            _gameLoopManager.SetupStorm(stormInDebugMode, debugMode);
            _gameLoopManager.ActivateCharacterControls();
        }

        private void StartLevelWithSpawnedCharacters()
        {
            Debug.Log("Starting level with spawned characters.");
            if (_playersCharacterNameCodes.Count == 0)
            {
                string actualPlayerCharacter = justSpawnAI ? "" : PlayerPrefs.GetString("Selected_Player", playerCharacter);
                _playersCharacterNameCodes.Add(actualPlayerCharacter);
                availablePlayer.Add(new GameObject("symbolic player WIP"));
                PlayerPrefs.SetInt("Player_Num", defaultPlayerCount);

            }

            int actualCurrentPlayers = justSpawnAI ? 0 : PlayerPrefs.GetInt("Player_Num", availablePlayer.Count);


            bool spawnSucceeded = _characterSpawner.TrySpawnCharacters(
                maxCharactersInGame,
                _playersCharacterNameCodes,
                actualCurrentPlayers,
                justSpawnAI,
                out GameObject playerForAI
            );

            if (!spawnSucceeded || InGameCharacters.Count == 0)
            {
                Debug.LogError("Character spawning failed or resulted in no characters. Level cannot continue as intended.");
                return;
            }

            _localPlayer = _characterSpawner.PlayerReference;

            _gameLoopManager.InitializeSession(maxCharactersInGame, InGameCharacters, _localPlayer, charactersCustomStart);

            bool shouldCallStorm = !debugMode || stormInDebugMode;
            _gameLoopManager.SetupStorm(shouldCallStorm, debugMode);

            onLevelPreStart?.Invoke();
            _gameLoopManager.StartGameInitializationDelay(delayUntilGameStarts, OnGameStartDelayComplete);

            if (!useMyCharacters && InGameCharacters.Count > 0)
            {
                GameObject firstCharacter = InGameCharacters[0];
                GameObject nearestToFirst = GetNearestCharacterToCharacter(firstCharacter);
                if (nearestToFirst != null)
                {
                    EnemyAIBrain enemyAIBrain = nearestToFirst.GetComponentInParent<EnemyAIBrain>();
                    if (enemyAIBrain)
                    {
                        enemyAIBrain.ChoosePlayer();
                        var aiDebugChanger = nearestToFirst.GetComponent<AIDebugStateChanger>();
                        if (aiDebugChanger) aiDebugChanger.NextState();
                    }
                }
            }
        }

        private void OnGameStartDelayComplete()
        {
            _gameLoopManager.ActivateCharacterControls();

            foreach (var character in InGameCharacters)
            {
                character.GetComponent<StatsController>().onDead.AddListener(ReportCharacterDeath);
            }
        }
        public void ReportCharacterDeath(float delay, GameObject character, bool isPlayer)
        {
            character.GetComponent<StatsController>().onDead.RemoveListener(ReportCharacterDeath);
            _gameLoopManager.OnCharacterDied(character, isPlayer, endGameDelayTime, characterIcons,
                ProcessPlayerWin, ProcessPlayerLoss);
        }

        private void ProcessPlayerWin()
        {
            // Called by GameLoopManager when player wins
            Debug.Log("Player has won!");
            _gameEndHandler.ProcessGameEnd(true, _localPlayer, "Selected_Player");
        }

        private void ProcessPlayerLoss()
        {
            // Called by GameLoopManager when player loses (player character died)
            Debug.Log("Player has lost!");
            _gameEndHandler.ProcessGameEnd(false, _localPlayer, "Selected_Player");
        }

        #region Public Accessors / Wrappers
        // This method is called by SandstormController when storm is actually active
        public void NotifyStormIsActive()
        {
            _isStormActive = true;
        }

        public bool IsInsideSandstorm(GameObject target, float marginError = 0f)
        {
            return _isStormActive && _sandstormInteractionManager.IsInsideSandstorm(target, marginError);
        }

        public List<GameObject> SafeZonesOutsideSandstorm()
        {
            return _sandstormInteractionManager.GetSafeZonesOutsideSandstorm(_isStormActive);
        }

        public bool AreAllThisGameElementsInsideSandstorm(GameElement gameElement)
        {
            return _sandstormInteractionManager.AreAllGameElementsInsideSandstorm(gameElement, InGameCharacters, _isStormActive);
        }

        public GameObject GetNearestCharacterToCharacter(GameObject sourceCharacter)
        {
            if (InGameCharacters.Count <= 1 || sourceCharacter == null) return null;

            GameObject nearestTarget = null;
            float nearestDistanceSqr = float.MaxValue;

            foreach (GameObject character in InGameCharacters)
            {
                if (character == null || character == sourceCharacter) continue;

                float distanceSqr = (character.transform.position - sourceCharacter.transform.position).sqrMagnitude;
                if (distanceSqr < nearestDistanceSqr)
                {
                    nearestDistanceSqr = distanceSqr;
                    nearestTarget = character;
                }
            }
            return nearestTarget;
        }
        #endregion

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
        }

        public GameObject GetARandomPlayer()
        {
            return availablePlayer.GetRandomValueFromList();
        }
        public void AddCharacterToListOfSelectedCharacters(int id,string character) {
            if (id == -1) {

                id++;
            }
            
            if(id>= _playersCharacterNameCodes.Count)
            _playersCharacterNameCodes.Add(character);
            else
            {
                _playersCharacterNameCodes[id] = character;
            }
        }

    }
}