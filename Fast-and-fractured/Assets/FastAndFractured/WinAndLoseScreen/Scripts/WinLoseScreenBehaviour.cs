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
        [SerializeField] private GameObject killCountText;
        [SerializeField] private GameObject container;
        private GameObject _objectToSpawn;
        public GameObject spawnPoint;
        public GameObject replayButton;
        public LoadingManager LoadingManager;
        private PlayableDirector _playableDirector;
        public PlayableDirector SpawnedDirector => _playableDirector;
        [SerializeField] public GameEndData gameEndData;
        [SerializeField] private GameEndData gameEndDataDefault;
        private GameObject _spawnedObject;
        private bool _menuShown;
        private const string WIN_TEXT = "Menu.Win";
        private const string GROUND_LAYER = "Ground";
        private const float GROUND_PROBE_HEIGHT = 3f;
        private const string LOSE_TEXT = "Menu.Lose";


        void OnEnable()
        {
            _menuShown = false;
            SetFinalStats();
            _objectToSpawn = gameEndData.finalAnimation;
            if (_objectToSpawn != null)
            {
                _spawnedObject = Instantiate(_objectToSpawn, CutscenePosition, spawnPoint.transform.rotation);
                if (_spawnedObject.TryGetComponent(out FinalAnimationSkins skins))
                    skins.Apply(gameEndData.finalAnimationSkin);
                _playableDirector = _spawnedObject.GetComponentInChildren<PlayableDirector>();
                _playableDirector.stopped += OnPlayableDirectorStopped;
                replayButton.GetComponent<Button>().onClick.AddListener(() => { LoadingManager.ReplayScene();});
            }
            else
            {
                ShowMenu();
            }
        }
        // The cutscenes have their feet at the root, and the spawn point sits 30 cm above the terrain: stand them on it.
        public Vector3 CutscenePosition
        {
            get
            {
                Vector3 spawn = spawnPoint.transform.position;
                return Physics.Raycast(spawn + Vector3.up * GROUND_PROBE_HEIGHT, Vector3.down, out RaycastHit hit,
                    GROUND_PROBE_HEIGHT * 2f, LayerMask.GetMask(GROUND_LAYER), QueryTriggerInteraction.Ignore)
                    ? hit.point : spawn;
            }
        }

        public void ShowMenu()
        {
            if (_menuShown) return;
            _menuShown = true;
            MenuSkipInitialCutscene.Instance.AlreadySkipped = true;
            MenuSkipInitialCutscene.Instance.skipText.SetActive(false);
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.WIN_LOSE, 0.5f);
            ResetGameEndData();
        }
        private void OnDisable()
        {
            if (_playableDirector != null)
                _playableDirector.stopped -= OnPlayableDirectorStopped;
        }

        // The cutscenes hold their last frame (the characters stay where they end), so the director never stops.
        private void Update()
        {
            if (_menuShown || _playableDirector == null || !gameObject.scene.isLoaded) return;
            if (_playableDirector.time >= _playableDirector.duration) ShowMenu();
        }

        private void OnPlayableDirectorStopped(PlayableDirector obj)
        {
            _playableDirector.stopped -= OnPlayableDirectorStopped;
            if (!gameObject.scene.isLoaded) return;
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
            killCountText.GetComponent<TextMeshProUGUI>().text = gameEndData.killCount.ToString();
        }
        private void ResetGameEndData()
        {
            gameEndData.isWin = gameEndDataDefault.isWin;
            gameEndData.totalDamageDealt = gameEndDataDefault.totalDamageDealt;
            gameEndData.totalDamageTaken = gameEndDataDefault.totalDamageTaken;
            gameEndData.totalDistanceTraveled = gameEndDataDefault.totalDistanceTraveled;
            gameEndData.finalAnimation = gameEndDataDefault.finalAnimation;
            gameEndData.finalAnimationSkin = gameEndDataDefault.finalAnimationSkin;
            gameEndData.sceneBuildIndex = gameEndDataDefault.sceneBuildIndex;
            gameEndData.killCount = gameEndDataDefault.killCount;
        }
        public void GoToMainMenu()
        {
            _spawnedObject.SetActive(false);
            MainMenuManager.Instance.TransitionBetweenScreens(ScreensType.MAIN_MENU, -1);
        }
    }
}
