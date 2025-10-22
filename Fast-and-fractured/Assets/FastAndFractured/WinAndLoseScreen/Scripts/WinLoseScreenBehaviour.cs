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
    public class WinLoseScreenBehaviour : AbstractSingleton<WinLoseScreenBehaviour>
    {
        [SerializeField] private GameObject resultText;
        [SerializeField] private GameObject totalDamageDealtText;
        [SerializeField] private GameObject totalDamageTakenText;
        [SerializeField] private GameObject totalDistanceText;
        [SerializeField] private GameObject container;
        private GameObject _objectToSpawn;
        public GameObject spawnPoint;
        public GameObject replayButton;
        public LoadingManager LoadingManager;
        private PlayableDirector _playableDirector;
        [SerializeField] private GameEndData gameEndData;
        [SerializeField] private GameEndData gameEndDataDefault;
        private GameObject _spawnedObject;
        private const string WIN_TEXT = "Menu.Win";
        private const string LOSE_TEXT = "Menu.Lose";


        void OnEnable()
        {
            SetFinalStats();
            _objectToSpawn = gameEndData.finalAnimation;
            if (_objectToSpawn != null)
            {
                _spawnedObject = Instantiate(_objectToSpawn, spawnPoint.transform.position, spawnPoint.transform.rotation);
                _playableDirector = _spawnedObject.GetComponentInChildren<PlayableDirector>();
                _playableDirector.stopped += OnPlayableDirectorStopped;
                replayButton.GetComponent<Button>().onClick.AddListener(() => { LoadingManager.ReplayScene();});
            }
            else
            {
                ShowMenu();
            }
        }
        public void ShowMenu()
        {
            MenuSkipInitialCutscene.Instance.AlreadySkipped = true;
            MenuSkipInitialCutscene.Instance.skipText.SetActive(false);
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.WIN_LOSE, 0.5f);
            ResetGameEndData();
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
                resultText.GetComponent<LocalizedText>().LocalizationKey = WIN_TEXT;
            }
            else
            {
                resultText.GetComponent<LocalizedText>().LocalizationKey = LOSE_TEXT;
            }

            string totalDamageTaken = Mathf.RoundToInt(float.Parse(gameEndData.totalDamageTaken)).ToString();
            totalDamageDealtText.GetComponent<TextMeshProUGUI>().text = gameEndData.totalDamageDealt;
            totalDamageTakenText.GetComponent<TextMeshProUGUI>().text = totalDamageTaken;
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
        }
    }
}
