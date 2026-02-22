using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class TomatoDecal : MonoBehaviour, IPausable
    {
        public DecalProjector decalProyector;

        private bool _isPaused = false;
        private bool _isGrowing = false;
        private float _growElapsed = 0f;
        private float _growDuration = 1f;
        private float _startSize = 0f;
        private float _targetSize = 0f;

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
            decalProyector.size = new Vector3(0f, 0f, 800f);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
            decalProyector.size = new Vector3(0f, 0f, 800f);
        }

        void Update()
        {
            if (_isPaused) return;

            if (_isGrowing)
            {
                _growElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(_growElapsed / _growDuration);
                float size = Mathf.Lerp(_startSize, _targetSize, t);
                decalProyector.size = new Vector3(size, size, 800f);

                if (t >= 1f)
                    _isGrowing = false;
            }
        }
        public void SetRadius(float newRadius)
        {
            _startSize = decalProyector.size.x;
            _targetSize = newRadius * 2f;
            _growElapsed = 0f;
            _isGrowing = true;
        }

        public void OnPause()
        {
            _isPaused = true;
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }
}