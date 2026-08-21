using System.Collections;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative wrapper for the Mega Chicken Boss
    /// (ChickenBrain + ChickenKillZone + ChickenPushZone).
    ///
    /// ChickenKillZone.OnTriggerEnter calls statsController.Dead() directly — bypassing
    /// NetworkedStatsController's death pipeline (no kill feed, no spectator transition, no
    /// character-count tracking). ChickenPushZone applies AddForce without FishNet reconcile.
    ///
    /// KILL ZONE:
    ///   This wrapper replaces ChickenKillZone on ALL peers with a relay installed on the exact
    ///   child hitbox. The legacy component is destroyed after its visual references are copied,
    ///   so its StatsController.Dead() callback cannot run even though Unity invokes trigger
    ///   messages on disabled MonoBehaviours in some configurations.
    ///
    /// PUSH ZONE:
    ///   ChickenPushZone and its collider are removed on non-server clients. The server runs it;
    ///   FishNet rigidbody reconcile propagates the resulting impulses to clients.
    ///
    /// ANIMATOR SYNC:
    ///   IsJumping / IsLayingEgg booleans are synced via SyncVars so client chickens
    ///   animate correctly without running the AI locally.
    ///
    /// Setup: add this component and a NetworkObject to the MegaChicken Boss root GameObject.
    /// ChickenKillZone and ChickenPushZone live on child GameObjects. The kill-zone relay is
    /// installed automatically at runtime; no singleplayer prefab or script changes are needed.
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

        public readonly SyncVar<bool> SyncedIsJumping   = new SyncVar<bool>();
        public readonly SyncVar<bool> SyncedIsLayingEgg = new SyncVar<bool>();

        // Mirrored constants from ChickenKillZone (kept in sync manually if the original changes).
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
            _killZone = GetComponentInChildren<ChickenKillZone>(true);
            _pushZone = GetComponentInChildren<ChickenPushZone>(true);

            ReplaceLegacyKillZone();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // Pure clients never apply local push physics. Destroy both the legacy callback and
            // its private hitbox; the server's authoritative impulse is reconciled by FishNet.
            if (!base.IsServerInitialized && _pushZone != null)
                RemoveClientPushZone();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            SyncedIsJumping.OnChange   += OnJumpingChanged;
            SyncedIsLayingEgg.OnChange += OnLayingEggChanged;
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
        }

        private void ReplaceLegacyKillZone()
        {
            if (_killZone == null) return;

            _groundImpactEffect = _killZone.groundImpactEffect;

            _killZoneRelay = _killZone.GetComponent<NetworkedChickenKillZoneRelay>();
            if (_killZoneRelay == null)
                _killZoneRelay = _killZone.gameObject.AddComponent<NetworkedChickenKillZoneRelay>();
            _killZoneRelay.Configure(this);

            // Destroy() is deferred until the end of the frame. Quarantine the exact hitbox
            // during that window so the legacy callback cannot race the relay's installation.
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

            StartCoroutine(RestoreRelayedCollidersNextFrame(colliders, enabledStates));
        }

        private static IEnumerator RestoreRelayedCollidersNextFrame(Collider[] colliders, bool[] enabledStates)
        {
            yield return null;

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = enabledStates[i];
            }
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

            // Activate push zone when chicken lands (matches ChickenKillZone ground-hit logic).
            if (other.gameObject.layer == LayerMask.NameToLayer(GROUND_LAYER) && _brain.IsJumping)
            {
                if (_groundImpactCooldown > 0f) return;

                _groundImpactCooldown = GROUND_IMPACT_COOLDOWN;
                RpcPlayGroundImpact();
                if (_pushZone != null) _pushZone.ActivatePushZone();
                return;
            }

            // Kill a vehicle when the jump or egg-laying hitbox connects.
            if (!_brain.IsJumping && !_brain.IsLayingEgg) return;

            var stats = other.GetComponentInParent<NetworkedStatsController>();
            if (stats == null) stats = other.GetComponentInChildren<NetworkedStatsController>();
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

        // ── Animator sync ────────────────────────────────────────────────────────
        private void OnJumpingChanged(bool prev, bool next, bool asServer)
        {
            if (asServer || _brain?.Animator == null) return;
            _brain.Animator.SetBool("IsJumping", next);
        }

        private void OnLayingEggChanged(bool prev, bool next, bool asServer)
        {
            if (asServer || _brain?.Animator == null) return;
            _brain.Animator.SetBool("IsLayingEgg", next);
        }
    }
}
