using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative Mega Chicken boss. ChickenKillZone (which calls Dead() directly) is replaced on every
    /// peer by a relay on its hitbox; the push zone and the AI only run on the server, and clients mirror the
    /// animation and the eggs.
    /// </summary>
    [RequireComponent(typeof(ChickenBrain))]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedMegaChickenBoss : NetworkBehaviour
    {
        private ChickenBrain    _brain;
        private ChickenKillZone _killZone;
        private ChickenPushZone _pushZone;
        private NetworkedChickenKillZoneRelay _killZoneRelay;
        private ParticleSystem _groundImpactEffect;
        private Animator _animator;

        public readonly SyncVar<bool> SyncedIsJumping   = new SyncVar<bool>();
        public readonly SyncVar<bool> SyncedIsLayingEgg = new SyncVar<bool>();

        // Mirrored from ChickenKillZone.
        private const float ANIMATION_JUMP_HIT_TIME        = 0.5f;
        private const float ANIMATION_LAYING_EGG_KILL_TIME = 0.4f;
        private const string ANIM_JUMP       = "Jump";
        private const string ANIM_LAYING_EGG = "LayEgg";
        private const string GROUND_LAYER    = "Ground";
        private const float GROUND_IMPACT_COOLDOWN = 2f;

        private float _animSyncTimer;
        private float _groundImpactCooldown;
        private const float ANIM_SYNC_INTERVAL = 0.05f; // 20 Hz

        private void Awake()
        {
            _brain    = GetComponent<ChickenBrain>();
            _animator = GetComponent<Animator>();
            _killZone = GetComponentInChildren<ChickenKillZone>(true);
            _pushZone = GetComponentInChildren<ChickenPushZone>(true);

            ReplaceLegacyKillZone();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            RestoreRelayedColliders();

            // Pure clients apply no push: the server's impulse reaches them through reconcile.
            if (!base.IsServerInitialized && _pushZone != null)
                RemoveClientPushZone();

            // The AI only runs on the server: on clients it laid local eggs that pushed cars the server never saw.
            if (!base.IsServerInitialized)
                DisableClientAi();
        }

        private void DisableClientAi()
        {
            if (_brain != null) _brain.enabled = false;
            var stateMachine = GetComponent<StateMachine.Controller>();
            if (stateMachine != null) stateMachine.enabled = false;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SyncedIsJumping.OnChange   += OnJumpingChanged;
            SyncedIsLayingEgg.OnChange += OnLayingEggChanged;

            if (!base.IsServerInitialized && _animator != null)
            {
                _animator.SetBool("IsJumping", SyncedIsJumping.Value);
                _animator.SetBool("IsLayingEgg", SyncedIsLayingEgg.Value);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            SyncedIsJumping.OnChange   -= OnJumpingChanged;
            SyncedIsLayingEgg.OnChange -= OnLayingEggChanged;
        }

        private void Update()
        {
            if (!base.IsServerInitialized || _brain == null) return;

            if (_groundImpactCooldown > 0f)
                _groundImpactCooldown = Mathf.Max(0f, _groundImpactCooldown - Time.deltaTime);

            _animSyncTimer += Time.deltaTime;
            if (_animSyncTimer >= ANIM_SYNC_INTERVAL)
            {
                _animSyncTimer = 0f;
                SyncedIsJumping.Value   = _brain.IsJumping;
                SyncedIsLayingEgg.Value = _brain.IsLayingEgg;
            }

            _eggSyncTimer += Time.deltaTime;
            if (_eggSyncTimer >= EGG_SYNC_INTERVAL)
            {
                _eggSyncTimer = 0f;
                ServerSendEggs();
            }
        }

        // Eggs: only the server's chicken lays them. Both peers build the same pool in the same order, so the
        // server sends each egg's state by pool index and the client moves a logic-less copy.
        private const float EGG_SYNC_INTERVAL = 0.1f;
        private const float EGG_FOLLOW_SHARPNESS = 12f;
        private const float MIN_ROLL_SQR_DISTANCE = 1e-8f;
        private const byte EGG_OFF = 0, EGG_ROLLING = 1, EGG_EXPLODING = 2;

        private float _eggSyncTimer;
        private bool _eggsWereActive;
        private List<GameObject> _eggPool;

        private sealed class EggMirror
        {
            public GameObject Root;
            public Transform Model;
            public GameObject Explosion;
            public float Radius = 0.68f;
            public Vector3 Target;
            public bool HasTarget;
        }
        private EggMirror[] _eggMirrors;

        private List<GameObject> EggPool()
        {
            if (_eggPool != null) return _eggPool;
            if (_brain == null || !global::Utilities.ObjectPoolManager.HasInstance) return null;
            _eggPool = global::Utilities.ObjectPoolManager.Instance.GiveAllMyPooledObjects(_brain.pooltypeMegaChickenEgg);
            return _eggPool;
        }

        [Server]
        private void ServerSendEggs()
        {
            var pool = EggPool();
            if (pool == null || pool.Count == 0) return;

            bool anyActive = false;
            var states = new byte[pool.Count];
            var positions = new Vector3[pool.Count];
            for (int i = 0; i < pool.Count; i++)
            {
                var egg = pool[i];
                if (egg == null || !egg.activeInHierarchy) continue;
                var logic = egg.GetComponent<MegaChickenEgg>();
                bool rolling = logic == null || logic.visualModel == null || logic.visualModel.gameObject.activeSelf;
                states[i] = rolling ? EGG_ROLLING : EGG_EXPLODING;
                positions[i] = egg.transform.position;
                anyActive = true;
            }

            // While eggs are out, and once more when the last one is gone.
            if (!anyActive && !_eggsWereActive) return;
            _eggsWereActive = anyActive;
            RpcEggs(states, positions);
        }

        [ObserversRpc]
        private void RpcEggs(byte[] states, Vector3[] positions)
        {
            if (base.IsServerInitialized) return;
            var pool = EggPool();
            if (pool == null) return;
            if (_eggMirrors == null) _eggMirrors = new EggMirror[pool.Count];

            for (int i = 0; i < pool.Count && i < states.Length; i++)
            {
                var mirror = _eggMirrors[i] ??= MakeEggMirror(pool[i]);
                if (mirror == null) continue;

                if (states[i] == EGG_OFF)
                {
                    if (mirror.Root.activeSelf) mirror.Root.SetActive(false);
                    mirror.HasTarget = false;
                    continue;
                }

                if (!mirror.Root.activeSelf || !mirror.HasTarget)
                {
                    mirror.Root.transform.position = positions[i];
                    mirror.Root.SetActive(true);
                }
                mirror.Target = positions[i];
                mirror.HasTarget = true;
                bool rolling = states[i] == EGG_ROLLING;
                if (mirror.Model != null) mirror.Model.gameObject.SetActive(rolling);
                if (mirror.Explosion != null) mirror.Explosion.SetActive(!rolling);
            }
        }

        /// <summary>Client: turns a pooled egg into a moving picture of the server's one.</summary>
        private static EggMirror MakeEggMirror(GameObject egg)
        {
            if (egg == null) return null;
            egg.SetActive(false);
            var mirror = new EggMirror { Root = egg };
            var logic = egg.GetComponent<MegaChickenEgg>();
            if (logic != null)
            {
                mirror.Model = logic.visualModel;
                mirror.Explosion = logic.explosionParticles;
                Destroy(logic);
            }
            var sphere = egg.GetComponent<SphereCollider>();
            if (sphere != null)
                mirror.Radius = sphere.radius * Mathf.Max(egg.transform.lossyScale.x, egg.transform.lossyScale.y, egg.transform.lossyScale.z);
            var agent = egg.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) Destroy(agent);
            foreach (var collider in egg.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            var body = egg.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = true;
            return mirror;
        }

        private void LateUpdate()
        {
            if (_eggMirrors == null) return;
            float blend = 1f - Mathf.Exp(-EGG_FOLLOW_SHARPNESS * Time.deltaTime);
            foreach (var mirror in _eggMirrors)
            {
                if (mirror == null || !mirror.HasTarget || mirror.Root == null || !mirror.Root.activeSelf) continue;
                Transform egg = mirror.Root.transform;
                Vector3 before = egg.position;
                egg.position = Vector3.Lerp(before, mirror.Target, blend);

                Vector3 movement = egg.position - before;
                movement.y = 0f;
                if (mirror.Model != null && movement.sqrMagnitude > MIN_ROLL_SQR_DISTANCE && mirror.Radius > 0f)
                    mirror.Model.Rotate(Vector3.Cross(Vector3.up, movement.normalized),
                                        movement.magnitude / mirror.Radius * Mathf.Rad2Deg, Space.World);
            }
        }

        private void ReplaceLegacyKillZone()
        {
            if (_killZone == null) return;

            _groundImpactEffect = _killZone.groundImpactEffect;

            _killZoneRelay = _killZone.GetComponent<NetworkedChickenKillZoneRelay>();
            if (_killZoneRelay == null)
                _killZoneRelay = _killZone.gameObject.AddComponent<NetworkedChickenKillZoneRelay>();
            _killZoneRelay.Configure(this);

            // Destroy() is deferred: quarantine the hitbox so the legacy callback cannot race the relay.
            Collider[] colliders = _killZone.GetComponents<Collider>();
            bool[] enabledStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                enabledStates[i] = colliders[i] != null && colliders[i].enabled;
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }

            _killZone.enabled = false;
            PauseManager.TryUnregisterPausable(_killZone);
            Destroy(_killZone);
            _killZone = null;

            _quarantined = colliders;
            _quarantinedStates = enabledStates;
            StartCoroutine(RestoreRelayedCollidersNextFrame());
        }

        // The kill zone's colliders while the legacy component is being destroyed.
        private Collider[] _quarantined;
        private bool[] _quarantinedStates;

        private IEnumerator RestoreRelayedCollidersNextFrame()
        {
            yield return null;
            RestoreRelayedColliders();
        }

        /// <summary>Also run from OnStartNetwork: FishNet deactivates scene objects before spawn, which stops the coroutine.</summary>
        private void RestoreRelayedColliders()
        {
            if (_quarantined == null) return;
            for (int i = 0; i < _quarantined.Length; i++)
            {
                if (_quarantined[i] != null)
                    _quarantined[i].enabled = _quarantinedStates[i];
            }
            _quarantined = null;
            _quarantinedStates = null;
        }

        private void RemoveClientPushZone()
        {
            Collider[] pushColliders = _pushZone.GetComponents<Collider>();
            for (int i = 0; i < pushColliders.Length; i++)
            {
                if (pushColliders[i] != null)
                    pushColliders[i].enabled = false;
            }

            _pushZone.enabled = false;
            Destroy(_pushZone);
            _pushZone = null;
        }

        // Called only by NetworkedChickenKillZoneRelay on the exact legacy kill hitbox.
        internal void HandleKillZoneTrigger(Collider other)
        {
            if (!base.IsServerInitialized || _brain == null) return;

            if (other.gameObject.layer == LayerMask.NameToLayer(GROUND_LAYER) && _brain.IsJumping)
            {
                if (_groundImpactCooldown > 0f) return;

                _groundImpactCooldown = GROUND_IMPACT_COOLDOWN;
                RpcPlayGroundImpact();
                if (_pushZone != null) _pushZone.ActivatePushZone();
                return;
            }

            if (!_brain.IsJumping && !_brain.IsLayingEgg) return;

            // Same filter as ChickenKillZone: only the car body collider.
            if (!other.TryGetComponent(out StatsController _)) return;

            var stats = other.GetComponentInParent<NetworkedStatsController>();
            if (stats == null) return;

            AnimatorStateInfo state = _brain.Animator.GetCurrentAnimatorStateInfo(0);

            bool jumpKill = _brain.IsJumping &&
                            state.IsName(ANIM_JUMP) &&
                            state.normalizedTime >= ANIMATION_JUMP_HIT_TIME;

            bool eggKill  = _brain.IsLayingEgg &&
                            state.IsName(ANIM_LAYING_EGG) &&
                            state.normalizedTime <= ANIMATION_LAYING_EGG_KILL_TIME;

            if (jumpKill || eggKill)
                stats.KillFromEnvironmentServer();
        }

        [ObserversRpc]
        private void RpcPlayGroundImpact()
        {
            if (_groundImpactEffect != null)
                _groundImpactEffect.Play();
        }

        private void OnJumpingChanged(bool prev, bool next, bool asServer)
        {
            if (asServer || _animator == null) return;
            _animator.SetBool("IsJumping", next);
        }

        private void OnLayingEggChanged(bool prev, bool next, bool asServer)
        {
            if (asServer || _animator == null) return;
            _animator.SetBool("IsLayingEgg", next);
        }
    }
}
