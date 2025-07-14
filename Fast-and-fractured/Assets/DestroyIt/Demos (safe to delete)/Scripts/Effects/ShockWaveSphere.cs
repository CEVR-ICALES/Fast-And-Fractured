using UnityEngine;

namespace DestroyIt
{
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(Rigidbody))]
    public class ShockWaveSphere : MonoBehaviour
    {
        [Tooltip("How many seconds to wait before the shockwave starts.")]
        public float timer = 10f;

        [Tooltip("The particle effect to use for the shockwave.")]
        public ParticleSystem visualEffect;

        [Tooltip("How much force to apply to rigidbodies hit by the shockwave.")]
        public float blastForce = 500f;

        [Tooltip("How much damage to apply to Destructible objects that are hit by the shockwave.")]
        public float damageAmount = 200f;

        [Tooltip("How fast the shockwave spreads out.")]
        public float shockwaveSpeed = 30f;

        [Tooltip("The maximum radius of the shockwave. Once it reaches this size, it is disabled and removed from the scene.")]
        public float maxRadius = 300f;
        
        private Vector3 _originPos; // the world position where the shockwave originates 
        private float _timeLeft;
        private bool _isInitialized;
        private bool _hasStarted;
        private SphereCollider _sphereCollider;
        private Rigidbody _rigidbody;

        public void Start()
        {
            Initialize();
        }

        public void OnEnable()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            _originPos = transform.position;
            _timeLeft = timer;
            _sphereCollider = gameObject.GetComponent<SphereCollider>();
            _sphereCollider.isTrigger = true;
            _sphereCollider.radius = 0f;
            _rigidbody = gameObject.GetComponent<Rigidbody>();
            _rigidbody.useGravity = false;

            _isInitialized = true;
        }

        public void Update()
        {
            if (!_isInitialized) return;

            if (_hasStarted)
            {
                // Check if the shockwave has reached its max size. If so, destroy it.
                if (_sphereCollider.radius >= maxRadius)
                {
                    gameObject.SetActive(false);
                    Destroy(gameObject);
                    return;
                }

                // Grow the shockwave based on the shockwave speed
                _sphereCollider.radius += Time.deltaTime * shockwaveSpeed;

                return;
            }

            _timeLeft -= Time.deltaTime;
            
            if (_timeLeft <= 0)
            {
                // Start the shockwave
                if (visualEffect != null)
                    Instantiate(visualEffect, _originPos, Quaternion.identity);

                _hasStarted = true;
            }
        }

        public void OnTriggerEnter(Collider col)
        {
            // If it has a rigidbody, apply force
            Rigidbody rbody = col.attachedRigidbody;
            if (rbody != null && !rbody.isKinematic)
                rbody.AddExplosionForce(blastForce, _originPos, 0f, 0.5f);

            // Check for Chip-Away Debris
            ChipAwayDebris chipAwayDebris = col.gameObject.GetComponent<ChipAwayDebris>();
            if (chipAwayDebris != null)
            {
                if (Random.Range(1, 100) > 50) // Do this about half the time...
                {
                    chipAwayDebris.BreakOff(blastForce, 0f, 0.5f);
                    return; //Skip the destructible check if the debris hasn't chipped away yet.
                }

                return;
            }

            // If it's a destructible object, apply damage
            // Only do this for the first active and enabled Destructible script found in parent objects
            // Special Note: Destructible scripts are turned off on terrain trees by default (to save resources), so we will make an exception for them and process the damage anyway
            Destructible[] destObjs = col.gameObject.GetComponentsInParent<Destructible>(false);
            foreach (Destructible destObj in destObjs)
            {
                if (!destObj.isActiveAndEnabled && !destObj.isTerrainTree) continue;
                ExplosiveDamage explosiveDamage = new ExplosiveDamage { DamageAmount = damageAmount, BlastForce = blastForce, Position = _originPos, Radius = 0f, UpwardModifier = 0.5f };
                destObj.ApplyDamage(explosiveDamage);
                break;
            }
        }
    }
}