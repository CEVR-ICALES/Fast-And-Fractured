// RUTA: Assets/FastAndFractured/Multiplayer/NetworkedProjectile.cs

using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using UnityEngine;
using FastAndFractured.Utilities;
using FishNet;

namespace FastAndFractured.Multiplayer
{
    [RequireComponent(typeof(ICustomRigidbody), typeof(Collider))]
    public class NetworkedProjectile : NetworkBehaviour
    {
        // ----------------- DATOS DE PREDICCIÓN -----------------
        #region PREDICTION_DATA_STRUCTS
        public struct MoveData : IReplicateData
        {
            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }

        public struct ReconcileData : IReconcileData
        {
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
            public int Bounces;
            public bool IsActive;

            public ReconcileData(Vector3 pos, Quaternion rot, Vector3 vel, Vector3 angVel, int bounces, bool isActive)
            {
                Position = pos;
                Rotation = rot;
                Velocity = vel;
                AngularVelocity = angVel;
                Bounces = bounces;
                IsActive = isActive;
                _tick = 0;
            }

            private uint _tick;
            public void Dispose() { }
            public uint GetTick() => _tick;
            public void SetTick(uint value) => _tick = value;
        }
        #endregion

        #region REFERENCES_AND_STATE
        private ICustomRigidbody _customRigidbody;
        private Collider _ownCollider;

        [Header("Efectos")]
        [SerializeField] private GameObject _visuals;
        [SerializeField] private GameObject _impactParticlesPrefab;
        [SerializeField] private GameObject _explosionPrefab;

        private uint _despawnTick;

        private bool _isInitialized = false;
        private float _damage;
        private LayerMask _collisionMask;
        private Collider _ignoredCollider;

        private float _pushForce;
        private float _explosionRadius;
        private float _bounceStrength;
        private int _maxBounces;
        private int _currentBounces;
        private Vector3 _customGravity;
        private bool _useCustomGravity;
        #endregion

        // ... (El Awake, OnStartNetwork y OnStopNetwork no cambian) ...
        #region FISHNET_LIFECYCLE
        private void Awake()
        {
            _customRigidbody = GetComponent<ICustomRigidbody>();
            _ownCollider = GetComponent<Collider>();
            if (_customRigidbody is MultiplayerRigidbodyAdapter adapter)
            {
                adapter.InitializeAdapter();
            }
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            base.TimeManager.OnTick += TimeManager_OnTick;
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            if (base.TimeManager != null)
                base.TimeManager.OnTick -= TimeManager_OnTick;
        }

        // ---------- LA CORRECCIÓN ESTÁ AQUÍ ----------

        [ObserversRpc]
        public void RpcInitialize(NetworkShootParams data, NetworkObject ignorer)
        {
            if (_isInitialized) return;
            _isInitialized = true;

            gameObject.name = $"{gameObject.name}_Owner({(ignorer != null ? ignorer.OwnerId : -1)})_Tick({base.TimeManager.Tick})";

            _customRigidbody.linearVelocity = data.InitialVelocity;
            _damage = data.Damage;
            float lifetime = (data.InitialVelocity.magnitude > 0.1f) ? data.Range / data.InitialVelocity.magnitude : 5f;

            _collisionMask = LayerMask.GetMask("Default", "Characters", "Ground", "Obstacles");
            _customGravity = data.CustomGravity;
            _useCustomGravity = (data.BouncingNum > 0 || data.IsMine);

            if (ignorer != null && ignorer.TryGetComponent(out Collider col))
            {
                _ignoredCollider = col;
                if (_ownCollider != null) // El collider podría no estar listo aún en Awake en clientes remotos
                    Physics.IgnoreCollision(_ownCollider, _ignoredCollider, true);
            }
            _pushForce = data.PushForce;
            _explosionRadius = data.ExplosionRadius;
            _bounceStrength = data.BouncingStrenght;
            _maxBounces = data.BouncingNum;

            _despawnTick = base.TimeManager.Tick + base.TimeManager.TimeToTicks(lifetime);
        }

        // ---------- FIN DE LA CORRECCIÓN PRINCIPAL ----------

        private void TimeManager_OnTick()
        {
            if (base.IsOwner)
            {
                Replicate(default, ReplicateState.Ticked, Channel.Unreliable);
            }
            if (base.IsServer)
            {
                Replicate(default, ReplicateState.Ticked, Channel.Unreliable);
            }
        }

        public override void CreateReconcile()
        {
            if (!base.IsServer) return;

            var rd = new ReconcileData(
                _customRigidbody.position,
                _customRigidbody.rotation,
                _customRigidbody.linearVelocity,
                _customRigidbody.angularVelocity,
                _currentBounces,
                gameObject.activeSelf
            );

            // Esta llamada es correcta. El 'true' implícito se maneja por la sobrescritura y el contexto.
            // Para el dueño del objeto, el base.Reconcile se encarga de enviarlo.
            if (base.Owner.IsValid)
                Reconcile(rd, Channel.Reliable);
        }
        #endregion

        // ... (El resto de la lógica de SIMULATION_LOGIC: Replicate, Reconcile, HandleCollisions, HandleImpact no necesita cambios) ...
        #region SIMULATION_LOGIC
        [Replicate]
        private void Replicate(MoveData md, ReplicateState state, Channel channel)
        {
            if (!_isInitialized) return;

            bool replaying = (state == ReplicateState.Replayed);

            if (_useCustomGravity)
            {
                _customRigidbody.linearVelocity += _customGravity * (float)base.TimeManager.TickDelta;
            }

            _customRigidbody.Simulate();

            if (!HandleCollisions(replaying))
            {
                if (base.IsServer && base.TimeManager.Tick >= _despawnTick)
                {
                    HandleImpact(transform.position, Vector3.up, null, replaying);
                }
            }
        }

        [Reconcile]
        private void Reconcile(ReconcileData rd,  Channel channel)
        {
            if (!rd.IsActive)
            {
                gameObject.SetActive(false);
                return;
            }

            _customRigidbody.position = rd.Position;
            _customRigidbody.rotation = rd.Rotation;
            _customRigidbody.linearVelocity = rd.Velocity;
            _customRigidbody.angularVelocity = rd.AngularVelocity;
            _currentBounces = rd.Bounces;
        }

        private bool HandleCollisions(bool replaying)
        {
            float tickDelta = (float)base.TimeManager.TickDelta;
            float distance = _customRigidbody.linearVelocity.magnitude * tickDelta;
            if (distance < 0.01f) return false;

            if (Physics.SphereCast(transform.position, 0.15f, _customRigidbody.linearVelocity.normalized, out RaycastHit hit, distance, _collisionMask))
            {
                if (_ignoredCollider != null && (hit.collider == _ignoredCollider || hit.collider.transform.IsChildOf(_ignoredCollider.transform)))
                    return false;

                if (_currentBounces < _maxBounces)
                {
                    _currentBounces++;
                    Vector3 reflection = Vector3.Reflect(_customRigidbody.linearVelocity, hit.normal);
                    float loss = _bounceStrength > 0 ? _bounceStrength : 0.8f;
                    _customRigidbody.linearVelocity = reflection * loss;
                    _customRigidbody.position = hit.point + hit.normal * 0.15f;
                }
                else
                {
                    HandleImpact(hit.point, hit.normal, hit.collider.gameObject, replaying);
                }
                return true;
            }
            return false;
        }

        private void HandleImpact(Vector3 point, Vector3 normal, GameObject hitObject, bool replaying)
        {
            if (!replaying)
            {
                if (_visuals != null) _visuals.SetActive(false);
                _ownCollider.enabled = false;
                if (_customRigidbody != null)
                {
                    _customRigidbody.isKinematic = true;
                    _customRigidbody.linearVelocity = Vector3.zero;
                }

                if (_explosionRadius > 0f)
                {
                    if (_explosionPrefab != null) Instantiate(_explosionPrefab, point, Quaternion.LookRotation(normal));
                }
                else
                {
                    if (_impactParticlesPrefab != null) Instantiate(_impactParticlesPrefab, point, Quaternion.LookRotation(normal));
                }
            }

            if (base.IsServer)
            {
                if (_explosionRadius <= 0f && hitObject != null && hitObject.TryGetComponent<StatsController>(out var stats))
                {
                    if (base.Owner.IsValid)
                        stats.TakeEndurance(_damage, false, base.Owner.FirstObject.gameObject);
                }

                InstanceFinder.ServerManager.Despawn(base.NetworkObject, DespawnType.Destroy);
            }
        }
        #endregion
    }
} 