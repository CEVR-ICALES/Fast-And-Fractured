using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections.Generic;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networked version of Pepe's ascending tomato.
    ///
    /// It only rises straight up at a constant speed from where it was launched, so every peer flies
    /// it itself from that origin (launch data in NetworkedPredictedAbilitySpawn's payload). The server
    /// alone times the burst and spawns the descending tomatoes. Its NetworkTransform used to move it
    /// as well: client-authoritative by default, so with a predicted spawn the owner's updates fought
    /// the server's own motion - 12 snap-backs and 25 m single-frame jumps measured on the host
    /// (ShootingProbe, 2026-09-30). Position sync is switched off on every peer.
    /// </summary>
    public class NetworkedAscendingTomato : NetworkBehaviour
    {
        [Header("Visual Settings")]
        [SerializeField] private Vector3 _rotationSpeed = new Vector3(30f, 60f, 45f);
        
        // Synced state
        private NetworkConnection _ownerConnection;
        private GameObject _casterVehicle;
        private Vector3 _originPosition;
        private float _speed;
        private float _descendingSpeed;
        private float _effectDistance;
        private float _ascendingTime;
        private float _effectTime;
        private GameObject _descendingTomatoPrefab;
        
        private float _startTime;
        private bool _initialized;
        private bool _hasExploded;

        private void Awake()
        {
            // The network prefab is a variant of the singleplayer pooled tomato. Remove its
            // local Update/timer path on every peer; disabling it would still leave lifecycle
            // callbacks and pooled registrations alive.
            var legacy = GetComponent<AscendingTomatoBehaviour>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Destroy(legacy);
            }

            var networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.SetSynchronizePosition(false);
                networkTransform.SetSynchronizeRotation(false);
                networkTransform.SetSynchronizeScale(false);
            }
        }
        
        /// <summary>
        /// Called by NetworkedPepeUniqueAbility on the server after spawning.
        /// </summary>
        public void Initialize(
            NetworkConnection owner,
            GameObject casterVehicle,
            Vector3 originPosition,
            float speed,
            float descendingSpeed,
            float effectDistance,
            float ascendingTime,
            float effectTime,
            GameObject descendingTomatoPrefab)
        {
            _ownerConnection = owner;
            _casterVehicle = casterVehicle;
            _originPosition = originPosition;
            _speed = speed;
            _descendingSpeed = descendingSpeed;
            _effectDistance = effectDistance;
            _ascendingTime = ascendingTime;
            _effectTime = effectTime;
            _descendingTomatoPrefab = descendingTomatoPrefab;
            _startTime = Time.time;
            _initialized = true;
            _hasExploded = false;
        }

        /// <summary>Observers: fly the tomato from its launch point; the server does the rest.</summary>
        internal void InitializeReplica(Vector3 originPosition, float speed)
        {
            if (_initialized) return;
            _originPosition = originPosition;
            _speed = speed;
            _startTime = Time.time;
            _initialized = true;
        }
        
        private void Update()
        {
            transform.Rotate(_rotationSpeed * Time.deltaTime);
            if (!_initialized) return;

            float elapsed = Time.time - _startTime;
            transform.position = _originPosition + Vector3.up * (_speed * elapsed);

            if (base.IsServerInitialized && elapsed >= _ascendingTime && !_hasExploded)
            {
                _hasExploded = true;
                ExplodeAndSpawnDescendingTomatoes();
            }
        }
        
        /// <summary>
        /// Server-side: Find all targets in range and spawn descending tomatoes for them.
        /// </summary>
        private void ExplodeAndSpawnDescendingTomatoes()
        {
            MpCanary.Mark("ExplodeAndSpawnDescendingTomatoes ENTER");
            if (_descendingTomatoPrefab == null)
            {
                Debug.LogWarning("[NetworkedAscendingTomato] No descending tomato prefab!");
                DespawnTomato();
                return;
            }
            
            // Find all vehicles in range
            var allVehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            List<NetworkedVehicle> targets = new List<NetworkedVehicle>();
            
            foreach (var vehicle in allVehicles)
            {
                // Skip the caster
                if (_casterVehicle != null && vehicle.gameObject == _casterVehicle) continue;
                if (_casterVehicle != null && vehicle.transform.IsChildOf(_casterVehicle.transform)) continue;
                
                float distance = Vector3.Distance(_originPosition, vehicle.transform.position);
                if (distance <= _effectDistance)
                {
                    targets.Add(vehicle);
                }
            }
            
            // Spawn descending tomato for each target
            foreach (var target in targets)
            {
                SpawnDescendingTomato(target.gameObject);
            }
            
            // Broadcast explosion VFX
            OnExplodeRpc();
            
            // Despawn after a short delay for VFX
            Invoke(nameof(DespawnTomato), 0.5f);
        }
        
        private void SpawnDescendingTomato(GameObject target)
        {
            MpCanary.Mark($"SpawnDescendingTomato ENTER target={target.name}");
            GameObject tomato = Instantiate(_descendingTomatoPrefab, transform.position, Quaternion.identity);
            MpCanary.Mark("SpawnDescendingTomato instantiated");
            
            var networkedDescending = tomato.GetComponent<NetworkedDescendingTomato>();
            if (networkedDescending != null)
            {
                networkedDescending.Initialize(target, _descendingSpeed, _effectTime);
            }
            
            base.ServerManager.Spawn(tomato);
            MpCanary.Mark("SpawnDescendingTomato spawned OK");
        }

        [ObserversRpc]
        private void OnExplodeRpc()
        {
            // Play explosion VFX on all clients
            // The ascending tomato visual can have particle effects that auto-play
            var particles = GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in particles)
            {
                ps.Play();
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
