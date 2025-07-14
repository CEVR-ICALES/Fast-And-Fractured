using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
// ReSharper disable ArrangeAccessorOwnerBody
// ReSharper disable UnusedMember.Global
// ReSharper disable ForCanBeConvertedToForeach

namespace DestroyIt
{
    /// <summary>Put this script on an object you want to be destructible.</summary>
    [DisallowMultipleComponent]
    public class Destructible : MonoBehaviour
    {
        public float TotalHitPoints
        {
            get { return _totalHitPoints; }
            set
            {
                _totalHitPoints = value;
                damageLevels.CalculateDamageLevels(_totalHitPoints);
            }
        }

        public float CurrentHitPoints
        {
            get { return _currentHitPoints; }
            set { _currentHitPoints = value; }
        }

        [SerializeField]
        [FormerlySerializedAs("totalHitPoints")]
        [HideInInspector] 
        private float _totalHitPoints = 50f;

        [SerializeField]
        [FormerlySerializedAs("currentHitPoints")]
        [HideInInspector] 
        private float _currentHitPoints = 50f;
        
        [HideInInspector] public List<DamageLevel> damageLevels;
	    [HideInInspector] public GameObject destroyedPrefab;
	    [HideInInspector] public GameObject destroyedPrefabParent;
        [HideInInspector] public ParticleSystem fallbackParticle;
        [HideInInspector] public int fallbackParticleMatOption;
        [HideInInspector] public List<DamageEffect> damageEffects;
        [HideInInspector] public float velocityReduction = .5f;
        [HideInInspector] public bool limitDamage = false;
        [HideInInspector] public float minDamage = 10f; // The minimum amount of damage the object can receive per hit.
        [HideInInspector] public float maxDamage = 100f; // The maximum amount of damage the object can receive per hit.
        [HideInInspector] public float minDamageTime = 0f; // The minimum amount of time (in seconds) that must pass before the object can be damaged again.
        [HideInInspector] public float ignoreCollisionsUnder = 2f;
        [HideInInspector] public List<GameObject> unparentOnDestroy;
        [HideInInspector] public bool disableKinematicOnUparentedChildren = true;
        [HideInInspector] public List<MaterialMapping> replaceMaterials;
        [HideInInspector] public List<MaterialMapping> replaceParticleMats;
        [HideInInspector] public bool canBeDestroyed = true;
        [HideInInspector] public bool canBeRepaired = true;
        [HideInInspector] public List<string> debrisToReParentByName;
        [HideInInspector] public bool debrisToReParentIsKinematic;
        [HideInInspector] public List<string> childrenToReParentByName;
        [HideInInspector] public bool isDebrisChipAway;
        [HideInInspector] public float chipAwayDebrisMass = 1f;
        [HideInInspector] public float chipAwayDebrisDrag;
        [HideInInspector] public float chipAwayDebrisAngularDrag = 0.05f;
        [HideInInspector] public bool autoPoolDestroyedPrefab = true;
        [HideInInspector] public bool useFallbackParticle = true;
        [HideInInspector] public Vector3 centerPointOverride;
        [HideInInspector] public Vector3 fallbackParticleScale = Vector3.one;
        [HideInInspector] public Transform fallbackParticleParent;
        [HideInInspector] public bool sinkWhenDestroyed;
        [HideInInspector] public bool shouldDeactivate; // If true, this script will deactivate after a set period of time (configurable on DestructionManager).
        [HideInInspector] public bool isTerrainTree; // Is this Destructible object a stand-in for a terrain tree?
        [HideInInspector] public AudioClip destroyedSound;
        [HideInInspector] public AudioClip damagedSound;
        [HideInInspector] public AudioClip repairedSound;

        // Private variables
        private const float InvulnerableTimer = 0.5f; // How long (in seconds) the destructible object is invulnerable after instantiation.
        private DamageLevel _currentDamageLevel;
        private bool _isInitialized;
        private float _deactivateTimer;
        private bool _firstFixedUpdate = true;
        private Rigidbody _rigidBody; // store a reference to this destructible object's rigidbody, so we don't have to use GetComponent() at runtime.
        private bool _isInvulnerable; // Determines whether the destructible object starts with a short period of invulnerability. Prevents destructible debris being immediately destroyed by the same forces that destroyed the original object.

        // Properties
        public bool UseProgressiveDamage { get; set; } = true; // Used to determine if the shader on the destructible object is
        public bool CheckForClingingDebris { get; set; } = true; // This is an added optimization used when we are auto-pooling destroyed prefabs. It allows us to avoid a GetComponentsInChildren() check for ClingPoints destruction time.
        public Rigidbody[] PooledRigidbodies { get; set; } // This is an added optimization used when we are auto-pooling destroyed prefabs. It allows us to avoid multiple GetComponentsInChildren() checks for Rigidbodies at destruction time.
        public GameObject[] PooledRigidbodyGos { get; set; } // This is an added optimization used when we are auto-pooling destroyed prefabs. It allows us to avoid multiple GetComponentsInChildren() checks for the GameObjects on Rigidibodies at destruction time.
        public float VelocityReduction => Mathf.Abs(velocityReduction - 1f) /* invert the velocity reduction value (so it makes sense in the UI) */;
        public Quaternion RotationFixedUpdate { get; private set; }
        public Vector3 PositionFixedUpdate { get; private set; }
        public Vector3 VelocityFixedUpdate { get; private set; }
        public Vector3 AngularVelocityFixedUpdate { get; private set; }
        public float LastRepairedAmount { get; private set; }
        public float LastDamagedAmount { get; private set; }
        public float LastDamagedTime { get; private set; }
        public bool IsDestroyed => !_isInvulnerable && canBeDestroyed && CurrentHitPoints <= 0;
        public Vector3 MeshCenterPoint { get; private set; }

        // Events
        public event Action DamagedEvent;
        public event Action DestroyedEvent;
        public event Action RepairedEvent;

        public void Start()
        {
            CheckForClingingDebris = true;

            if (damageLevels == null || damageLevels.Count == 0)
                damageLevels = DestructibleHelper.DefaultDamageLevels();
            damageLevels.CalculateDamageLevels(TotalHitPoints);

            // Store a reference to this object's rigidbody, for better performance.
            _rigidBody = GetComponent<Rigidbody>();

            // Only calculate the mesh center point if the destructible object uses a fallback particle.
            if (useFallbackParticle)
            {
                MeshRenderer[] meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
                MeshCenterPoint = gameObject.GetMeshCenterPoint(meshRenderers);
                if (gameObject.IsAnyMeshPartOfStaticBatch(meshRenderers) && centerPointOverride == Vector3.zero)
                    Debug.Log($"[{gameObject.name}] is a Destructible object with one or more static meshes, but no position override for the fallback particle effect. Particle effect may not spawn where you expect.");
            }

            PlayDamageEffects();
            _isInvulnerable = true;
            Invoke(nameof(RemoveInvulnerability), InvulnerableTimer);

            if (gameObject.HasTagInParent(Tag.TerrainTree))
                isTerrainTree = true;

            // If AutoPool is turned on, add the destroyed prefab to the ObjectPool.
            if (autoPoolDestroyedPrefab)
                ObjectPool.Instance.AddDestructibleObjectToPool(this);
            
            _isInitialized = true;
        }

        public void RemoveInvulnerability()
        {
            _isInvulnerable = false;
        }

        public void FixedUpdate()
        {
            if (!_isInitialized) return;
            DestructionManager destructionManager = DestructionManager.Instance;
            if (destructionManager == null) return;

            // Use the fixed update position/rotation for placement of the destroyed prefab.
            PositionFixedUpdate = transform.position;
            RotationFixedUpdate = transform.rotation;
            if (_rigidBody != null)
            {
                VelocityFixedUpdate = _rigidBody.velocity;
                AngularVelocityFixedUpdate = _rigidBody.angularVelocity;
            }

            SetDamageLevel();
            PlayDamageEffects();

            // Check if this script should be auto-deactivated, as configured on the DestructionManager
            if (destructionManager.autoDeactivateDestructibles && !isTerrainTree && shouldDeactivate)
                UpdateDeactivation(destructionManager.deactivateAfter);
            else if (destructionManager.autoDeactivateDestructibleTerrainObjects && isTerrainTree && shouldDeactivate)
                UpdateDeactivation(destructionManager.deactivateAfter);
            
            if (IsDestroyed) 
                destructionManager.ProcessDestruction(this, destroyedPrefab, new ExplosiveDamage());

            // If this is the first fixed update frame and autoDeativateDestructibles is true, start this component deactivated.
            if (_firstFixedUpdate)
                this.SetActiveOrInactive(destructionManager);

            _firstFixedUpdate = false;
        }

        private void UpdateDeactivation(float deactivateAfter)
        {
            if (_deactivateTimer > deactivateAfter)
            {
                _deactivateTimer = 0f;
                shouldDeactivate = false;
                enabled = false;
            }
            else
                _deactivateTimer += Time.fixedDeltaTime;
        }

        /// <summary>Applies a generic amount of damage, with no specific impact or explosive force.</summary>
        public void ApplyDamage(float amount)
        {
            if (IsDestroyed || _isInvulnerable || !DestructionManager.Instance.allowDamage) return; // don't try to apply damage to an already-destroyed or invulnerable object, or if damaging object is not allowed.

            // Adjust the damage based on Min/Max/Time thresholds.
            if (limitDamage)
            {
                if (LastDamagedTime > 0f && minDamageTime > 0f && Time.time < LastDamagedTime + minDamageTime)
                    return;

                if (maxDamage >= 0 && amount > maxDamage)
                    amount = maxDamage;

                if (minDamage >= 0 && minDamage <= maxDamage && amount < minDamage)
                    amount = minDamage;

                if (amount <= 0) return;
            }

            LastDamagedAmount = amount;
            LastDamagedTime = Time.time;
            FireDamagedEvent();

            // Check for any audio clip we may need to play
            if (damagedSound != null)
                AudioSource.PlayClipAtPoint(damagedSound, transform.position);

            CurrentHitPoints -= amount;
            if (CurrentHitPoints > 0) return;
            if (CurrentHitPoints < 0) CurrentHitPoints = 0;

            PlayDamageEffects();

            if (IsDestroyed)
                DestructionManager.Instance.ProcessDestruction(this, destroyedPrefab, new DirectDamage{DamageAmount = amount});
        }

        public void ApplyDamage(Damage damage)
        {
            if (IsDestroyed || _isInvulnerable || !DestructionManager.Instance.allowDamage) return; // don't try to apply damage to an already-destroyed or invulnerable object, or if damaging object is not allowed.

            // Adjust the damage based on Min/Max/Time thresholds.
            if (limitDamage)
            {
                if (LastDamagedTime > 0f && minDamageTime > 0f && Time.time < LastDamagedTime + minDamageTime)
                    return;

                if (maxDamage >= 0 && damage.DamageAmount > maxDamage)
                    damage.DamageAmount = maxDamage;

                if (minDamage >= 0 && minDamage <= maxDamage && damage.DamageAmount < minDamage)
                    damage.DamageAmount = minDamage;

                if (damage.DamageAmount <= 0) return;
            }

            LastDamagedAmount = damage.DamageAmount;
            LastDamagedTime = Time.time;
            FireDamagedEvent();

            // Check for any audio clip we may need to play
            if (damagedSound != null)
                AudioSource.PlayClipAtPoint(damagedSound, transform.position);

            CurrentHitPoints -= damage.DamageAmount;
            if (CurrentHitPoints > 0) return;
            if (CurrentHitPoints < 0) CurrentHitPoints = 0;

            PlayDamageEffects();

            if (IsDestroyed)
                DestructionManager.Instance.ProcessDestruction(this, destroyedPrefab, damage);
        }

        public void RepairDamage(float amount) 
        {
            if (IsDestroyed || !canBeRepaired) return; // object cannot be repaired if it is either already destroyed or not repairable.

            LastRepairedAmount = amount;

            CurrentHitPoints += amount;
            if (CurrentHitPoints > TotalHitPoints) // object cannot be over-repaired beyond its total hit points.
                CurrentHitPoints = TotalHitPoints;

            PlayDamageEffects();
            FireRepairedEvent();

            // Check for any audio clip we may need to play
            if (repairedSound != null)
                AudioSource.PlayClipAtPoint(repairedSound, transform.position);
        }

        public void Destroy()
        {
            if (IsDestroyed || _isInvulnerable) return; // don't try to destroy an already-destroyed or invulnerable object.

            LastDamagedAmount = CurrentHitPoints;
            LastDamagedTime = Time.time;
            FireDamagedEvent();

            CurrentHitPoints = 0;
            PlayDamageEffects();

            DestructionManager.Instance.ProcessDestruction(this, destroyedPrefab, CurrentHitPoints);
        }

        /// <summary>Advances the damage state, applies damage-level materials as needed, and plays particle effects.</summary>
        private void SetDamageLevel()
        {
            DamageLevel damageLevel = damageLevels?.GetDamageLevel(CurrentHitPoints);
            if (damageLevel == null) return;
            if (_currentDamageLevel != null && damageLevel == _currentDamageLevel) return;

            _currentDamageLevel = damageLevel;
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer rend in renderers)
            {
                Destructible parentDestructible = rend.GetComponentInParent<Destructible>(); // child Destructible objects should not be affected by damage on their parents.
                if (parentDestructible != this) continue;
                bool isAcceptableRenderer = rend is MeshRenderer || rend is SkinnedMeshRenderer;
                if (isAcceptableRenderer && !rend.gameObject.HasTag(Tag.ClingingDebris) && rend.gameObject.layer != DestructionManager.Instance.debrisLayer)
                {
                    for (int j = 0; j < rend.sharedMaterials.Length; j++)
                        DestructionManager.Instance.SetProgressiveDamageTexture(rend, rend.sharedMaterials[j], _currentDamageLevel);
                }
            }

            PlayDamageEffects();
        }

        /// <summary>Gets the material to use for the fallback particle effect when this Destructible object is destroyed.</summary>
        public Material GetDestroyedParticleEffectMaterial()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            foreach (Renderer rend in renderers)
            {
                Destructible parentDestructible = rend.GetComponentInParent<Destructible>(); // only get the material for the parent object to use for a particle effect
                if (parentDestructible != this) continue;
                bool isAcceptableRenderer = rend is MeshRenderer || rend is SkinnedMeshRenderer;
                if (isAcceptableRenderer)
                    return rend.sharedMaterial;
            }

            return null; // could not find an acceptable material to use for particle effects
        }

        private void PlayDamageEffects()
        {
            // Check if we should play a particle effect for this damage level
            if (damageEffects == null || damageEffects.Count == 0) return;

            int currentDamageLevelIndex = 0;
            if (_currentDamageLevel != null)
                currentDamageLevelIndex = damageLevels.IndexOf(_currentDamageLevel); // FindIndex(a => a == currentDamageLevel);
            
            foreach (DamageEffect effect in damageEffects)
            {
                if (effect == null || effect.Prefab == null) continue;

                // Get rotation
                Quaternion rotation = transform.rotation;
                if (effect.Rotation != Vector3.zero)
                    rotation = transform.rotation * Quaternion.Euler(effect.Rotation);

                // Is this effect only played if the destructible object has a certain tag?
                if (effect.HasTagDependency && !gameObject.HasTag(effect.TagDependency))
                    continue;

                if (_currentDamageLevel != null && effect.TriggeredAt < damageLevels.Count)
                {
                    // TURN ON pre-destruction damage effects
                    if (currentDamageLevelIndex >= effect.TriggeredAt && !effect.HasStarted)
                    {
                        if (effect.GameObject != null)
                        {
                            for (int i = 0; i < effect.ParticleSystems.Length; i++)
                            {
                                ParticleSystem.EmissionModule emission = effect.ParticleSystems[i].emission;
                                emission.enabled = true;
                            }
                        }
                        else
                        {
                            // set parent to this destructible object and play
                            effect.GameObject = ObjectPool.Instance.Spawn(effect.Prefab, effect.Offset, rotation, transform);

                            if (effect.GameObject != null)
                            {
                                effect.ParticleSystems = effect.GameObject.GetComponentsInChildren<ParticleSystem>();

                                if (effect.Scale != Vector3.one)
                                {
                                    foreach (ParticleSystem ps in effect.ParticleSystems)
                                    {
                                        var main = ps.main;
                                        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                                    }

                                    effect.GameObject.transform.localScale = effect.Scale;
                                }
                            }
                        }

                        effect.HasStarted = true;
                    }

                    // TURN OFF pre-destruction damage effects
                    if (currentDamageLevelIndex < effect.TriggeredAt && effect.HasStarted)
                    {
                        if (effect.GameObject != null)
                        {
                            for (int i = 0; i < effect.ParticleSystems.Length; i++)
                            {
                                ParticleSystem.EmissionModule emission = effect.ParticleSystems[i].emission;
                                emission.enabled = false;
                            }
                        }

                        effect.HasStarted = false;
                    }
                }

                // Destroyed effects
                if (effect.TriggeredAt == damageLevels.Count && IsDestroyed && !effect.HasStarted)
                {
                    effect.GameObject = canBeDestroyed ? 
                        ObjectPool.Instance.Spawn(effect.Prefab, transform.TransformPoint(effect.Offset), rotation) : 
                        ObjectPool.Instance.Spawn(effect.Prefab, effect.Offset, rotation, transform);

                    if (effect.GameObject != null)
                    {
                        effect.ParticleSystems = effect.GameObject.GetComponentsInChildren<ParticleSystem>();

                        if (effect.Scale != Vector3.one)
                        {
                            foreach (ParticleSystem ps in effect.ParticleSystems)
                            {
                                var main = ps.main;
                                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                            }

                            effect.GameObject.transform.localScale = effect.Scale;
                        }
                    }

                    effect.HasStarted = true;
                }
            }
        }

        // NOTE: OnCollisionEnter will only fire if a rigidbody is attached to this object!
        public void OnCollisionEnter(Collision collision)
        {
            if (DestructionManager.Instance == null) return;
            if (!isActiveAndEnabled) return;

            this.ProcessDestructibleCollision(collision, GetComponent<Rigidbody>());
            
            if (collision.contacts.Length <= 0) return;
            
            Destructible destructibleObj = collision.contacts[0].otherCollider.gameObject.GetComponentInParent<Destructible>();
            if (destructibleObj != null && collision.contacts[0].otherCollider.attachedRigidbody == null)
                destructibleObj.ProcessDestructibleCollision(collision, GetComponent<Rigidbody>());
        }

        // NOTE: OnDrawGizmos will only fire if Gizmos are turned on in the Unity Editor!
        public void OnDrawGizmos()
        {
            damageEffects.DrawGizmos(transform);
            centerPointOverride.DrawGizmos(transform);
        }

        public void FireDestroyedEvent()
        {
            DestroyedEvent?.Invoke(); // If there is at least one listener, trigger the event.
        }

        public void FireRepairedEvent()
        {
            RepairedEvent?.Invoke(); // If there is at least one listener, trigger the event.
        }

        public void FireDamagedEvent()
        {
            DamagedEvent?.Invoke(); // If there is at least one listener, trigger the event.
        }
    }
}