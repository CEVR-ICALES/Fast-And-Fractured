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

        protected virtual void Awake()
        {
            Wrapped = GetComponent<T>();
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();

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
            var collidersToRestore = new List<Collider>();
            foreach (var collider in GetComponents<Collider>())
            {
                if (collider == null || !collider.enabled)
                    continue;

                collider.enabled = false;
                collidersToRestore.Add(collider);
            }

            Destroy(Wrapped);
            Wrapped = null;

            if (collidersToRestore.Count > 0)
                StartCoroutine(RestoreCollidersNextFrame(collidersToRestore));
        }

        private static IEnumerator RestoreCollidersNextFrame(List<Collider> colliders)
        {
            yield return null;

            foreach (var collider in colliders)
            {
                if (collider != null)
                    collider.enabled = true;
            }
        }

        protected virtual void OnConfigured() { }
    }
}
