using System.Collections;
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

        private bool _wrappedRemovalScheduled;
        private List<Collider> _quarantinedColliders;

        protected virtual void Awake()
        {
            Wrapped = GetComponent<T>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

            // FishNet deactivates scene objects before spawn, which stops a restore started in Awake.
            RestoreQuarantinedColliders();

            if (Wrapped != null && !base.IsServerInitialized)
                RemoveWrappedBehaviour();

            OnConfigured();
        }

        /// <summary>Quarantines the colliders for a frame and removes the legacy behaviour; also usable on the server.</summary>
        protected void RemoveWrappedBehaviour()
        {
            if (_wrappedRemovalScheduled || Wrapped == null)
                return;

            _wrappedRemovalScheduled = true;
            _quarantinedColliders = new List<Collider>();
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                _quarantinedColliders.Add(collider);
            }

            Destroy(Wrapped);
            Wrapped = null;

            if (_quarantinedColliders.Count > 0)
                StartCoroutine(RestoreCollidersNextFrame());
        }

        private IEnumerator RestoreCollidersNextFrame()
        {
            yield return null;
            RestoreQuarantinedColliders();
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
