using System;
using System.Linq;
using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Particle Manager (Singleton) - manages the playing of particle effects and handles performance throttling.
    /// Call the PlayEffect() method, and this script decides whether to play the effect based on how many are currently active.
    /// </summary>
    [DisallowMultipleComponent]
    public class ParticleManager : MonoBehaviour
    {
        public int maxDestroyedParticles = 20; // max particles to allow during [withinSeconds].
        public int maxPerDestructible = 5;  // max particles to allow for a single Destructible object or DestructibleGroup.
        public float withinSeconds = 4f;    // remove particles from the managed list after this many seconds.
        public float updateFrequency = .5f; // The time (in seconds) this script updates its counters

        public static ParticleManager Instance { get; private set; }
        public ActiveParticle[] ActiveParticles 
        { 
            get => _activeParticles;
            private set => _activeParticles = value;
        }
        public bool IsMaxActiveParticles => ActiveParticles.Length >= maxDestroyedParticles;

        private float _nextUpdate;
        private ActiveParticle[] _activeParticles;
        private ParticleManager() { } // hide constructor

        // Events
        public event Action ActiveParticlesCounterChangedEvent;

        public void Awake()
        {
            ActiveParticles = new ActiveParticle[0];
            Instance = this;
            _nextUpdate = Time.time + updateFrequency;
        }

        public void Update()
        {
            if (!(Time.time > _nextUpdate)) return;
            if (_activeParticles.Length == 0) return;

            int removeIndicesCounter = 0;
            int[] removeIndices = new int[0];
            bool isChanged = false;
            for (int i = 0; i < ActiveParticles.Length;i++ )
            {
                if (Time.time >= ActiveParticles[i].InstantiatedTime + withinSeconds)
                {
                    isChanged = true;
                    removeIndicesCounter++;
                    Array.Resize(ref removeIndices, removeIndicesCounter);
                    removeIndices[removeIndicesCounter - 1] = i;
                }
            }
            _activeParticles = _activeParticles.RemoveAllAt(removeIndices);
            if (isChanged)
                FireActiveParticlesCounterChangedEvent();

            // Reset the nextUpdate counter.
            _nextUpdate = Time.time + updateFrequency; 
        }

        /// <summary>Plays a particle effect and adjusts its texture to have maximum damage level progressive damage (if specified).</summary>
        public void PlayEffect(ParticleSystem particle, Destructible destObj, Vector3 pos, Quaternion rot, int parentId)
        {
            if (particle == null)
                particle = DestructionManager.Instance.defaultParticle;

            // Check if we're at the maximum active particle limit. If so, ignore the request to play the particle effect.
            if (IsMaxActiveParticles) return;

            // Check if we've reached the max particle limit per destructible object for this object already.
            int parentParticleCount = ActiveParticles.Count(x => x.ParentId == parentId);
            if (parentParticleCount > maxPerDestructible) return;

            // Instantiate and add to the ActiveParticles counter
            GameObject spawn = ObjectPool.Instance.Spawn(particle.gameObject, pos, rot);
            if (spawn == null || spawn.GetComponent<ParticleSystem>() == null) return;
            ActiveParticle aParticle = new ActiveParticle { GameObject = spawn, InstantiatedTime = Time.time, ParentId = parentId };
            Array.Resize(ref _activeParticles, _activeParticles.Length + 1);
            ActiveParticles[_activeParticles.Length - 1] = aParticle;
            FireActiveParticlesCounterChangedEvent();

            // If a particle scale override has been specified...
            if (destObj.fallbackParticleScale != Vector3.one)
            {
                var particleSystems = spawn.GetComponentsInChildren<ParticleSystem>();
                foreach (ParticleSystem ps in particleSystems)
                {
                    var main = ps.main;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                }

                spawn.transform.localScale = destObj.fallbackParticleScale;

                // If the particle effect is being put back into the object pool after use, set the PoolAfter script to reset this object back to its prefab,
                // so it won't have the scale overrides active when it plays again.
                var poolAfter = spawn.GetComponent<PoolAfter>();
                if (poolAfter != null)
                    poolAfter.resetToPrefab = true;
            }

            // Parent the particle effect under the fallback particle parent, if specified.
            if (destObj.fallbackParticleParent != null)
                spawn.transform.SetParent(destObj.fallbackParticleParent);

            // Particle Effect Material Replacement
            if (destObj.fallbackParticleMatOption == 1) return; // No material replacement was selected, so just exit.

            // Get the particle system renderers so we can replace the materials on them.
            if (spawn.GetComponent<ParticleSystem>() == null) return;
            ParticleSystemRenderer[] particleRenderers = spawn.GetComponentsInChildren<ParticleSystemRenderer>();

            // Replace particle materials with the one (index 0) from the destroyed object.
            if (destObj.fallbackParticleMatOption == 0) 
            {
                foreach (ParticleSystemRenderer particleRenderer in particleRenderers)
                {
                    if (particleRenderer.renderMode != ParticleSystemRenderMode.Mesh) continue;

                    particleRenderer.material = destObj.GetDestroyedParticleEffectMaterial();

                    if (particleRenderer.sharedMaterial.IsProgressiveDamageCapable())
                    {
                        Texture2D detailMask = DestructionManager.Instance.GetDetailMask(particleRenderer.sharedMaterial, destObj.damageLevels[destObj.damageLevels.Count - 1]);
                        particleRenderer.material.SetTexture("_DetailMask", detailMask);
                    }
                }
            }

            // Replace particle materials with custom ones specified on the Destructible object.
            if (destObj.fallbackParticleMatOption == 2)
            {
                foreach (ParticleSystemRenderer particleRenderer in particleRenderers)
                {
                    if (particleRenderer.renderMode != ParticleSystemRenderMode.Mesh) continue;

                    // First, see if we need to replace the material with one defined on the Destructible script.
                    MaterialMapping matMap = destObj.replaceParticleMats.Find(x => x.SourceMaterial == particleRenderer.sharedMaterial);
                    Material newMat = matMap == null ? particleRenderer.sharedMaterial : matMap.ReplacementMaterial;

                    particleRenderer.material = newMat ?? destObj.GetDestroyedParticleEffectMaterial();
                    if (particleRenderer.sharedMaterial.IsProgressiveDamageCapable())
                    {
                        Texture2D detailMask = DestructionManager.Instance.GetDetailMask(particleRenderer.sharedMaterial, destObj.damageLevels[destObj.damageLevels.Count - 1]);
                        particleRenderer.material.SetTexture("_DetailMask", detailMask);
                    }
                }
            }
        }

        /// <summary>Fires when the number of Active Particles changes.</summary>
        public void FireActiveParticlesCounterChangedEvent()
        {
            if (ActiveParticlesCounterChangedEvent != null) // first, make sure there is at least one listener.
                ActiveParticlesCounterChangedEvent(); // if so, trigger the event.
        }
    }
}