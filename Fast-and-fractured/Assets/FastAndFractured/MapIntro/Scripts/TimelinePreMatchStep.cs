using System;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class TimelinePreMatchStep : PreMatchStep, IPausable
    {
        [SerializeField] private PlayableDirector director;
        [Tooltip("Any key skips the step, like the map intro.")]
        [SerializeField] private bool skippable = true;

        public bool Skippable => skippable;
        public override float Duration => director != null && director.playableAsset != null ? (float)director.playableAsset.duration : 0f;
        public override float Remaining => _playing ? Mathf.Max(0f, Duration - (float)director.time) : 0f;

        private Action _onFinished;
        private bool _playing, _paused;

        private void Reset()
        {
            director = GetComponent<PlayableDirector>();
            OnValidate();
        }

        // PreMatchSequence starts it: playing on its own would run under the intro.
        private void OnValidate()
        {
            if (director != null) director.playOnAwake = false;
        }

        public override void Play(Action onFinished)
        {
            if (director == null || director.playableAsset == null)
            {
                onFinished?.Invoke();
                return;
            }
            _onFinished = onFinished;
            _playing = true;
            BindCameraTracks();
            director.time = 0;
            director.Play();
            PauseManager.TryRegisterPausable(this);
        }

        // The player's camera only exists after the spawn, so camera tracks cannot be bound in the scene.
        private void BindCameraTracks()
        {
            CinemachineBrain brain = Camera.main != null ? Camera.main.GetComponent<CinemachineBrain>() : null;
            if (brain == null) return;
            foreach (PlayableBinding output in director.playableAsset.outputs)
                if (output.sourceObject is CinemachineTrack && director.GetGenericBinding(output.sourceObject) == null)
                    director.SetGenericBinding(output.sourceObject, brain);
        }

        // Polled: with Hold at the end the director never stops by itself.
        private void Update()
        {
            if (!_playing || _paused) return;
            if (director.state != PlayState.Playing || director.time >= Duration) Finish();
        }

        public override void Skip()
        {
            if (!_playing) return;
            director.Stop();
            Finish();
        }

        private void Finish()
        {
            if (!_playing) return;
            _playing = false;
            PauseManager.TryUnregisterPausable(this);
            Action onFinished = _onFinished;
            _onFinished = null;
            onFinished?.Invoke();
        }

        private void OnDestroy()
        {
            if (_playing) PauseManager.TryUnregisterPausable(this);
        }

        public void OnPause()
        {
            _paused = true;
            director.Pause();
        }

        public void OnResume()
        {
            _paused = false;
            if (_playing) director.Resume();
        }
    }
}
