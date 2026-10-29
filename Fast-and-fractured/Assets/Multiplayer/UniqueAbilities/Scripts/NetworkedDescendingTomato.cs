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
    /// Pepe's descending tomato: every peer flies it toward its target car (synced with the spawn); the server
    /// decides the hit and applies the debuff.
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
        private const float DESPAWN_DELAY_SECONDS = 0.1f;
        private const float LOCAL_HIT_DISTANCE = 5f;

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
        
        private Vector3 _lastServerPosition;
        private Vector3 _targetPosition;
        private float _interpTimer;
        private const float INTERP_DURATION = 0.1f;
        
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
                DespawnTomato();
                return;
            }
            
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
                
                var networkedStats = other.GetComponentInParent<NetworkedStatsController>();
                if (networkedStats != null)
                {
                    var statsController = other.GetComponentInParent<StatsController>();
                    if (statsController != null)
                    {
                        statsController.TemporalProductStat(Stats.MAX_SPEED, _percentDebuffSpeed, _effectTime);
                    }
                }
                
                OnHitRpc(other.transform.position);
                
                Invoke(nameof(DespawnTomato), DESPAWN_DELAY_SECONDS);
            }
        }
        
        [ObserversRpc]
        private void OnHitRpc(Vector3 hitPosition)
        {
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Play();
            }
            
            var localPlayer = LevelController.Instance?.LocalPlayer;
            if (localPlayer != null)
            {
                float dist = Vector3.Distance(hitPosition, localPlayer.transform.position);
                if (dist < LOCAL_HIT_DISTANCE)
                {
                    if (IngameEventsManager.Instance != null)
                    {
                        IngameEventsManager.Instance.RemoveAlert();
                    }
                    
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
