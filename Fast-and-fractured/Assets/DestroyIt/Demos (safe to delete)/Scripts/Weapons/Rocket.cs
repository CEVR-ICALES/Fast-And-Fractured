using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable CommentTypo
// ReSharper disable IdentifierTypo
// ReSharper disable StringLiteralTypo

namespace DestroyIt
{
    /// <summary>
    /// Attach this script to a rocket.
    /// Handles applying blast damage and force to all objects within range, playing an explosive effect, 
    /// and separating the smoke trail from the rocket on impact, so smoke hangs in the air after the rocket is gone.
    /// </summary>
    public class Rocket : MonoBehaviour
    {
        [Tooltip("The amount of constant force applied to the missile. This directly affects the missile's overall speed.")]
        [Range(1, 100)]
        public int speed = 30;

        [Tooltip("The maximum amount of damage the blast can do. This is separate from, and does not affect, the force of the blast on rigidbodies.")]
        public float blastDamage = 200f;
        
        [Tooltip("The strength (or force) of the blast. Higher numbers push rigidbodies around more.")]
        public float blastForce = 250f; 
        
        [Tooltip("The distance from point of impact where objects are considered to be hit at point blank range. Point Blank radius is checked first, then Near, then Far.")]
        public float pointBlankBlastRadius = 2f;

        [Tooltip("The percentage of blast damage applied to objects hit at point blank distance from the rocket's impact point.")]
        [Range(0f, 1f)]
        public float pointBlankDamagePercent = 1f;

        [Tooltip("The distance from the point of impact where objects are nearby, but not considered point blank. Point Blank radius is checked first, then Near, then Far.")]
        public float nearBlastRadius = 4f;

        [Tooltip("The percentage of blast damage applied to objects hit at a distance near to the rocket's impact point.")]
        [Range(0f, 1f)]
        public float nearDamagePercent = .5f;

        [Tooltip("The distance from the point of impact where objects are far away, but still considered to be in the blast zone. Point Blank radius is checked first, then Near, then Far.")]
        public float farBlastRadius = 8f;
        
        [Tooltip("The percentage of blast damage applied to objects hit within maximum effective distance from the rocket's impact point.")]
        [Range(0f, 1f)]
        public float farDamagePercent = .2f;
        
        [Tooltip("The amount of upward \"push\" explosions have. Higher numbers make debris fly up in the air, but can get unrealistic.")]
        [Range(0f, 3f)]
        public float explosionUpwardPush = 1f; 
        
        [Tooltip("The particle effect to play when this object collides with something.")]
        public GameObject explosionPrefab; 
        
        public ParticleSystem smokeTrailPrefab;
        
        [Tooltip("How long the rocket will fly (in seconds) before running out of fuel.")]
        [Range(0f, 6f)]
        public float flightTime = 2f;
        
        [Tooltip("Remove the rocket from the scene after this many seconds, regardless if it's out of fuel or not.")]
        [Range(0f, 10f)]
        public float maxLifetime = 5f;

        private float checkFrequency = 0.1f; // The time (in seconds) this script checks for updates to flight time and fuel levels.
        private float nextUpdateCheck;
        private bool outOfFuel;
        private float flightTimer;
        private GameObject smokeTrailObj;
        private bool isExploding;
        private bool isInitialized;
        private bool isStarted;
        private float smokeTrailDistance = 0.27f;
        private List<Rigidbody> affectedRigidbodies;
        private Dictionary<ChipAwayDebris, float> affectedChipAwayDebris;
        private Dictionary<Destructible, ExplosiveDamage> affectedDestructibles;

        private void Start()
        {
            isInitialized = true;
        }

        private void OnEnable()
        {
            isStarted = false;
            affectedRigidbodies = new List<Rigidbody>();
            affectedChipAwayDebris = new Dictionary<ChipAwayDebris, float>();
            affectedDestructibles = new Dictionary<Destructible, ExplosiveDamage>();
            nextUpdateCheck = Time.time + checkFrequency;
        }

        private void Update()
        {
            if (!isInitialized) return;

            if (!isStarted)
            {
                EngineStartUp();
                isStarted = true;
            }

            if (Time.time > nextUpdateCheck)
            {
                float remainingFlightTime = Time.time - flightTimer;

                // Check if rocket should be culled
                if (remainingFlightTime > maxLifetime)
                    StartCoroutine(Recover());

                // Check if rocket is out of fuel    
                if (!outOfFuel && remainingFlightTime > flightTime)
                    EngineShutDown();

                // reset the counter
                nextUpdateCheck = Time.time + checkFrequency;
            }
        }

        private void EngineStartUp()
        {
            flightTimer = Time.time;
            isExploding = false;
            outOfFuel = false;

            // set the missile's speed (constant force)
            GetComponent<ConstantForce>().relativeForce = new Vector3(0, 0, speed);

            // create the smoke trail
            smokeTrailObj = ObjectPool.Instance.Spawn(smokeTrailPrefab.gameObject, new Vector3(0, 0, smokeTrailDistance * -1), Quaternion.identity, transform);
        }

        private void EngineShutDown()
        {
            if (GetComponent<ConstantForce>() != null)
                GetComponent<ConstantForce>().relativeForce = Vector3.zero;
            GetComponent<Rigidbody>().useGravity = true;
            Transform exhaust = transform.Find("exhaust");
            if (exhaust != null)
                exhaust.gameObject.SetActive(false);
            outOfFuel = true;

            // turn off point light (no thrust means no light source)
            Transform pointLight = transform.Find("point light");
            if (pointLight != null)
                pointLight.gameObject.SetActive(false);
        }

        private void TurnOffSmokeTrail()
        {
            if (smokeTrailObj == null) return;
            // Unparent smoke trail from rocket, 
            // turn off particle emitters, and queue it up for culling.
            smokeTrailObj.transform.parent = null;
            var emission = smokeTrailObj.GetComponent<ParticleSystem>().emission;
            emission.enabled = false;
            PoolAfter poolAfter = smokeTrailObj.AddComponent<PoolAfter>();
            poolAfter.seconds = 7f;
            poolAfter.removeWhenPooled = true;
        }

        public void OnCollisionEnter(Collision collision)
        {
            // If rocket is already exploding, exit.
            if (!isExploding)
                Explode();
        }

        public void Explode()
        {
            Vector3 currPos = transform.position;
            isExploding = true;
            TurnOffSmokeTrail();
            // Play explosion particle effect.
            ObjectPool.Instance.Spawn(explosionPrefab, currPos, GetComponent<Rigidbody>().rotation);
            
            // POINT BLANK RANGE - Apply force and damage to colliders and rigidbodies
            int pointBlankCounter = Physics.OverlapSphereNonAlloc(currPos, pointBlankBlastRadius, DestructionManager.Instance.overlapColliders);
            ExplosiveDamage pointBlankExplosiveDamage = new ExplosiveDamage()
            {
                Position = currPos, 
                DamageAmount = blastDamage * pointBlankDamagePercent, 
                BlastForce = blastForce, 
                Radius = farBlastRadius, 
                UpwardModifier = explosionUpwardPush
            };
            AddAffectedObjects(pointBlankCounter, pointBlankExplosiveDamage, .75f);
            
            // NEAR RANGE - Apply force and damage to colliders and rigidbodies
            int nearCounter = Physics.OverlapSphereNonAlloc(currPos, nearBlastRadius, DestructionManager.Instance.overlapColliders);
            ExplosiveDamage nearExplosiveDamage = new ExplosiveDamage()
            {
                Position = currPos, 
                DamageAmount = blastDamage * nearDamagePercent, 
                BlastForce = blastForce, 
                Radius = farBlastRadius, 
                UpwardModifier = explosionUpwardPush
            };
            AddAffectedObjects(nearCounter, nearExplosiveDamage, .50f);
            
            // FAR RANGE - Apply force and damage to colliders and rigidbodies
            int farCounter = Physics.OverlapSphereNonAlloc(currPos, farBlastRadius, DestructionManager.Instance.overlapColliders);
            ExplosiveDamage farExplosiveDamage = new ExplosiveDamage()
            {
                Position = currPos, 
                DamageAmount = blastDamage * farDamagePercent, 
                BlastForce = blastForce, 
                Radius = farBlastRadius, 
                UpwardModifier = explosionUpwardPush
            };
            AddAffectedObjects(farCounter, farExplosiveDamage, .25f);

            // Apply blast force to all affected rigidbodies
            foreach (Rigidbody rbody in affectedRigidbodies)
                rbody.AddExplosionForce(blastForce, transform.position, farBlastRadius, explosionUpwardPush); // NOTE: farBlastRadius is used because we need the max radius for rigidbody force.
            
            // Apply blast to ChipAwayDebris
            foreach (KeyValuePair<ChipAwayDebris, float> chipAwayDebris in affectedChipAwayDebris)
            {

                if (Random.Range(1, 100) <= 100 * chipAwayDebris.Value) // Chip off debris pieces a fraction of the time, depending on how close they were to the blast point.
                {
                    chipAwayDebris.Key.BreakOff(blastForce, farBlastRadius, explosionUpwardPush);
                }
            }
            
            // Apply blast to Destructibles
            foreach (KeyValuePair<Destructible, ExplosiveDamage> destructible in affectedDestructibles)
            {
                if (destructible.Value.DamageAmount > 0f)
                    destructible.Key.ApplyDamage(destructible.Value);
            }
            
            StartCoroutine(Recover());
        }

        private void AddAffectedObjects(int colliderCount, ExplosiveDamage explosiveDamage, float chipAwayPercentage)
        {
            for (int i=0; i<colliderCount; i++)
            {
                Collider col = DestructionManager.Instance.overlapColliders[i];
                
                // Ignore terrain colliders
                if (col is TerrainCollider)
                    continue;

                // Ignore self (the rocket)
                if (col == GetComponent<Collider>())
                    continue;

                // Check for Rigidbodies
                Rigidbody rbody = col.attachedRigidbody;
                if (rbody != null && !rbody.isKinematic && !affectedRigidbodies.Contains(rbody))
                    affectedRigidbodies.Add(rbody);

                // Check for Chip-Away Debris
                ChipAwayDebris chipAwayDebris = col.gameObject.GetComponent<ChipAwayDebris>();
                if (chipAwayDebris != null && !affectedChipAwayDebris.ContainsKey(chipAwayDebris))
                    affectedChipAwayDebris.Add(chipAwayDebris, chipAwayPercentage);
                    
                if (chipAwayDebris != null)
                    continue; // Don't process destructible components on chip-away debris.

                // Apply damage to Destructible objects
                // Only do this for the first active and enabled Destructible script found in parent objects
                // Special Note: Destructible scripts are turned off on terrain trees by default (to save resources), so we will make an exception for them and process the collision anyway
                Destructible[] destObjs = col.gameObject.GetComponentsInParent<Destructible>(false);
                foreach (Destructible destObj in destObjs)
                {
                    if (affectedDestructibles.ContainsKey(destObj)) continue;
                    if (!destObj.isActiveAndEnabled && !destObj.isTerrainTree) continue;
                    affectedDestructibles.Add(destObj, explosiveDamage);
                }
            }
        }
        
        private IEnumerator Recover()
        {
            yield return new WaitForFixedUpdate();
            GetComponent<Rigidbody>().velocity = Vector3.zero;
            GetComponent<Rigidbody>().angularVelocity = Vector3.zero;
            GetComponent<Rigidbody>().Sleep();
            GetComponent<Rigidbody>().useGravity = false;
            ObjectPool.Instance.PoolObject(gameObject, true);
            StopAllCoroutines();
        }
    }
}