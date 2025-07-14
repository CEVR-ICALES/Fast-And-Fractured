using UnityEngine;

namespace DestroyIt
{
    public class Bullet : MonoBehaviour
    {
        [Tooltip("The bullet's speed in game units per second.")]
        public float speed = 400f;
        [Tooltip("How many seconds the bullet will live, regardless of distance traveled.")]
        public float timeToLive = 0.5f;

        public Renderer streak;
        [Range(1,10)]
        [Tooltip("How often the bullet streak is visibile. 1 = 10% of the time. 10 = 100% of the time.")]
        public int streakVisibleFreq = 6;
        [Range(1,50)]
        [Tooltip("Once turned on or off, the bullet streak will remain stable (unchanged) for this many frames.")]
        public int streakMinFramesStable = 3;

        /// <summary>The position where the bullet started.</summary>
        public Vector3 StartingPosition { get; set; }

        /// <summary>How far in game units the bullet has traveled after being fired.</summary>
        public float DistanceTraveled
        {
            get { return Vector3.Distance(StartingPosition, transform.position); }
        }

        private float spawnTime = 0f;
        private bool hitSomething = false;
        private bool isInitialized = false;
        private int streakFramesStable = 0;

        public void OnEnable()
        {
            spawnTime = Time.time;
            hitSomething = false;
            StartingPosition = transform.position;

            if (streak != null)
                streak.gameObject.SetActive(Random.Range(1, 11) <= streakVisibleFreq);

            isInitialized = true;
        }

        public void Update()
        {
            if (!isInitialized) return;

            // Check if the bullet needs to be destroyed.
            if (Time.time > spawnTime + timeToLive || hitSomething)
            {
                ObjectPool.Instance.PoolObject(gameObject);
                return;
            }

            if (streak != null)
            {
                if (streakFramesStable > streakMinFramesStable)
                {
                    streak.gameObject.SetActive(Random.Range(1, 11) <= streakVisibleFreq);
                    streakFramesStable = 0;
                }
                else
                    streakFramesStable += 1;
            }

            Vector3 lineEndPoint = transform.position + (transform.forward * speed * Time.deltaTime);
            Debug.DrawLine(transform.position, lineEndPoint, Color.red, 5f);

            // Raycast in front of the bullet to see if it hit anything. Sort the hits from closest to farthest.
            RaycastHit[] hits = Physics.RaycastAll(transform.position, transform.forward, speed * Time.deltaTime);
            int hitIndex = -1; // index of the closest hit that is not a trigger collider
            float closestHitDistance = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider.isTrigger) continue;
                if (hits[i].distance < closestHitDistance)
                {
                    hitIndex = i;
                    closestHitDistance = hits[i].distance;
                }
            }

            if (hitIndex > -1)
            {
                ProcessBulletHit(hits[hitIndex], transform.forward);
                hitSomething = true;
                return;
            }

            // Move the bullet forward.
            transform.position += transform.forward * speed * Time.deltaTime;
        }

        private static void ProcessBulletHit(RaycastHit hitInfo, Vector3 bulletDirection)
        {
            HitEffects hitEffects = hitInfo.collider.gameObject.GetComponentInParent<HitEffects>();
            if (hitEffects != null && hitEffects.effects.Count > 0)
                hitEffects.PlayEffect(HitBy.Bullet, hitInfo.point, hitInfo.normal);

            // Apply damage if object hit was Destructible
            // Only do this for the first active and enabled Destructible script found in parent objects
            // Special Note: Destructible scripts are turned off on terrain trees by default (to save resources), so we will make an exception for them and process the hit anyway
            Destructible[] destObjs = hitInfo.collider.gameObject.GetComponentsInParent<Destructible>(false);
            foreach (Destructible destObj in destObjs)
            {
                if (!destObj.isActiveAndEnabled && !destObj.isTerrainTree) continue;
                ImpactDamage bulletDamage = new ImpactDamage { DamageAmount = InputManager.Instance.bulletDamage, AdditionalForce = InputManager.Instance.bulletForcePerSecond, AdditionalForcePosition = hitInfo.point, AdditionalForceRadius = .5f };
                destObj.ApplyDamage(bulletDamage);
                break;
            }

            Vector3 force = bulletDirection * (InputManager.Instance.bulletForcePerSecond / InputManager.Instance.bulletForceFrequency);

            // Apply impact force to rigidbody hit
            Rigidbody rbody = hitInfo.collider.attachedRigidbody;
            if (rbody != null)
                rbody.AddForceAtPosition(force, hitInfo.point, ForceMode.Impulse);

            // Check for Chip-Away Debris
            ChipAwayDebris chipAwayDebris = hitInfo.collider.gameObject.GetComponent<ChipAwayDebris>();
            if (chipAwayDebris != null)
                chipAwayDebris.BreakOff(-1.5f * force, hitInfo.point);
        }
    }
}