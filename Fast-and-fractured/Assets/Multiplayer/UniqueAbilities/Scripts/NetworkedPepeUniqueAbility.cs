using FishNet.Object;
using UnityEngine;
using System.Collections.Generic;
using Enums;
using System.Collections;
using FishNet;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Networks Pepe's ultimate: the server spawns networked tomatoes instead of the pooled singleplayer ones.</summary>
    public class NetworkedPepeUniqueAbility : NetworkBehaviour
    {
        private const float MAX_SPAWN_SQR_ERROR = 25f;

        [Header("Networked Prefabs")]
        [SerializeField] private GameObject _networkedAscendingTomatoPrefab;
        [SerializeField] private GameObject _networkedDescendingTomatoPrefab;
        
        [Header("Ability Settings (mirrors PepeUniqueAbility)")]
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _tomatoSpeed = 100f;
        [SerializeField] private float _descendingTomatoSpeed = 100f;
        [SerializeField] private float _effectDistance = 50f;
        [SerializeField] private float _ascendingTime = 3f;
        [SerializeField] private float _effectTime = 5f;
        
        private PepeUniqueAbility _localAbility;
        private bool _intercepted;
        
        private void Awake()
        {
            _localAbility = GetComponentInChildren<PepeUniqueAbility>();
        }
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            if (_localAbility == null)
            {
                Debug.LogWarning($"[NetworkedPepeUniqueAbility] No PepeUniqueAbility found on {gameObject.name}");
                enabled = false;
                return;
            }
            
            CopyAbilitySettings();
            
            _localAbility.onAbilityActivated.AddListener(OnAbilityActivated);
        }
        
        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (_localAbility != null)
            {
                _localAbility.onAbilityActivated.RemoveListener(OnAbilityActivated);
            }
        }
        
        private void CopyAbilitySettings()
        {
            var abilityType = typeof(PepeUniqueAbility);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            
            var spawnPointField = abilityType.GetField("spawnPoint", flags);
            var tomatoSpeedField = abilityType.GetField("tomatoSpeed", flags);
            var descendingSpeedField = abilityType.GetField("descendingTomatoSpeed", flags);
            var distanceField = abilityType.GetField("distanceEffect", flags);
            var ascendingTimeField = abilityType.GetField("ascendingTime", flags);
            
            if (spawnPointField != null)
            {
                var spawnGO = spawnPointField.GetValue(_localAbility) as GameObject;
                if (spawnGO != null) _spawnPoint = spawnGO.transform;
            }
            if (tomatoSpeedField != null) _tomatoSpeed = (float)tomatoSpeedField.GetValue(_localAbility);
            if (descendingSpeedField != null) _descendingTomatoSpeed = (float)descendingSpeedField.GetValue(_localAbility);
            if (distanceField != null) _effectDistance = (float)distanceField.GetValue(_localAbility);
            if (ascendingTimeField != null) _ascendingTime = (float)ascendingTimeField.GetValue(_localAbility);
        }
        
        private void OnAbilityActivated()
        {
            bool serverAi = base.IsServerInitialized && (base.Owner == null || !base.Owner.IsValid);
            if (!base.IsOwner && !serverAi) return;

            StartCoroutine(CancelSingleplayerTomatoNextFrame());

            Vector3 spawnPos = _spawnPoint != null ? _spawnPoint.position : transform.position;

            // A [ServerRpc] called from the host never runs its logic.
            if (base.IsServerInitialized)
                SpawnAscendingTomatoOnServer(spawnPos);
            else
                StartCoroutine(SpawnAscendingTomatoPredictedNextFrame(spawnPos));
        }

        /// <summary>Clears the Caster of the pooled tomato the singleplayer ability hands this car, so it spawns nothing.</summary>
        private IEnumerator CancelSingleplayerTomatoNextFrame()
        {
            yield return null;
            var controller = GetComponentInParent<StateMachine.Controller>();
            GameObject caster = controller != null ? controller.gameObject : null;
            foreach (var tomato in FindObjectsByType<AscendingTomatoBehaviour>(FindObjectsSortMode.None))
            {
                if (tomato == null || tomato.GetComponent<NetworkObject>() != null) continue;
                if (caster != null && tomato.Caster != caster) continue;
                tomato.Caster = null;
                var pool = global::Utilities.ObjectPoolManager.ExistingInstance;
                if (pool != null)
                    pool.DeactivatePooledObject(tomato, tomato.gameObject);
                else
                    tomato.gameObject.SetActive(false);
            }
        }

        private IEnumerator SpawnAscendingTomatoPredictedNextFrame(Vector3 spawnPosition)
        {
            yield return null; // activation ServerRpc is queued first on FishNet's reliable channel.
            if (_networkedAscendingTomatoPrefab == null) yield break;
            var tomato = Instantiate(_networkedAscendingTomatoPrefab, spawnPosition, Quaternion.identity);
            var payload = tomato.GetComponent<NetworkedPredictedAbilitySpawn>();
            if (payload == null) { Destroy(tomato); yield break; }
            tomato.GetComponent<NetworkedAscendingTomato>()?.Initialize(base.Owner, gameObject,
                spawnPosition, _tomatoSpeed, _descendingTomatoSpeed, _effectDistance,
                _ascendingTime, _effectTime, _networkedDescendingTomatoPrefab);
            payload.ConfigurePepe(base.NetworkObject, spawnPosition);
            InstanceFinder.ServerManager.Spawn(tomato, base.Owner);
        }

        [ServerRpc]
        private void SpawnAscendingTomatoServerRpc(Vector3 spawnPosition)
        {
            SpawnAscendingTomatoOnServer(spawnPosition);
        }

        [Server]
        private void SpawnAscendingTomatoOnServer(Vector3 spawnPosition)
        {
            MpCanary.Mark("SpawnAscendingTomatoOnServer ENTER");
            if (_networkedAscendingTomatoPrefab == null)
            {
                Debug.LogError("[NetworkedPepeUniqueAbility] No networked ascending tomato prefab assigned!");
                return;
            }
            
            GameObject tomato = Instantiate(_networkedAscendingTomatoPrefab, spawnPosition, Quaternion.identity);

            // Same launch data as a predicted tomato, so observers fly it from its origin.
            tomato.GetComponent<NetworkedPredictedAbilitySpawn>()?.ConfigurePepe(base.NetworkObject, spawnPosition);
            
            var networkedTomato = tomato.GetComponent<NetworkedAscendingTomato>();
            if (networkedTomato != null)
            {
                networkedTomato.Initialize(
                    base.Owner,
                    gameObject,
                    spawnPosition,
                    _tomatoSpeed,
                    _descendingTomatoSpeed,
                    _effectDistance,
                    _ascendingTime,
                    _effectTime,
                    _networkedDescendingTomatoPrefab
                );
            }
            
            base.ServerManager.Spawn(tomato);
        }

        internal float TomatoSpeed => _tomatoSpeed;
        internal float EffectDistance => _effectDistance;
        internal float AscendingTime => _ascendingTime;

        internal bool InitializeValidatedPredictedTomato(NetworkedAscendingTomato tomato,
            Vector3 requestedPosition)
        {
            if (!base.IsServerInitialized || tomato == null) return false;
            Vector3 authoritativePosition = _spawnPoint != null ? _spawnPoint.position : transform.position;
            if ((requestedPosition - authoritativePosition).sqrMagnitude > MAX_SPAWN_SQR_ERROR) return false;
            tomato.transform.position = authoritativePosition;
            tomato.Initialize(base.Owner, gameObject, authoritativePosition, _tomatoSpeed,
                _descendingTomatoSpeed, _effectDistance, _ascendingTime, _effectTime,
                _networkedDescendingTomatoPrefab);
            return true;
        }
    }
}
