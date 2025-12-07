 using FishNet.Object;
using UnityEngine;
using Utilities;
using Enums;
using FastAndFractured.Abstractions;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using FishNet.Connection;
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
        private PlayerInputProvider _inputProvider; 

        [Header("Cosmetic Projectile Types")]
        [Tooltip("Tipo de Pool para el proyectil cosmético (no-red) de disparo normal.")]
        [SerializeField] private Pooltype _cosmeticNormalBulletType;
        [Tooltip("Tipo de Pool para el proyectil cosmético (no-red) de disparo de empuje.")]
        [SerializeField] private Pooltype _cosmeticPushBulletType;
        [Tooltip("Tipo de Pool para el proyectil cosmético (no-red) de la mina.")]
        [SerializeField] private Pooltype _cosmeticMineType;

        [Header("Networked Projectile Prefabs")]
        [SerializeField] private GameObject _networkedNormalBulletPrefab;
        [SerializeField] private GameObject _networkedPushBulletPrefab;
        [SerializeField] private GameObject _networkedMinePrefab;
        #endregion

        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            _pushShootHandle = GetComponentInChildren<PushShootHandle>();
            _inputProvider = GetComponentInParent<PlayerInputProvider>();  
        }
        public override void OnOwnershipClient(NetworkConnection prevOwner)
        {
            base.OnOwnershipClient(prevOwner);
            base.TimeManager.OnTick += TimeManager_OnTick;

            if (base.IsOwner)
            {
                _normalShootHandle.OnShootRequest += HandleLocalShootPrediction;
                _pushShootHandle.OnShootRequest += HandleLocalShootPrediction;
            }
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
                _normalShootHandle.OnShootRequest -= HandleLocalShootPrediction;
                _normalShootHandle.OnShootRequest -= HandleServerAuthSpawn;
            }
            if (_pushShootHandle != null)
            {
                _pushShootHandle.OnShootRequest -= HandleLocalShootPrediction;
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
            if (base.IsServer)
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
            if (_inputProvider == null) return;

            sd.NormalShoot = _inputProvider.IsNormalShooting;
            sd.PushShoot = _inputProvider.IsPushShooting;
            sd.MineShoot = _inputProvider.IsThrowingMine;
            sd.AimDirection = _inputProvider.AimDirection;
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

            GameObject prefabToSpawn = GetNetworkedPrefabFromSpawnParams(spawnParams);
            if (prefabToSpawn == null)
            {
                Debug.LogError($"[Server] No se encontró un Prefab de red para el tipo: {spawnParams.Pooltype}");
                return;
            }

            GameObject spawnedBulletGO = Instantiate(prefabToSpawn, spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation,transform);
            base.ServerManager.Spawn(spawnedBulletGO, base.Owner);

            var networkedProjectile = spawnedBulletGO.GetComponent<NetworkedProjectile>();
            if (networkedProjectile != null)
            {
                // ##### LA CORRECCIÓN ESTÁ AQUÍ #####
                // Convertimos los datos del servidor a un formato de red seguro antes de enviar el RPC.
                NetworkShootParams netParams = new NetworkShootParams
                {
                    Pooltype = spawnParams.Pooltype,
                    InitialVelocity = spawnParams.Velocity, // La velocidad ya incluye la del vehículo.
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

                networkedProjectile.RpcInitialize(netParams, base.Owner.FirstObject);
            }
            else
            {
                Debug.LogError($"El prefab de red '{prefabToSpawn.name}' no tiene el script NetworkedProjectile.", prefabToSpawn);
                spawnedBulletGO.GetComponent<NetworkObject>().Despawn();
            }
        }

        private GameObject GetNetworkedPrefabFromSpawnParams(BulletSpawnParameters spawnParams)
        {
            if (spawnParams.Pooltype == _normalShootHandle.Pooltype) return _networkedNormalBulletPrefab;
            if (spawnParams.IsMine) return _networkedMinePrefab;
            return _networkedPushBulletPrefab; // Asumimos PushShoot normal si no es mina
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