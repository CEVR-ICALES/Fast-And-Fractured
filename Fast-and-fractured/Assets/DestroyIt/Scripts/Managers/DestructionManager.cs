using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Destruction Manager (Singleton) - manages all destructible objects.
    /// Put this script on an empty game object in your scene.
    /// </summary>
    [DisallowMultipleComponent]
    public class DestructionManager : MonoBehaviour
    {
        [Tooltip("If true, Destructible scripts will be deactivated on start, and will activate any time they are inside a trigger collider with the ActivateDestructibles script on it.")]
        [HideInInspector] public bool autoDeactivateDestructibles;
        [Tooltip("If true, Destructible terrain object scripts will be deactivated on start, and will activate any time they are inside a trigger collider with the ActivateDestructibles script on it.")]
        [HideInInspector] public bool autoDeactivateDestructibleTerrainObjects = true;
        [Tooltip("If true, Destructible terrain tree scripts will not be activated by ActivateDestructibles scripts. Recommended to leave this true for performance, unless you need to move trees during the game or use progressive damage textures on them.")]
        [HideInInspector] public bool destructibleTreesStayDeactivated = true;
        [Tooltip("The time in seconds to automatically deactivate Destructible scripts when they are outside an ActivateDestructibles trigger area.")]
        [HideInInspector] public float deactivateAfter = 2f;

        [Tooltip("If true, Destructible objects can be damaged and destroyed. Turn this off if you want to globally deactivate Destructible objects taking damage.")]
        public bool allowDamage = true;
        [Tooltip("Maximum allowed persistent debris pieces in the scene.")]
        public int maxPersistentDebris = 400;
        [Tooltip("Maximum allowed destroyed prefabs within [withinSeconds] seconds. When this limit is reached, a particle effect will be used instead.")]
        public int destroyedPrefabLimit = 15;
        [Tooltip("Number of seconds within which no more than [destroyedPrefabLimit] destructions will be instantiated.")]
        public int withinSeconds = 4;
        [Tooltip("The default particle effect to use when an object is destroyed.")]
        public ParticleSystem defaultParticle;
        [Tooltip("If true, persistent debris is allowed to be culled even if the camera is currently rendering it.")]
        public bool removeVisibleDebris = true;
        [Tooltip("The time (in seconds) this script processes updates.")]
        public float updateFrequency = .5f;

        [HideInInspector]
        public bool useCameraDistanceLimit = true;  // If true, things beyond the specified distance from the main camera will be destroyed in a more limiting (ie, higher performance) way.
        [HideInInspector]
        public int cameraDistanceLimit = 100;       // Specified game units (usually meters) from camera, where destruction limiting will occur.
        [HideInInspector]
        public int debrisLayer = -1;
        [HideInInspector]
        public Collider[] overlapColliders; // These are the colliders overlapped by an Overlap Sphere (used for determining affected objects in a blast radius without allocating GC).

        // Private Variables
        private float _nextUpdate;
        private List<Destructible> _destroyedObjects;
        private List<Debris> _debrisPieces;
        private List<Texture2D> _detailMasks;

        // Events
        public event Action DestroyedPrefabCounterChangedEvent;
        public event Action ActiveDebrisCounterChangedEvent;

        // Properties
        public List<float> DestroyedPrefabCounter { get; private set; }

        public bool IsDestroyedPrefabLimitReached => DestroyedPrefabCounter.Count >= destroyedPrefabLimit;

        public int ActiveDebrisCount
        {
            get
            {
                int count = 0;
                foreach (Debris debris in _debrisPieces)
                {
                    if (debris.IsActive)
                        count ++;
                }
                return count;
            }
        }

        // Hide the default constructor (use DestructionManager.Instance instead).
        private DestructionManager() { }

        // Private reference only this class can access
        private static DestructionManager _instance;

        // This is the public reference that other classes will use
        public static DestructionManager Instance
        {
            get
            {
                // If _instance hasn't been set yet, we grab it from the scene.
                // This will only happen the first time this reference is used.
                if (_instance == null)
                    _instance = FindObjectOfType<DestructionManager>();
                return _instance;
            }
        }

        private void Awake()
        {
            // Initialize variables
            DestroyedPrefabCounter = new List<float>();
            overlapColliders = new Collider[100];
            _detailMasks = Resources.LoadAll<Texture2D>("ProgressiveDamage").ToList();
            debrisLayer = LayerMask.NameToLayer("DestroyItDebris");
            _debrisPieces = new List<Debris>();
            _destroyedObjects = new List<Destructible>();
            _nextUpdate = Time.time + updateFrequency;

            // If the default particle hasn't been assigned, try to get it from the Resources folder.
            if (defaultParticle == null)
                defaultParticle = Resources.Load<ParticleSystem>("Default_Particles/DefaultLargeParticle");

            // Checks
            Check.IsDefaultParticleAssigned();
            if (Check.LayerExists("DestroyItDebris", false) == false)
                Debug.LogWarning("DestroyItDebris layer not found. Add a layer named 'DestroyItDebris' to your project if you want debris to ignore other debris when using Cling Points.");
        }

        private void Update()
        {
            if (Time.time < _nextUpdate) return;
            
            // Manage Destroyed Prefab counter
            DestroyedPrefabCounter.Update(withinSeconds);

            // Manage Debris Queue
            if (_debrisPieces.Count > 0)
            {
                // Cleanup references to debris no longer in the game
                int itemsRemoved = _debrisPieces.RemoveAll(x => x == null || !x.IsActive);
                if (itemsRemoved > 0)
                    FireActiveDebrisCounterChangedEvent();
                //TODO: Debris is getting removed from the list, but not destroyed from the game. Debris parent objects should probably check their children periodically for enabled meshes.

                // Disable debris until the Max Debris limit is satisfied.
                if (ActiveDebrisCount > maxPersistentDebris)
                {
                    int overBy = ActiveDebrisCount - maxPersistentDebris;
                        
                    foreach (Debris debris in _debrisPieces)
                    {
                        if (overBy <= 0) break;
                        if (!debris.IsActive) continue;
                        if (!removeVisibleDebris)
                        {
                            if (debris.Rigidbody.GetComponent<Renderer>() == null) continue;
                            if (debris.Rigidbody.GetComponent<Renderer>().isVisible) continue;
                        }
                        // Disable the debris.
                        debris.Disable();
                        overBy -= 1;
                    }
                }
            }

            // Manage Destroyed Objects list (ie, we're spacing out the Destroy() calls for performance)
            if (_destroyedObjects.Count > 0)
            {
                // Destroy a maximum of 5 gameobjects per update, to space it out a little.
                int nbrObjects = _destroyedObjects.Count > 5 ? 5 : _destroyedObjects.Count;
                for (int i=0; i<nbrObjects; i++)
                {
                    // Destroy the gameobject and remove it from the list.
                    if (_destroyedObjects[i] != null && _destroyedObjects[i].gameObject != null)
                        Destroy(_destroyedObjects[i].gameObject);
                }
                _destroyedObjects.RemoveRange(0, nbrObjects);
            }

            _nextUpdate = Time.time + updateFrequency; // reset the next update time.
        }

        /// <summary>Swaps the current destructible object with a new one and applies the correct materials to the new object.</summary>
        public void ProcessDestruction<T>(Destructible oldObj, GameObject destroyedPrefab, T damageInfo)
        {
            if (oldObj == null) return;
            if (!oldObj.canBeDestroyed) return;

            oldObj.FireDestroyedEvent();

            // Check for any audio clips we may need to play
            if (oldObj.destroyedSound != null)
                AudioSource.PlayClipAtPoint(oldObj.destroyedSound, oldObj.transform.position);

            // Look for any debris objects clinging to the old object and un-parent them before destroying the old object.
            oldObj.ReleaseClingingDebris();

            // Remove any Joints from the destroyed object
            //TODO: Add option for transerring joints to the destroyed prefab
            Joint[] joints = oldObj.GetComponentsInChildren<Joint>();
            foreach (Joint jnt in joints)
                Destroy(jnt);

            // Unparent DamageEffects and turn off all particle emissions
            if (oldObj.damageEffects != null)
            {
                foreach (var damageEffect in oldObj.damageEffects)
                {
                    if (!damageEffect.UnparentOnDestroy || damageEffect.GameObject == null) continue;
                    damageEffect.GameObject.transform.SetParent(null, true);
                    if (!damageEffect.StopEmittingOnDestroy || damageEffect.ParticleSystems == null || damageEffect.ParticleSystems.Length <= 0) continue;
                    foreach (var particle in damageEffect.ParticleSystems)
                    {
                        var emission = particle.emission;
                        emission.enabled = false;
                    }
                }
            }

            // Is this destructible object a stand-in for a Terrain Tree?
            if (oldObj.gameObject.HasTagInParent(Tag.TerrainTree))
                TreeManager.Instance.DestroyTreeAt(oldObj.transform.position);

            // Should this object sink into the ground instead of being destroyed?
            if (oldObj.sinkWhenDestroyed)
            {
                DestructibleHelper.SinkAndDestroy(oldObj);
                return; // Exit immediately, don't do any more destruction processing.
            }

            // Play a particle effect and exit.
            if (destroyedPrefab == null || IsDestroyedPrefabLimitReached)
            {
                DestroyWithParticleEffect(oldObj, oldObj.fallbackParticle, damageInfo);
                return;
            }
            
            if (useCameraDistanceLimit)
            {
                // Find the distance between the camera and the destroyed object
                float distance = Vector3.Distance(oldObj.transform.position, Camera.main.transform.position);
                if (distance > cameraDistanceLimit)
                {
                    DestroyWithParticleEffect(oldObj, oldObj.fallbackParticle, damageInfo);
                    return;
                }
            }

            // If we've passed the checks above, we are creating debris.
            DestroyedPrefabCounter.Add(Time.time);
            FireDestroyedPrefabCounterChangedEvent();

            // Unparent any specified child objects before destroying
            UnparentSpecifiedChildren(oldObj);

            // Put the destroyed object in the Debris layer to keep new debris from clinging to it
            if (debrisLayer != -1)
                oldObj.gameObject.layer = debrisLayer;

            // Try to get the destroyed prefab from the object pool
            GameObject newObj = ObjectPool.Instance.Spawn(destroyedPrefab, oldObj.PositionFixedUpdate, oldObj.RotationFixedUpdate, oldObj.GetInstanceID());
            InstantiateDebris(newObj, oldObj, damageInfo);

            oldObj.gameObject.SetActive(false);
            _destroyedObjects.Add(oldObj);
        }

        private void DestroyWithParticleEffect<T>(Destructible oldObj, ParticleSystem customParticle, T damageInfo)
        {
            if (oldObj.useFallbackParticle)
            {
                // Use the DestructibleGroup instance ID if it exists, otherwise use the Destructible object's parent's instance ID.
                GameObject parentObj = oldObj.gameObject.GetHighestParentWithTag(Tag.DestructibleGroup) ?? oldObj.gameObject;
                int instanceId = parentObj.GetInstanceID();

                // Use the mesh center point as the starting position for the particle effect.
                var position = oldObj.MeshCenterPoint;
                
                // If a particle spawn point has been specified, use that instead.
                if (oldObj.centerPointOverride != Vector3.zero)
                    position = oldObj.centerPointOverride;
                
                // Convert the particle spawn point position to world coordinates.
                position = oldObj.transform.TransformPoint(position);

                // If no specific fallback particle effect is defined, use the default particle effect assigned in DestructionManager.
                ParticleManager.Instance.PlayEffect(customParticle ?? defaultParticle, oldObj, position, oldObj.transform.rotation, instanceId);
            }

            UnparentSpecifiedChildren(oldObj);
            oldObj.gameObject.SetActive(false);
            _destroyedObjects.Add(oldObj);

            // Reapply impact force to impact object so it punches through the destroyed object along its original path. 
            // If you turn this off, impact objects will be deflected even though the impacted object was destroyed.
            if (damageInfo.GetType() == typeof(ImpactDamage))
                DestructibleHelper.ReapplyImpactForce(damageInfo as ImpactDamage, oldObj.VelocityReduction);
        }

        private static void UnparentSpecifiedChildren(Destructible obj)
        {
            if (obj.unparentOnDestroy == null) return;

            foreach (GameObject child in obj.unparentOnDestroy)
            {
                if (child == null)
                    continue;

                // Unparent the child object from the destructible object.
                child.transform.parent = null;

                // Initialize any DelayedRigidbody scripts on the object.
                DelayedRigidbody[] delayedRigidbodies = child.GetComponentsInChildren<DelayedRigidbody>();
                foreach (DelayedRigidbody dr in delayedRigidbodies)
                    dr.Initialize();

                // Check whether we should turn off Kinematic on child objects, so they will fall freely.
                if (obj.disableKinematicOnUparentedChildren)
                {
                    Rigidbody[] rigidbodies = child.GetComponentsInChildren<Rigidbody>();
                    foreach (Rigidbody rbody in rigidbodies)
                        rbody.isKinematic = false;
                }
                
                // Turn off any animations
                Animation[] animations = child.GetComponentsInChildren<Animation>();
                foreach (Animation anim in animations)
                    anim.enabled = false;
            }
        }

        private void InstantiateDebris<T>(GameObject newObj, Destructible oldObj, T damageInfo)
        {
            // Apply new materials derived from previous object's materials
            if (!oldObj.autoPoolDestroyedPrefab) // if the old object was autopooled, the destroyed object will come from the pool already having the right materials on it.
                DestructibleHelper.TransferMaterials(oldObj, newObj);

            // For SpeedTree terrain trees, turn off the Hue Variation on the shader so it's locked in and the tree can fall without changing hue.
            if (oldObj.isTerrainTree)
                newObj.gameObject.LockHueVariation();

            // Re-scale destroyed version if original destructible object has been scaled. (Scaling rigidbodies in general is bad, but this is put here for convenience.)
            if (oldObj.transform.lossyScale != new Vector3(1f, 1f, 1f)) // if destructible object has been scaled in the scene
                newObj.transform.localScale = oldObj.transform.lossyScale;

            if (oldObj.destroyedPrefabParent != null)
                newObj.transform.parent = oldObj.destroyedPrefabParent.transform;

            if (oldObj.isDebrisChipAway)
            {
                // If we are doing chip-away debris, attach the ChipAwayDebris script to each piece of debris and exit.
                Collider[] debrisColliders = newObj.GetComponentsInChildren<Collider>();
                foreach (Collider coll in debrisColliders)
                {
                    if (coll.gameObject.GetComponent<ChipAwayDebris>() != null) continue;

                    // If there is already an attached non-kinematic rigidbody on the debris piece, remove it.
                    if (coll.attachedRigidbody != null && !coll.attachedRigidbody.isKinematic)
                        coll.attachedRigidbody.gameObject.RemoveComponent<Rigidbody>();

                    ChipAwayDebris chipAwayDebris = coll.gameObject.AddComponent<ChipAwayDebris>();
                    chipAwayDebris.debrisMass = oldObj.chipAwayDebrisMass;
                    chipAwayDebris.debrisDrag = oldObj.chipAwayDebrisDrag;
                    chipAwayDebris.debrisAngularDrag = oldObj.chipAwayDebrisAngularDrag;
                }
                return;
            }

            oldObj.ReparentChildren(newObj); // assign the appropriate parent objects for Children to Reparent

            // Attempt to get the debris rigidbodies from the PooledRigidbody property on the destroyed object first.
            Rigidbody[] debrisRigidbodies = oldObj.PooledRigidbodies;
	        GameObject[] debrisRigidbodyGos = oldObj.PooledRigidbodyGos;

            // If the debris rigidbodies weren't pooled on the destroyed object, try to retrieve them directly from the newly-spawned-in destroyed object instead.
            if (debrisRigidbodies == null || debrisRigidbodies.Length == 0)
            {
                debrisRigidbodies = newObj.GetComponentsInChildren<Rigidbody>();
                debrisRigidbodyGos = new GameObject[debrisRigidbodies.Length];
                for (int i = 0; i < debrisRigidbodies.Length; i++)
                    debrisRigidbodyGos[i] = debrisRigidbodies[i].gameObject;
            }

            // If we found rigidbodies on the destroyed object, assign them to the appropriate layer/tag/queue, and transfer velocity to them.
            if (debrisRigidbodies.Length > 0)
            {
                for (int i = 0; i < debrisRigidbodies.Length; i++)
                {
                    // Assign each piece of debris to the Debris layer if it exists.
                    if (debrisLayer != -1)
                        debrisRigidbodies[i].gameObject.layer = debrisLayer;

                    // Reparent any debris tagged for reparenting.
	                if (oldObj.debrisToReParentByName != null && oldObj.debrisToReParentByName.Count > 0 && oldObj.transform.parent != null && (oldObj.debrisToReParentByName.Contains("ALL DEBRIS") || oldObj.debrisToReParentByName.Contains(debrisRigidbodies[i].name)))
	                {
                        debrisRigidbodies[i].gameObject.transform.parent = oldObj.transform.parent;
                        debrisRigidbodies[i].isKinematic = oldObj.debrisToReParentIsKinematic;
                    }

                    // Add leftover velocity and angular velocity from destroyed object
                    if (!debrisRigidbodies[i].isKinematic)
                    {
                        debrisRigidbodies[i].velocity = oldObj.VelocityFixedUpdate;
                        debrisRigidbodies[i].angularVelocity = oldObj.AngularVelocityFixedUpdate;
                    }

                    // Add debris to the debris queue.
                    Debris debris = new Debris {Rigidbody = debrisRigidbodies[i], GameObject = debrisRigidbodyGos[i]};
                    _debrisPieces.Add(debris);
                    FireActiveDebrisCounterChangedEvent();
                }
            }

            // Attempt to make some of the debris cling to adjacent rigidbodies
            if (oldObj.CheckForClingingDebris)
                newObj.MakeDebrisCling();

            // Reapply impact force to impact object so it punches through the destroyed object along its original path. 
            // If you turn this off, impact objects will be deflected even though the impacted object was destroyed.
            if (damageInfo.GetType() == typeof(ImpactDamage))
                DestructibleHelper.ReapplyImpactForce(damageInfo as ImpactDamage, oldObj.VelocityReduction);

            if (damageInfo.GetType() == typeof(ExplosiveDamage) || damageInfo.GetType() == typeof(ImpactDamage))
                ExplosionHelper.ApplyForcesToDebris(newObj, 1f, damageInfo);
        }

        public void SetProgressiveDamageTexture(Renderer rend, Material sourceMat, DamageLevel damageLevel)
        {
            if (sourceMat == null) return;
            if (!sourceMat.HasProperty("_DetailMask")) return;
            Texture sourceDetailMask = sourceMat.GetTexture("_DetailMask");
            if (sourceDetailMask == null) return;
            if (_detailMasks == null || _detailMasks.Count == 0) return;

            string sourceDetailMaskName = Regex.Replace(sourceDetailMask.name, "_D[0-9]*$", "");
            Texture newDetailMask = null;
            foreach (Texture2D detailMask in _detailMasks)
            {
                if (detailMask.name == $"{sourceDetailMaskName}_D{damageLevel.visibleDamageLevel}")
                {
                    newDetailMask = detailMask;
                    break;
                }
            }

            if (newDetailMask == null) return;

            MaterialPropertyBlock propBlock = new MaterialPropertyBlock();
            rend.GetPropertyBlock(propBlock);
            propBlock.SetTexture("_DetailMask", newDetailMask);
            rend.SetPropertyBlock(propBlock);
        }

        public Texture2D GetDetailMask(Material sourceMat, DamageLevel damageLevel)
        {

            if (sourceMat == null) return null;
            if (!sourceMat.HasProperty("_DetailMask")) return null;
            Texture sourceDetailMask = sourceMat.GetTexture("_DetailMask");
            if (sourceDetailMask == null) return null;
            if (_detailMasks == null || _detailMasks.Count == 0) return null;

            string sourceDetailMaskName = Regex.Replace(sourceDetailMask.name, "_D[0-9]*$", "");

            foreach (Texture2D detailMask in _detailMasks)
            {
                if (detailMask.name == $"{sourceDetailMaskName}_D{damageLevel.visibleDamageLevel - 1}")
                    return detailMask;
            }

            return null;
        }

        /// <summary>Fires when the Destroyed Prefab counter changes.</summary>
        public void FireDestroyedPrefabCounterChangedEvent()
        {
            if (DestroyedPrefabCounterChangedEvent != null) // first, make sure there is at least one listener.
                DestroyedPrefabCounterChangedEvent(); // if so, trigger the event.
        }

        /// <summary>Fires when the Active Debris count changes.</summary>
        public void FireActiveDebrisCounterChangedEvent()
        {
            if (ActiveDebrisCounterChangedEvent != null) // first, make sure there is at least one listener.
                ActiveDebrisCounterChangedEvent(); // if so, trigger the event.
        }
    }
}