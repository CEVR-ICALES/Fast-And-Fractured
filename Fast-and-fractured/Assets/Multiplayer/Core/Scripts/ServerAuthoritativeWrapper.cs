using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Removes the wrapped singleplayer component on clients so it only runs on the server (disabled behaviours
    /// still get physics messages).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public abstract class ServerAuthoritativeWrapper<T> : NetworkBehaviour where T : MonoBehaviour
    {
        protected T Wrapped { get; private set; }

        protected virtual bool KeepWrappedOnClients => false;

        private bool _wrappedRemovalScheduled;
        private List<Collider> _quarantinedColliders;
        private int _quarantineFrame;

        protected virtual void Awake()
        {
            Wrapped = GetComponent<T>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // FishNet deactivates scene objects before spawn, so a quarantine from Awake may still be pending.
            RestoreQuarantinedColliders();

            if (Wrapped != null && !base.IsServerInitialized && !KeepWrappedOnClients)
                RemoveWrappedBehaviour();

            OnConfigured();
        }

        private void Update()
        {
            if (_quarantinedColliders != null && Time.frameCount > _quarantineFrame)
                RestoreQuarantinedColliders();
        }

        /// <summary>Quarantines the colliders for a frame and removes the legacy behaviour; also usable on the server.</summary>
        protected void RemoveWrappedBehaviour()
        {
            if (_wrappedRemovalScheduled || Wrapped == null)
                return;

            _wrappedRemovalScheduled = true;
            var quarantined = new List<Collider>();
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                quarantined.Add(collider);
            }

            Destroy(Wrapped);
            Wrapped = null;

            if (quarantined.Count == 0)
                return;
            _quarantinedColliders = quarantined;
            _quarantineFrame = Time.frameCount;
        }

        private void RestoreQuarantinedColliders()
        {
            if (_quarantinedColliders == null)
                return;

            foreach (var collider in _quarantinedColliders)
            {
                if (collider != null)
                    collider.enabled = true;
            }
            _quarantinedColliders = null;
        }

        protected virtual void OnConfigured() { }
    }
}
