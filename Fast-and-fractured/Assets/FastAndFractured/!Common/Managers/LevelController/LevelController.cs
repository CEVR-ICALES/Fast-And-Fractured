using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class LevelController : AbstractSingleton<LevelController>
    {
        #region Original Variables 
        [Header("Character Spawn")]
        public UnityEvent onLevelPreStart;
        public UnityEvent charactersCustomStart;
        [SerializeField] private ListOfCharactersData charactersDataScriptableObject;
        private List<CharacterData> _charactersData;
        [SerializeField] private string playerCharacter = "Pepe_0";
        public int MaxCharactersInGame { get => maxCharactersInGame; set => maxCharactersInGame = value; }
        [SerializeField] private int maxCharactersInGame = 8;

        // The setter exists for the multiplayer client path: the wrapper assigns the
        // server-authoritative name-code list before initializing the client level state.
        public List<string> InGameCharactersNameCodes { get => _characterSpawner?.InGameCharactersNameCodes ?? new List<string>(); set { if (_characterSpawner != null) _characterSpawner.InGameCharactersNameCodes = value; } }


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
        /// <summary>Seconds until the storm is called; null when no countdown is running (StormCountdownHud).</summary>
        public float? StormSecondsLeft => _gameLoopManager?.StormSecondsLeft;
        // Exposed for the multiplayer wrapper, which reads the debug flag off the real controller.
        public bool DebugMode { get => debugMode; set => debugMode = value; }
        public bool HasGameStarted { get; private set; }
        private bool _isStormActive = false;

        [SerializeField] private List<string> _playersCharacterNameCodes;
        protected override void Construct()
        {
            base.Construct();
            _charactersData = charactersDataScriptableObject.listOfCharactersData;
            InitializeHandlers();
            //Provisional For Debug 
            if (debugMode)
            {
                PlayerPrefs.SetString("Selected_Player", playerCharacter);
                PlayerPrefs.SetInt("Player_Num", 1);

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
            _characterDataProvider = new CharacterDataProvider(_charactersData);
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
                PlayerInputController playerCtrl = FindFirstObjectByType<PlayerInputController>();

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



            EnemyAIBrain[] aiBrains = FindObjectsByType<EnemyAIBrain>(FindObjectsSortMode.None);
            foreach (var aiBrain in aiBrains)
            {
                foundCharacters.Add(aiBrain.GetComponentInChildren<StatsController>().gameObject);
                aiBrain.Player = GetARandomPlayer();

            }

            InitializeAfterSpawn(foundCharacters);
        }

        // Extracted so the multiplayer wrapper can initialize the level with server-spawned
        // characters. Order matters: onLevelPreStart fires immediately, then
        // StartGameInitializationDelay -> OnGameStartDelayComplete activates the controls.
        public void InitializeAfterSpawn(List<GameObject> injectedCharacters = null)
        {
            Cursor.lockState = CursorLockMode.Locked;
            _isStormActive = false;

            // Null-safe: In multiplayer, remote clients may not have PlayerInputController yet
            if (PlayerInputController.Instance != null && PlayerInputController.Instance.GetComponentInChildren<CarMovementController>() != null)
            {
                _localPlayer = PlayerInputController.Instance.GetComponentInChildren<CarMovementController>().gameObject;
            }

            var foundInjectors = FindObjectsByType<CarInjector>(sortMode: FindObjectsSortMode.None);
            foreach (var injector in foundInjectors)
            {
                injector.Install(null, true);
            }
            if (injectedCharacters != null)
            {
                _characterSpawner.InGameCharacters.Clear();
                _characterSpawner.InGameCharacters.AddRange(injectedCharacters);
            }
            _gameLoopManager.InitializeSession(
                _characterSpawner.InGameCharacters.Count,
                _characterSpawner.InGameCharacters, _localPlayer, charactersCustomStart);
            _gameLoopManager.SetupStorm(stormInDebugMode, debugMode);
            onLevelPreStart?.Invoke();
            _gameLoopManager.StartGameInitializationDelay(delayUntilGameStarts, OnGameStartDelayComplete);
        }

        private void StartLevelWithSpawnedCharacters()
        {
            Debug.Log("Starting level with spawned characters.");
            if (_playersCharacterNameCodes.Count == 0)
            {
                string actualPlayerCharacter = justSpawnAI ? "" : PlayerPrefs.GetString("Selected_Player", playerCharacter);
                _playersCharacterNameCodes.Add(actualPlayerCharacter);
                availablePlayer.Add(new GameObject("symbolic player WIP"));
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

            if (IngameEventsManager.Instance != null)
            {
                IngameEventsManager.Instance.SetCharactersTopElements();
                //todo replace by event callback or put on onLevelPreStart
            }

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
            HasGameStarted = true;

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
            // GetRandomValueFromList has both an (IRandom) and a (T defaultValue) overload, so a
            // no-arg call is ambiguous. The deterministic generator is also what the rest of the
            // spawning uses, so every peer picks the same character.
            return availablePlayer.GetRandomValueFromList(DeterministicRandom.Instance);
        }
        public void AddCharacterToListOfSelectedCharacters(int id, string character)
        {
            if (id == -1)
            {
                id++;
            }

            if (id < 0)
            {
                Debug.LogWarning($"Invalid player id {id} while adding character '{character}'.");
                return;
            }

            // Keep list indexed by clientId even if selections arrive out of order.
            while (_playersCharacterNameCodes.Count <= id)
            {
                _playersCharacterNameCodes.Add(string.Empty);
            }

            _playersCharacterNameCodes[id] = character;
        }

        /// <summary>
        /// Clears stale character selections for disconnected clientIds while preserving
        /// index-by-clientId semantics for active players.
        /// </summary>
        public void PruneSelectedCharactersToActiveClientIds(IReadOnlyCollection<int> activeClientIds)
        {
            if (_playersCharacterNameCodes == null || _playersCharacterNameCodes.Count == 0)
                return;

            if (activeClientIds == null || activeClientIds.Count == 0)
            {
                for (int i = 0; i < _playersCharacterNameCodes.Count; i++)
                    _playersCharacterNameCodes[i] = string.Empty;
                return;
            }

            var activeSet = new HashSet<int>(activeClientIds);
            for (int i = 0; i < _playersCharacterNameCodes.Count; i++)
            {
                if (!activeSet.Contains(i))
                    _playersCharacterNameCodes[i] = string.Empty;
            }
        }

    }
}
