using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using System.Collections.Generic;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Pepe's ascending tomato: every peer flies it straight up from its launch point; the server times the burst
    /// and spawns the descending tomatoes. NetworkTransform position sync is off (it fought the predicted spawn).
    /// </summary>
    public class NetworkedAscendingTomato : NetworkBehaviour
    {
        private const float DESPAWN_DELAY_SECONDS = 0.5f;
        private const float AREA_FADE_SECONDS = AscendingTomatoBehaviour.TIME_BEFORE_START_FADE;

        [Header("Visual Settings")]
        [SerializeField] private Vector3 _rotationSpeed = new Vector3(30f, 60f, 45f);
        
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
        private bool _areaFading;
        private TomatoDecal _areaDecal;
        private Quaternion _areaRotation;

        private void Awake()
        {
            // Remove the pooled singleplayer tomato's own timer path on every peer.
            var legacy = GetComponent<AscendingTomatoBehaviour>();
            if (legacy != null)
            {
                legacy.enabled = false;
                Destroy(legacy);
            }

            _areaDecal = GetComponentInChildren<TomatoDecal>(true);
            if (_areaDecal != null) _areaRotation = _areaDecal.transform.localRotation;

            var networkTransform = GetComponent<FishNet.Component.Transforming.NetworkTransform>();
            if (networkTransform != null)
            {
                networkTransform.SetSynchronizePosition(false);
                networkTransform.SetSynchronizeRotation(false);
                networkTransform.SetSynchronizeScale(false);
            }
        }
        
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
            ShowArea();
        }

        /// <summary>Observers: fly the tomato from its launch point; the server does the rest.</summary>
        internal void InitializeReplica(Vector3 originPosition, float speed, float effectDistance, float ascendingTime)
        {
            if (_initialized) return;
            _originPosition = originPosition;
            _speed = speed;
            _effectDistance = effectDistance;
            _ascendingTime = ascendingTime;
            _startTime = Time.time;
            _initialized = true;
            ShowArea();
        }

        /// <summary>The area the singleplayer AscendingTomatoBehaviour draws: it is removed here, so nothing else sizes the decal.</summary>
        private void ShowArea()
        {
            _areaFading = false;
            if (_areaDecal != null) _areaDecal.SetRadius(_effectDistance);
        }
        
        private void Update()
        {
            transform.Rotate(_rotationSpeed * Time.deltaTime);
            if (_areaDecal != null) _areaDecal.transform.rotation = _areaRotation;
            if (!_initialized) return;

            float elapsed = Time.time - _startTime;
            transform.position = _originPosition + Vector3.up * (_speed * elapsed);

            if (!_areaFading && _areaDecal != null && elapsed >= _ascendingTime - AREA_FADE_SECONDS)
            {
                _areaFading = true;
                _areaDecal.DecalFadeOut(AREA_FADE_SECONDS);
            }

            if (base.IsServerInitialized && elapsed >= _ascendingTime && !_hasExploded)
            {
                _hasExploded = true;
                ExplodeAndSpawnDescendingTomatoes();
            }
        }
        
        private void ExplodeAndSpawnDescendingTomatoes()
        {
            MpCanary.Mark("ExplodeAndSpawnDescendingTomatoes ENTER");
            if (_descendingTomatoPrefab == null)
            {
                Debug.LogWarning("[NetworkedAscendingTomato] No descending tomato prefab!");
                DespawnTomato();
                return;
            }
            
            var allVehicles = FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            List<NetworkedVehicle> targets = new List<NetworkedVehicle>();
            
            foreach (var vehicle in allVehicles)
            {
                if (_casterVehicle != null && vehicle.gameObject == _casterVehicle) continue;
                if (_casterVehicle != null && vehicle.transform.IsChildOf(_casterVehicle.transform)) continue;
                
                float distance = Vector3.Distance(_originPosition, vehicle.transform.position);
                if (distance <= _effectDistance)
                {
                    targets.Add(vehicle);
                }
            }
            
            foreach (var target in targets)
            {
                SpawnDescendingTomato(target.gameObject);
            }
            
            OnExplodeRpc();
            
            Invoke(nameof(DespawnTomato), DESPAWN_DELAY_SECONDS);
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
