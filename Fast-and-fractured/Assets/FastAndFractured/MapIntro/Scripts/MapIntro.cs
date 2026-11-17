using System;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class MapIntro : MonoBehaviour, IPreMatchStage, IPausable
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private MapIntroOverlay overlay;
        [Tooltip("Localization key of the map name shown on the title card.")]
        [SerializeField] private string mapNameKey;
        [Tooltip("Shots placed on the local player's car when the intro starts.")]
        [SerializeField] private MapIntroShot[] playerShots;

        private const string HUD_NAME = "PlayerHUD";
        private const string PAUSE_KEY = "escape";
        private const string PAUSE_BUTTON = "start";
        private const float SKIP_INPUT_GRACE_SECONDS = 0.5f;
        private const float SKIP_FADE_OUT_SECONDS = 0.25f;
        private const float SKIP_FADE_IN_SECONDS = 0.4f;
        private const float HUD_FADE_IN_SECONDS = 0.4f;

        public float Duration => director != null && director.playableAsset != null ? (float)director.playableAsset.duration : 0f;
        public float Remaining => _finished || director == null ? 0f : Mathf.Max(0f, Duration - (float)director.time);

        private Transform _car;
        private Action _onFinished;
        private CanvasGroup _hud;
        private float _hudAlpha;
        private bool _finished, _skipping, _countdownStarted;
        private IDisposable _skipListener;
        private float _startTime;

        public static MapIntro Create(Scene scene, Transform car)
        {
            MapIntro prefab = MapIntroLibrary.PrefabFor(scene.name);
            if (prefab == null || car == null) return null;
            MapIntro intro = Instantiate(prefab);
            if (intro.gameObject.scene != scene) SceneManager.MoveGameObjectToScene(intro.gameObject, scene);
            intro._car = car;
            return intro;
        }

        public void Play(Action onFinished)
        {
            _onFinished = onFinished;
            _startTime = Time.unscaledTime;
            foreach (MapIntroShot shot in playerShots)
                if (shot != null) shot.AttachTo(_car);
            HideHud(_car);
            overlay.SetMapNameKey(mapNameKey);
            overlay.SetSkippable(true);
            BindCameraTracks();
            director.stopped += OnDirectorStopped;
            director.Play();
            _skipListener = InputSystem.onAnyButtonPress.Call(OnAnyButtonPress);
            PauseManager.TryRegisterPausable(this);
        }

        private void BindCameraTracks()
        {
            CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            foreach (PlayableBinding output in director.playableAsset.outputs)
                if (output.sourceObject is CinemachineTrack)
                    director.SetGenericBinding(output.sourceObject, brain);
        }

        public void OnCountdownStarted()
        {
            if (_countdownStarted) return;
            _countdownStarted = true;
            overlay.SetSkippable(false);
            RestoreHud(true);
        }

        private void OnAnyButtonPress(InputControl control)
        {
            if (_countdownStarted) return;
            if (Time.unscaledTime - _startTime < SKIP_INPUT_GRACE_SECONDS) return;
            if (control.name == PAUSE_KEY || control.name == PAUSE_BUTTON) return;
            if (PauseManager.HasInstance && PauseManager.Instance.IsGamePaused) return;
            Skip();
        }

        public void Skip()
        {
            if (_finished || _skipping) return;
            _skipping = true;
            overlay.FadeTo(1f, SKIP_FADE_OUT_SECONDS).OnComplete(() =>
            {
                if (director != null) director.Stop();
                Finish();
            });
        }

        private void OnDirectorStopped(PlayableDirector stoppedDirector) => Finish();

        private void Finish()
        {
            if (_finished) return;
            _finished = true;
            Cleanup();
            OnCountdownStarted();
            Action onFinished = _onFinished;
            _onFinished = null;
            onFinished?.Invoke();
            if (_skipping) overlay.FadeTo(0f, SKIP_FADE_IN_SECONDS).OnComplete(() => Destroy(gameObject));
            else Destroy(gameObject);
        }

        private void Cleanup()
        {
            _skipListener?.Dispose();
            _skipListener = null;
            if (director != null) director.stopped -= OnDirectorStopped;
            PauseManager.TryUnregisterPausable(this);
        }

        private void OnDestroy()
        {
            if (_finished) return;
            Cleanup();
            RestoreHud(false);
        }

        public void OnPause()
        {
            if (director != null) director.Pause();
        }

        public void OnResume()
        {
            if (director != null && !_finished) director.Resume();
        }

        private void HideHud(Transform car)
        {
            Transform hud = FindChild(car.root, HUD_NAME);
            if (hud == null) return;
            _hud = hud.GetComponent<CanvasGroup>();
            if (_hud == null) _hud = hud.gameObject.AddComponent<CanvasGroup>();
            _hud.DOKill();
            _hudAlpha = _hud.alpha;
            _hud.alpha = 0f;
        }

        private void RestoreHud(bool fade)
        {
            if (_hud == null) return;
            if (fade) _hud.DOFade(_hudAlpha, HUD_FADE_IN_SECONDS).SetLink(_hud.gameObject);
            else _hud.alpha = _hudAlpha;
            _hud = null;
        }

        private static Transform FindChild(Transform parent, string childName)
        {
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == childName) return child;
            return null;
        }
    }
}
