using UnityEngine;

namespace DestroyIt
{
    public class AutoDamage : MonoBehaviour
    {
        public int startAtHitPoints = 30;
        public float damageIntervalSeconds = 0.5f;
        public int damagePerInterval = 5;

        private bool _isInitialized;
        private Destructible _destructible;
        private bool _autoDamageStarted;

        void Start()
        {
            _destructible = gameObject.GetComponent<Destructible>();
            if (_destructible == null)
            {
                Debug.LogWarning("No Destructible object found! AutoDamage removed.");
                Destroy(this);
            }
            _isInitialized = true;
        }

        void Update()
        {
            if (!_isInitialized) return;
            if (_destructible == null) return;
            if (_autoDamageStarted) return;

            if (_destructible.CurrentHitPoints <= startAtHitPoints)
            {
                InvokeRepeating("ApplyDamage", 0f, damageIntervalSeconds);
                _autoDamageStarted = true;
            }
        }

        void ApplyDamage()
        {
            if (_destructible == null) return;

            _destructible.ApplyDamage(damagePerInterval);
        }
    }
}
