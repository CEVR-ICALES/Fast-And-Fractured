using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine.UI;
using Utilities;
using UnityEngine.Playables;
using Enums;
namespace FastAndFractured
{
    public class WinLoseScreenBehaviour : MonoBehaviour
    {
        [SerializeField] private GameObject resultText;
        [SerializeField] private GameObject totalDamageDealtText;
        [SerializeField] private GameObject totalDamageTakenText;
        [SerializeField] private GameObject totalDistanceText;
        [SerializeField] private GameObject container;
        private GameObject objectToSpawn;
        public GameObject spawnPoint;
        private PlayableDirector _playableDirector;
        [SerializeField] private GameEndData gameEndData;
        [SerializeField] private GameEndData gameEndDataDefault;
        private GameObject _spawnedObject;
        [SerializeField] private GameObject mainMenuTimeline;


        void OnEnable()
        {
            SetFinalStats();
            objectToSpawn = gameEndData.finalAnimation;
            if (objectToSpawn != null)
            {
                _spawnedObject = Instantiate(objectToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                _playableDirector = _spawnedObject.GetComponentInChildren<PlayableDirector>();
                _playableDirector.stopped += OnPlayableDirectorStopped;
            }else
            {
                ShowMenu();
            }
            ResetGameEndData();
        }
        public void ShowMenu()
        {
            container.SetActive(true);
        }
        private void OnPlayableDirectorStopped(PlayableDirector obj)
        {
            _playableDirector.stopped -= OnPlayableDirectorStopped;
            ShowMenu();
        }
        private void SetFinalStats()
        {
            if (gameEndData.isWin)
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = "Menu.Win";
            }
            else
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = "Menu.Lose";
            }
            
            totalDamageDealtText.GetComponent<TextMeshProUGUI>().text = gameEndData.totalDamageDealt;
            totalDamageTakenText.GetComponent<TextMeshProUGUI>().text = gameEndData.totalDamageTaken;
            totalDistanceText.GetComponent<TextMeshProUGUI>().text = gameEndData.totalDistanceTraveled;
        }
        private void ResetGameEndData()
        {
            gameEndData.isWin = gameEndDataDefault.isWin;
            gameEndData.totalDamageDealt = gameEndDataDefault.totalDamageDealt;
            gameEndData.totalDamageTaken = gameEndDataDefault.totalDamageTaken;
            gameEndData.totalDistanceTraveled = gameEndDataDefault.totalDistanceTraveled;
            gameEndData.finalAnimation = gameEndDataDefault.finalAnimation;
        }
        public void GoToMainMenu()
        {
            _spawnedObject.SetActive(false);
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MAIN_MENU, -1);
            mainMenuTimeline.SetActive(true);
        }
    }
}
