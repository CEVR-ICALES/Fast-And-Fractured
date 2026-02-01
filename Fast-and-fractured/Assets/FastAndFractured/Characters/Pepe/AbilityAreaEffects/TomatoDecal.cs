using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class TomatoDecal : MonoBehaviour, IPausable
    {
        public DecalProjector decal;
        public float maxDuration = 0f;
        public float currentDuration = 0f;
        public float maxRadius = 0f;
        private bool _isPaused = false;

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
            decal.size = new Vector3(0f, 0f, decal.size.z);
        }
        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
            currentDuration = 0f;
        }
        void Update()
        {
            if (_isPaused)
                return;
            if(maxDuration==0f||maxRadius==0f)
            {
                return;
            }
            currentDuration += Time.deltaTime;
            float t = Mathf.Clamp01(currentDuration / maxDuration);

            float diameter = maxRadius * 2f;

            float currentSize = Mathf.Lerp(0f, diameter, t);

            decal.size = new Vector3(currentSize, currentSize, decal.size.z);
        }
        void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
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