using UnityEngine;
using UnityEngine.SceneManagement;  
using Utilities;  

namespace FastAndFractured
{
    public class GameEndHandler
    {
        private readonly GameEndData _gameEndDataSO;

        public GameEndHandler(GameEndData gameEndDataSO)
        {
            _gameEndDataSO = gameEndDataSO;
        }

        public void ProcessGameEnd(bool hasPlayerWon, GameObject playerReference, string selectedPlayerKeyInPrefs)
        {
            if (playerReference == null)
            {
                Debug.LogError("Player reference is null, cannot process game end stats.");
                if (MainMenuManager.Instance != null) MainMenuManager.Instance.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX); else SceneManager.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX);
                return;
            }

            StatsController playerStats = playerReference.GetComponent<StatsController>();
            if (playerStats == null)
            {
                Debug.LogError("Player reference does not have a StatsController component.");
                 if (MainMenuManager.Instance != null) MainMenuManager.Instance.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX); else SceneManager.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX);
                return;
            }

            float distance = playerStats.totalDistanceDriven;
            string totalDistanceText;
            if (distance < LevelConstants.DISTANCE_TO_CHANGE_TO_KM)
            {
                distance = Mathf.Round(distance);
                totalDistanceText = distance.ToString() + LevelConstants.METERS_TEXT;
            }
            else
            {
                distance = Mathf.Round(distance / LevelConstants.DISTANCE_TO_CHANGE_TO_KM * 10) / 10; 
                totalDistanceText = distance.ToString() + LevelConstants.KILOMETERS_TEXT;
            }

            GameObject finalAnimationPrefab = null;
            string playerCharacterNameCode = PlayerPrefs.GetString(selectedPlayerKeyInPrefs, "Pepe_0");  

            if (hasPlayerWon)
            {
                finalAnimationPrefab = playerStats.GetWinObjectByString(playerCharacterNameCode);
            }
            else
            {
                finalAnimationPrefab = playerStats.GetLoseObjectByString(playerCharacterNameCode);
            }

            if (_gameEndDataSO != null)
            {
                _gameEndDataSO.isWin = hasPlayerWon;
                _gameEndDataSO.totalDamageDealt = playerStats.totalDamageDealt.ToString();
                _gameEndDataSO.totalDamageTaken = playerStats.totalDamageTaken.ToString();
                _gameEndDataSO.totalDistanceTraveled = totalDistanceText;
                _gameEndDataSO.finalAnimation = finalAnimationPrefab; 
            }
            else
            {
                Debug.LogError("GameEndData ScriptableObject is not assigned.");
            }

             if (MainMenuManager.Instance != null)
            {
                MainMenuManager.Instance.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX);  
            }
            else
            {
                Debug.LogWarning("MainMenuManager.Instance is null. Attempting direct scene load to index 0.");
                SceneManager.LoadScene(LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX); 
            }
        }
    }
}