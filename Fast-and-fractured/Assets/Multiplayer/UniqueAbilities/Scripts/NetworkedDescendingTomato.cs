using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Enums;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networked version of Pepe's descending tomato.
    ///
    /// It homes onto one car at a fixed speed, so every peer flies it itself toward that car as it
    /// sees it (target and speed arrive with the spawn as SyncVars). The server alone decides the hit
    /// and applies the debuff. Drawn through its NetworkTransform instead, it arrived on clients in
    /// uneven steps: 3 m off a smooth path at 100 m/s (ShootingProbe, 2026-09-30).
    /// </summary>
    public class NetworkedDescendingTomato : NetworkBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Vector3 _rotationSpeed = new Vector3(45f, 30f, 60f);
        
        [Header("Effect Settings")]
        [SerializeField] private float _percentDebuffSpeed = 0.5f;
        
        // Replicated with the spawn so every peer can fly it.
        private readonly SyncVar<NetworkObject> _targetObject = new SyncVar<NetworkObject>();
        private readonly SyncVar<float> _syncedSpeed = new SyncVar<float>();

        // On clients: reached the target, waiting for the server's hit and despawn.
        private bool _arrived;
        private const float ARRIVAL_DISTANCE = 1f;

        // Server state
        private GameObject _target;
        private float _speed;
        private float _effectTime;
        private bool _initialized;
        private bool _hasHit;

        private static readonly FieldInfo PercentDebuffSpeedField =
            typeof(DescendingTomatoBehaviour).GetField("percentDebuffSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private void Awake()
        {
            var legacy = GetComponent<DescendingTomatoBehaviour>();
            if (legacy == null)
                return;

            if (PercentDebuffSpeedField != null)
                _percentDebuffSpeed = (float)PercentDebuffSpeedField.GetValue(legacy);

            var networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.SetSynchronizePosition(false);
                networkTransform.SetSynchronizeRotation(false);
                networkTransform.SetSynchronizeScale(false);
            }

            StartCoroutine(RemoveLegacyAfterColliderQuarantine(legacy));
        }

        private IEnumerator RemoveLegacyAfterColliderQuarantine(DescendingTomatoBehaviour legacy)
        {
            var collidersToRestore = new List<Collider>();
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                collidersToRestore.Add(collider);
            }

            legacy.enabled = false;
            Destroy(legacy);
            yield return null;

            foreach (var collider in collidersToRestore)
            {
                if (collider != null)
                    collider.enabled = true;
            }
        }
        
        // Client interpolation
        private Vector3 _lastServerPosition;
        private Vector3 _targetPosition;
        private float _interpTimer;
        private const float INTERP_DURATION = 0.1f;
        
        /// <summary>
        /// Called by NetworkedAscendingTomato on the server after spawning.
        /// </summary>
        public void Initialize(GameObject target, float speed, float effectTime)
        {
            _target = target;
            _speed = speed;
            _effectTime = effectTime;
            _initialized = true;
            _hasHit = false;
            _targetObject.Value = target != null ? target.GetComponent<NetworkObject>() : null;
            _syncedSpeed.Value = speed;
        }
        
        private void Update()
        {
            // Visual rotation on all clients
            transform.Rotate(_rotationSpeed * Time.deltaTime);
            
            if (base.IsServerInitialized)
            {
                ServerUpdate();
            }
            else if (base.IsClientInitialized)
            {
                ClientUpdate();
            }
        }

        private void ClientUpdate()
        {
            NetworkObject target = _targetObject.Value;
            if (_arrived || target == null) return;
            Vector3 toTarget = target.transform.position - transform.position;
            float step = _syncedSpeed.Value * Time.deltaTime;
            if (toTarget.magnitude <= Mathf.Max(ARRIVAL_DISTANCE, step))
            {
                transform.position = target.transform.position;
                _arrived = true;
                return;
            }
            transform.position += toTarget.normalized * step;
        }
        
        private void ServerUpdate()
        {
            if (!_initialized || _hasHit) return;
            
            if (_target == null)
            {
                // Target destroyed, despawn
                DespawnTomato();
                return;
            }
            
            // Move toward target
            Vector3 direction = (_target.transform.position - transform.position).normalized;
            transform.position += direction * _speed * Time.deltaTime;
        }
        
        private static int _charactersLayer = -1;

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized) return;
            if (_hasHit) return;

            if (_charactersLayer < 0) _charactersLayer = LayerMask.NameToLayer("Characters");

            if (other.gameObject.layer == _charactersLayer)
            {
                _hasHit = true;
                
                // Find NetworkedStatsController and apply debuff
                var networkedStats = other.GetComponentInParent<NetworkedStatsController>();
                if (networkedStats != null)
                {
                    // Apply speed debuff server-side
                    var statsController = other.GetComponentInParent<StatsController>();
                    if (statsController != null)
                    {
                        statsController.TemporalProductStat(Stats.MAX_SPEED, _percentDebuffSpeed, _effectTime);
                    }
                }
                
                // Notify clients of hit for VFX
                OnHitRpc(other.transform.position);
                
                // Despawn after short delay for VFX
                Invoke(nameof(DespawnTomato), 0.1f);
            }
        }
        
        [ObserversRpc]
        private void OnHitRpc(Vector3 hitPosition)
        {
            // Play hit VFX on all clients
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Play();
            }
            
            // Check if this hit the local player
            var localPlayer = LevelController.Instance?.LocalPlayer;
            if (localPlayer != null)
            {
                float dist = Vector3.Distance(hitPosition, localPlayer.transform.position);
                if (dist < 5f) // Hit local player
                {
                    // Remove alert
                    if (IngameEventsManager.Instance != null)
                    {
                        IngameEventsManager.Instance.RemoveAlert();
                    }
                    
                    // Show tomato effect on HUD
                    if (HUDManager.Instance != null && ResourcesManager.Instance != null)
                    {
                        HUDManager.Instance.UpdateUIEffect(
                            UIDynamicElementType.BAD_EFFECTS, 
                            ResourcesManager.Instance.GetResourcesSprite(ScreenEffects.TOMATO_EFFECT), 
                            _effectTime);
                    }
                }
            }
        }
        
        private void DespawnTomato()
        {
            if (base.IsServerInitialized)
            {
                base.ServerManager.Despawn(gameObject);
            }
        }
    }
}
