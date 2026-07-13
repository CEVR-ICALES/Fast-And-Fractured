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
        private bool _isFading = false;
        private bool _firstTime = true;
        private float _fadeElapsed = 0f;
        private float _fadeDuration = 0.5f;
        private float _growElapsed = 0f;
        private float _growDuration = 1f;
        private float _startSize = 0f;
        private float _targetSize = 0f;
        private Color _originalColor;

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
            if (_firstTime)
            {
                _originalColor = decalProyector.material.GetColor("_BaseColor");
                decalProyector.material = new Material(decalProyector.material);
                _firstTime = false;
            }
            decalProyector.size = new Vector3(0f, 0f, 800f);
            _isFading = false;
            _fadeElapsed = 0f;
            decalProyector.material.SetColor("_BaseColor", _originalColor);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
            decalProyector.size = new Vector3(0f, 0f, 800f);
            decalProyector.material.SetColor("_BaseColor", _originalColor);
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
            if (_isFading)
            {
                _fadeElapsed += Time.deltaTime;
                float tFade = Mathf.Clamp01(_fadeElapsed / _fadeDuration);
                float alpha = Mathf.Lerp(_originalColor.a, 0f, tFade);
                Color newColor = new Color(_originalColor.r, _originalColor.g, _originalColor.b, alpha);
                decalProyector.material.SetColor("_BaseColor", newColor);
                if (tFade >= 1f)
                {
                    _isFading = false;
                }
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
        public void DecalFadeOut(float fadeDuration)
        {
            _isFading = true;
            _fadeElapsed = 0f;
            _fadeDuration = fadeDuration;
        }
    }
}