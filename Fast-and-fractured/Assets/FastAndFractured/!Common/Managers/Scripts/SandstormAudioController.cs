using Utilities.Audio;
using UnityEngine;

namespace FastAndFractured
{
    [DisallowMultipleComponent]
    public sealed class SandstormAudioController : MonoBehaviour
    {
        private const float LISTENER_SEARCH_INTERVAL_SECONDS = 1f;
        private BoxCollider _volume;
        private StatsController _listener;
        private IAudioVoice _wind;
        private bool _warned;
        private float _nextListenerSearch;

        public void Begin(BoxCollider volume)
        {
            _volume = volume;
            if (_warned) return;
            _warned = true;
            GameplayAudio.Play(GameAudioCues.CommonSandstormWarning, transform.position);
        }

        public static bool Contains(BoxCollider volume, Vector3 worldPosition)
        {
            if (volume == null || !volume.enabled) return false;
            Vector3 point = volume.transform.InverseTransformPoint(worldPosition) - volume.center;
            Vector3 half = volume.size * 0.5f;
            return Mathf.Abs(point.x) <= half.x && Mathf.Abs(point.y) <= half.y && Mathf.Abs(point.z) <= half.z;
        }

        private void LateUpdate()
        {
            if (!GameplayAudio.CanPlay) return;
            if (_listener == null && Time.unscaledTime >= _nextListenerSearch)
            {
                _nextListenerSearch = Time.unscaledTime + LISTENER_SEARCH_INTERVAL_SECONDS;
                foreach (var stats in FindObjectsByType<StatsController>(FindObjectsSortMode.None))
                    if (GameplayAudio.IsLocal(stats)) { _listener = stats; break; }
            }
            bool inside = _listener != null && _listener.Endurance > 0 && Contains(_volume, _listener.transform.position);
            if (inside && (_wind == null || !_wind.IsAlive)) _wind = GameplayAudio.StartAttached(GameAudioCues.CommonSandstormLoop, _listener.gameObject);
            else if (!inside && _wind != null) GameplayAudio.Stop(ref _wind);
        }

        private void OnDisable()
        {
            GameplayAudio.Stop(ref _wind);
            _warned = false;
        }
    }
}
