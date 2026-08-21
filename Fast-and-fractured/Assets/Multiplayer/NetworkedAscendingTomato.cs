using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections.Generic;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Networked version of Pepe's ascending tomato.
    /// Server-authoritative: handles rising, timing, and spawning descending tomatoes.
    /// Clients see synced position and visual effects.
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
        
        private float _timer;
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
            _timer = 0f;
            _initialized = true;
            _hasExploded = false;
        }
        
        private void Update()
        {
            bool predictsOwner = base.IsOwner && base.IsClientInitialized;
            if (!base.IsServerInitialized && !predictsOwner)
            {
                // Client-side: just do visual rotation
                transform.Rotate(_rotationSpeed * Time.deltaTime);
                return;
            }
            
            if (!_initialized) return;
            
            // Server: handle movement and timing
            transform.position += Vector3.up * _speed * Time.deltaTime;
            transform.Rotate(_rotationSpeed * Time.deltaTime);
            
            _timer += Time.deltaTime;
            
            if (base.IsServerInitialized && _timer >= _ascendingTime && !_hasExploded)
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
