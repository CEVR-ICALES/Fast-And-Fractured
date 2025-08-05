using StateMachine;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class CharacterSpawner
    {
        private readonly CharacterDataProvider _characterDataProvider;
        private readonly CarInjector _playerPrefab;
        private readonly CarInjector _aiPrefab;
        private readonly GameObject[] _spawnPoints;

        private List<string> _availableNameCodesForSpawning;
        private Dictionary<string, int> _characterSelectionCount;

        public List<string> InGameCharactersNameCodes { get;   set; }
        public List<GameObject> InGameCharacters { get; private set; }
        public GameObject PlayerReference { get; private set; }

        public CharacterSpawner(CharacterDataProvider dataProvider, CarInjector playerPrefab, CarInjector aiPrefab, GameObject[] spawnPoints)
        {
            _characterDataProvider = dataProvider;
            _playerPrefab = playerPrefab;
            _aiPrefab = aiPrefab;
            _spawnPoints = spawnPoints;

            InGameCharactersNameCodes = new List<string>();
            InGameCharacters = new List<GameObject>();
            _characterSelectionCount = new Dictionary<string, int>();
            _availableNameCodesForSpawning = new List<string>();
        }

        private bool PrepareSpawnList(int maxCharactersInGame, List<string> playersCharacterNameCodes, int currentPlayers)
        {
            _availableNameCodesForSpawning = _characterDataProvider.CreateAllPossibleCharacterNameCodes(_characterSelectionCount);
            _characterSelectionCount.Clear();
            foreach (var characterData in _characterDataProvider.CreateAllPossibleCharacterNameCodes(new Dictionary<string, int>()))
            {
                string baseName = LevelUtilities.ParseCharacterNameFromCode(characterData);
                if (!_characterSelectionCount.ContainsKey(baseName) && baseName != " ")
                {
                    _characterSelectionCount.Add(baseName, 0);
                }
            }


            bool ignoreRepeatedCharactersLimit = _availableNameCodesForSpawning.Count < maxCharactersInGame;
            if (ignoreRepeatedCharactersLimit)
            {
                Debug.LogWarning("Caution, there is not sufficient variety of unique character skins to spawn " +
                                 LevelConstants.DEFAULT_LIMIT_OF_SAME_CHARACTER_SPAWNED +
                                 " of each type if needed. Game will run ignoring the limit of same character spawned, or may spawn fewer than maxCharacters if not enough total prefabs.");
            }
            if (playersCharacterNameCodes.Count < currentPlayers)
            {
                Debug.LogError("Not all players have selected a character. Fix it...");
                return false;
            }

            foreach (var playerCharacterNameCode in playersCharacterNameCodes)
            {
                if (!string.IsNullOrEmpty(playerCharacterNameCode))
                {
                    if (!AddCharacterToSelection(playerCharacterNameCode, ignoreRepeatedCharactersLimit, true)
)
                    {
                        Debug.LogError($"Player character {playerCharacterNameCode} could not be selected for spawning.");
                    }
                }
            }


            int totalAICharacters = maxCharactersInGame - currentPlayers;
            for (int aiCharacterCount = 0; aiCharacterCount < totalAICharacters; aiCharacterCount++)
            {
                if (_availableNameCodesForSpawning.Count == 0)
                {
                    Debug.LogWarning("Not enough unique character name codes available to fill all AI slots. Spawning fewer AIs.");
                    break;
                }
                string aiNameCode = (_availableNameCodesForSpawning.GetRandomValueFromList(LevelConstants.ERROR_STRING_MESSAGE_CHARACTER_LIST_EMPTY));
                if (aiNameCode == LevelConstants.ERROR_STRING_MESSAGE_CHARACTER_LIST_EMPTY)
                {
                    Debug.LogWarning("Error, all characters from _availableNameCodesForSpawning were depleted or list was empty. " +
                                     "Make sure character data provides enough variety or adjust limits.");
                    break;
                }

                if (!AddCharacterToSelection(aiNameCode, ignoreRepeatedCharactersLimit, false))
                {

                    aiCharacterCount--;
                    Debug.LogWarning($"Failed to add AI {aiNameCode}, trying another. Remaining available: {_availableNameCodesForSpawning.Count}");
                    if (_availableNameCodesForSpawning.Count == 0) break;
                }
            }
            return InGameCharactersNameCodes.Count > 0;
        }

        private bool AddCharacterToSelection(string nameCode, bool ignoreLimit, bool isPlayer)
        {
            if (!_availableNameCodesForSpawning.Contains(nameCode) && isPlayer)
            {
                Debug.LogWarning($"Player name code {nameCode} not in available list. This might be an issue with PlayerPrefs or CharacterData setup. Attempting to use it anyway.");

            }
            else if (!_availableNameCodesForSpawning.Contains(nameCode) && !isPlayer)
            {
                Debug.LogWarning($"AI name code {nameCode} not in available list. This should not happen if GetRandomValueFromList is used. Skipping.");
                return false;
            }

            string baseName = LevelUtilities.ParseCharacterNameFromCode(nameCode);
            if (baseName == " ")
            {
                Debug.LogError($"Invalid name code format: {nameCode}");
                return false;
            }

            if (!ignoreLimit)
            {
                if (!_characterSelectionCount.ContainsKey(baseName))
                {
                    _characterSelectionCount[baseName] = 0;
                }

                _characterSelectionCount[baseName]++;
                _availableNameCodesForSpawning.Remove(nameCode);

                if (_characterSelectionCount[baseName] >= LevelConstants.DEFAULT_LIMIT_OF_SAME_CHARACTER_SPAWNED)
                {
                    _availableNameCodesForSpawning.RemoveAll(s => LevelUtilities.ParseCharacterNameFromCode(s) == baseName);
                }
            }
            else
            {
                _availableNameCodesForSpawning.Remove(nameCode);
            }

            InGameCharactersNameCodes.Add(nameCode);
            return true;
        }


        public bool TrySpawnCharacters(int maxCharactersInGame, List<string> playersCharacterNameCodes, int currentPlayers, bool spawnOnlyAIs, out GameObject playerReferenceForAI)
        {
            playerReferenceForAI = null;
            InGameCharacters.Clear();
            InGameCharactersNameCodes.Clear();

            if (!PrepareSpawnList(maxCharactersInGame, playersCharacterNameCodes, spawnOnlyAIs ? 0 : currentPlayers))
            {
                Debug.LogError("Failed to prepare character spawn list.");
                return false;
            }

            if (_spawnPoints.Length < InGameCharactersNameCodes.Count)
            {
                Debug.LogError($"Not enough spawn points ({_spawnPoints.Length}) for selected characters ({InGameCharactersNameCodes.Count}).");
                return false;
            }

            _spawnPoints.ShuffleList();

            int characterSpawnIndex = 0;
            GameObject playerCarInstance = null;

            if (!spawnOnlyAIs)
            {
                for (int i = 0; i < currentPlayers && i < InGameCharactersNameCodes.Count; i++)
                {
                    string charNameCodeToSpawn = InGameCharactersNameCodes[i];
                    GameObject characterModelPrefab = _characterDataProvider.GetCharacterPrefab(charNameCodeToSpawn);
                    if (characterModelPrefab == null)
                    {
                        Debug.LogError($"Could not get prefab for player: {charNameCodeToSpawn}");
                        continue;
                    }

                    CarInjector carInjector = Object.Instantiate(_playerPrefab, _spawnPoints[characterSpawnIndex].transform.position, _spawnPoints[characterSpawnIndex].transform.rotation);
                    GameObject instantiatedCar = carInjector.Install(characterModelPrefab);

                    InGameCharacters.Add(instantiatedCar);
                    PlayerReference = instantiatedCar;
                    playerCarInstance = instantiatedCar;
                    playerReferenceForAI = playerCarInstance;

                    var playerInputController = instantiatedCar.GetComponentInParent<PlayerInputController>();
                    if (playerInputController) playerInputController.enabled = false;
                    var baseController = instantiatedCar.GetComponentInParent<Controller>();
                    if (baseController) baseController.enabled = false;

                    characterSpawnIndex++;
                }
            }

            int aiStartIndex = spawnOnlyAIs ? 0 : currentPlayers;
            for (int i = aiStartIndex; i < InGameCharactersNameCodes.Count; i++)
            {
                if (characterSpawnIndex >= _spawnPoints.Length) break;

                string charNameCodeToSpawn = InGameCharactersNameCodes[i];
                GameObject characterModelPrefab = _characterDataProvider.GetCharacterPrefab(charNameCodeToSpawn);
                if (characterModelPrefab == null)
                {
                    Debug.LogError($"Could not get prefab for AI: {charNameCodeToSpawn}");
                    continue;
                }

                CarInjector carInjector = Object.Instantiate(_aiPrefab, _spawnPoints[characterSpawnIndex].transform.position, _spawnPoints[characterSpawnIndex].transform.rotation);
                GameObject instantiatedCar = carInjector.Install(characterModelPrefab);

                InGameCharacters.Add(instantiatedCar);

                EnemyAIBrain enemyAIBrain = carInjector.GetComponent<EnemyAIBrain>();
                if (enemyAIBrain)
                {
                    enemyAIBrain.Player = playerCarInstance;
                }
                else
                {
                    Debug.LogWarning($"AIPrefab for {charNameCodeToSpawn} does not have an EnemyAIBrain component on its root.");
                }

                var baseController = instantiatedCar.GetComponentInParent<Controller>();
                if (baseController) baseController.enabled = false;

                characterSpawnIndex++;
            }
            return InGameCharacters.Count > 0;
        }
    }
}