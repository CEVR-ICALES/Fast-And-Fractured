 using FishNet.Object;
using UnityEngine;
using Utilities;
using Enums;
using FastAndFractured.Abstractions;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;
using FastAndFractured;
namespace FastAndFractured.Multiplayer
{
    public struct NetworkShootParams
    {
        public Pooltype Pooltype;
        public Vector3 InitialVelocity;
        public float Range;
        public float Damage;

        public float PushForce;
        public float ExplosionRadius;
        public Vector3 ExplosionCenterOffset;
        public Vector3 CustomGravity;
        public int BouncingNum;
        public float BouncingStrenght;
        public float TimeToExplode;
        public bool IsMine;
    } 
    public class NetworkedShoot : NetworkBehaviour
    {
        #region PREDICTION_STRUCT
        /// <summary>
        /// Estructura de datos replicada en cada tick que contiene SOLO el input de disparo.
        /// </summary>
        public struct ShootData : IReplicateData
        {
            public bool NormalShoot;
            public bool PushShoot;
            public bool MineShoot;
            public Vector3 AimDirection;

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        public struct ReconcileData : IReconcileData
        { 

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        #region REFERENCES & FIELDS
        private NormalShootHandle _normalShootHandle;
        private PushShootHandle _pushShootHandle;
        private NetworkInputProvider _networkInputProvider; 

        [Header("Cosmetic Projectile Types")]
        [Tooltip("Tipo de Pool para el proyectil cosmético (no-red) de disparo normal.")]
        [SerializeField] private Pooltype _cosmeticNormalBulletType;
        [Tooltip("Tipo de Pool para el proyectil cosmético (no-red) de disparo de empuje.")]
        [SerializeField] private Pooltype _cosmeticPushBulletType;
        [Tooltip("Tipo de Pool para el proyectil cosmético (no-red) de la mina.")]
        [SerializeField] private Pooltype _cosmeticMineType;

        [Header("Networked Projectile Prefabs (server instantiates these directly, NOT from ObjectPoolManager)")]
        [Tooltip("Prefab del proyectil de red para disparo normal. Debe tener NetworkObject + NetworkedProjectile.")]
        [SerializeField] private GameObject _networkedNormalBulletPrefab;
        [Tooltip("Prefab del proyectil de red para disparo de empuje. Debe tener NetworkObject + NetworkedProjectile.")]
        [SerializeField] private GameObject _networkedPushBulletPrefab;
        [Tooltip("Prefab del proyectil de red para la mina. Debe tener NetworkObject + NetworkedProjectile.")]
        [SerializeField] private GameObject _networkedMinePrefab;
        #endregion

        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            _pushShootHandle = GetComponentInChildren<PushShootHandle>();

            // Disable LocalShootController if present (it's on BaseCar.prefab).
            // NetworkedShoot handles both server-auth spawning and local cosmetic prediction.
            var localShoot = GetComponentInChildren<LocalShootController>();
            if (localShoot != null)
            {
                localShoot.enabled = false;
            }
        }

        /// <summary>
        /// Resolve NetworkInputProvider lazily (it may not exist at Awake time).
        /// </summary>
        private NetworkInputProvider GetNetworkInputProvider()
        {
            if (_networkInputProvider == null)
                _networkInputProvider = GetComponentInParent<NetworkInputProvider>();
            return _networkInputProvider;
        }
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            base.TimeManager.OnTick += TimeManager_OnTick;
            // No cosmetic prediction  Eserver-auth bullets arrive fast enough.
            // Cosmetic bullets caused double-rendering on client (pool bullet + networked bullet).
        }
        public override void OnStartServer()
        {
            base.OnStartServer();
            _normalShootHandle.OnShootRequest += HandleServerAuthSpawn;
            _pushShootHandle.OnShootRequest += HandleServerAuthSpawn;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null) TimeManager.OnTick -= TimeManager_OnTick;

            if (_normalShootHandle != null)
            {
                _normalShootHandle.OnShootRequest -= HandleServerAuthSpawn;
            }
            if (_pushShootHandle != null)
            {
                _pushShootHandle.OnShootRequest -= HandleServerAuthSpawn;
            }
        }

        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            {
                BuildShootData(out ShootData sd);
                Replicate(sd, ReplicateState.Ticked);
            }
            else if (base.IsServer)
            {
                Replicate(default, ReplicateState.Ticked);
            }
        }
        public override void CreateReconcile()
        {
            if (base.IsServer)
            { 
                var rd = new ReconcileData(); 
                 Reconcile(rd, Channel.Reliable);
            }
        }
        #endregion

        #region SIMULATION
        
        private void BuildShootData(out ShootData sd)
        {
            sd = default;
            var provider = GetNetworkInputProvider();
            if (provider == null) return;

            sd.NormalShoot = provider.IsNormalShooting;
            sd.PushShoot = provider.FirePushAction;
            sd.MineShoot = provider.IsThrowingMine;
            sd.AimDirection = provider.AimDirection;
        }

        [Replicate]
        private void Replicate(ShootData sd, ReplicateState state, Channel channel = Channel.Unreliable)
        {
            // El cliente no-dueño y el servidor deben saber a dónde apuntar para la IA y otros.
            if (!base.IsOwner)
            {
                // Si este vehículo no es tuyo, el input vendrá del servidor (replicado desde otro cliente o IA)
                // Aquí deberíamos tener un 'NetworkedInputProvider' que se llene desde la red.
                // Por ahora, asumimos que la IA del servidor actuará directamente.
            }

            // Asignar dirección de apuntado. En el servidor, sd estará vacío a menos que se le rellene para la IA.
            // Para el cliente dueño, sd viene de BuildShootData.
            if (sd.AimDirection != Vector3.zero) // Solo actualiza si hay un input válido
            {
                _normalShootHandle.CurrentShootDirection = sd.AimDirection;
                _pushShootHandle.CurrentShootDirection = sd.AimDirection;
            }

            if (sd.NormalShoot) _normalShootHandle.NormalShooting();
            if (sd.PushShoot) _pushShootHandle.PushShooting();
            if (sd.MineShoot) _pushShootHandle.MineShoot();
        }
         
        [Reconcile]
        private void Reconcile(ReconcileData rd,  Channel channel = Channel.Unreliable)
        {
            // Aunque este método esté vacío en el cliente, debe existir.
            // Aquí es donde pondrías la lógica si necesitaras corregir
            // el estado de la UI del cooldown, por ejemplo.
        }
        #endregion

        #region EVENT_HANDLERS_&_SPAWNING

        private void HandleLocalShootPrediction(BulletSpawnParameters spawnParams)
        {
            // Se llama en el cliente dueño como resultado del Replicate
            SpawnCosmeticProjectile(spawnParams);
        }

        private void HandleServerAuthSpawn(BulletSpawnParameters spawnParams)
        {
            if (!base.IsServer) return;

            GameObject prefab = GetNetworkedPrefabFromSpawnParams(spawnParams);
            if (prefab == null)
            {
                Debug.LogError($"[Server] No networked prefab assigned for this bullet type. Check NetworkedShoot inspector.");
                return;
            }

            // Instantiate fresh (don't use ObjectPoolManager  EFishNet Despawn destroys pool objects)
            GameObject spawnedBulletGO = Instantiate(prefab, spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);
            spawnedBulletGO.transform.SetParent(null);

            var networkObject = spawnedBulletGO.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[Server] Networked bullet prefab missing NetworkObject.", spawnedBulletGO);
                Destroy(spawnedBulletGO);
                return;
            }

            base.ServerManager.Spawn(spawnedBulletGO, base.Owner);

            var networkedProjectile = spawnedBulletGO.GetComponent<NetworkedProjectile>();
            if (networkedProjectile != null)
            {
                NetworkShootParams netParams = new NetworkShootParams
                {
                    Pooltype = spawnParams.Pooltype,
                    InitialVelocity = spawnParams.Velocity,
                    Range = spawnParams.Range,
                    Damage = spawnParams.Damage,
                    PushForce = spawnParams.PushForce,
                    ExplosionRadius = spawnParams.ExplosionRadius,
                    ExplosionCenterOffset = spawnParams.ExplosionCenterOffset,
                    CustomGravity = spawnParams.CustomGravity,
                    BouncingNum = spawnParams.BouncingNum,
                    BouncingStrenght = spawnParams.BouncingStrenght,
                    TimeToExplode = spawnParams.TimeToExplode,
                    IsMine = spawnParams.IsMine
                };

                networkedProjectile.RpcInitialize(netParams, base.NetworkObject, 
                    spawnedBulletGO.transform.position, spawnedBulletGO.transform.rotation);
            }
            else
            {
                Debug.LogError($"[Server] Networked bullet prefab missing NetworkedProjectile.", spawnedBulletGO);
                networkObject.Despawn();
            }
        }

        private GameObject GetNetworkedPrefabFromSpawnParams(BulletSpawnParameters spawnParams)
        {
            if (spawnParams.IsMine) return _networkedMinePrefab;
            if (spawnParams.Pooltype == _normalShootHandle.Pooltype) return _networkedNormalBulletPrefab;
            return _networkedPushBulletPrefab;
        }

        private void SpawnCosmeticProjectile(BulletSpawnParameters spawnParams)
        {
            Pooltype cosmeticType;
            if (spawnParams.IsMine)
                cosmeticType = _cosmeticMineType;
            else if (spawnParams.Pooltype == _normalShootHandle.Pooltype)
                cosmeticType = _cosmeticNormalBulletType;
            else
                cosmeticType = _cosmeticPushBulletType;

            GameObject cosmeticBulletGO = ObjectPoolManager.Instance.GivePooledObject(cosmeticType);
            if (cosmeticBulletGO == null) return;

            cosmeticBulletGO.transform.SetPositionAndRotation(spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);

            var bulletBehaviour = cosmeticBulletGO.GetComponent<BulletBehaviour>();
            if (bulletBehaviour != null)
            {
                ConfigureBullet(bulletBehaviour, spawnParams, true); // Es cosmético
                bulletBehaviour.InitBulletTrayectory();
            }
        }

        private void ConfigureBullet(BulletBehaviour bullet, BulletSpawnParameters parameters, bool isCosmetic)
        {
            bullet.Velocity = parameters.Velocity;
            bullet.Range = parameters.Range;
            bullet.Damage = isCosmetic ? 0 : parameters.Damage;

            if (!isCosmetic) bullet.gameObject.layer = LayerMask.NameToLayer("Default");
            else bullet.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            if (bullet is NormalBulletBehaviour nbb)
            {
                nbb.IgnoreCollider = parameters.IgnoredCollider;
            }
            else if (bullet is PushBulletBehaviour pbb)
            {
                pbb.PushForce = parameters.PushForce;
                pbb.ExplosionRadius = parameters.ExplosionRadius;
                pbb.ExplosionCenterOffset = parameters.ExplosionCenterOffset;
                pbb.CustomGravity = parameters.CustomGravity;
                pbb.BouncingNum = parameters.BouncingNum;
                pbb.BouncingStrenght = parameters.BouncingStrenght;
                pbb.TimeToExplode = parameters.TimeToExplode;
            }
            if (isCosmetic)
            {
                bullet.gameObject.GetComponentInChildren<Renderer>().material.color = Color.red;
                gameObject.name = "Cosmetic" + gameObject.name;
            }
            else
            {
                bullet.gameObject.GetComponentInChildren<Renderer>().material.color = Color.green;
                gameObject.name = "REAL" + gameObject.name;

            }
        }
        #endregion
    }
} 
