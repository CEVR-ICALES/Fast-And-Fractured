using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Networks the scene's InteractableHandler: the server syncs its pickup pool to clients by raw index.</summary>
    [DefaultExecutionOrder(-400)] // Awake self-heal must run before InteractableHandler.Awake (Construct iterates the array)
    public class NetworkedInteractableHandler : NetworkBehaviour
    {
        private static readonly BindingFlags ReflectionFlags = BindingFlags.NonPublic | BindingFlags.Instance;

        private static FieldInfo _interactablesField;

        private void Awake()
        {
            HealBrokenInteractablesArray();
        }

        /// <summary>
        /// Rebuilds interactablesToToggle from the scene's GenericInteractables: the migration's prefab swaps leave
        /// it full of nulls, and Construct then throws.
        /// </summary>
        private void HealBrokenInteractablesArray()
        {
            var handler = FindFirstObjectByType<InteractableHandler>(FindObjectsInactive.Include);
            if (handler == null) return;

            _interactablesField ??= typeof(InteractableHandler).GetField("interactablesToToggle", ReflectionFlags);
            if (_interactablesField == null) return;

            var current = _interactablesField.GetValue(handler) as GameObject[];
            bool broken = current == null || current.Length == 0;
            if (!broken)
            {
                foreach (var go in current)
                    if (go == null) { broken = true; break; }
            }
            if (!broken) return;

            var rebuilt = new List<GameObject>();
            if (current != null)
            {
                foreach (var go in current)
                    if (go != null && !rebuilt.Contains(go)) rebuilt.Add(go);
            }

            // Sorted by hierarchy path: the pool is synced by index, so every peer needs the same order.
            var found = new List<GameObject>();
            foreach (var interactable in FindObjectsByType<GenericInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (interactable.gameObject.scene != handler.gameObject.scene) continue;
                if (!rebuilt.Contains(interactable.gameObject) && !found.Contains(interactable.gameObject))
                    found.Add(interactable.gameObject);
            }
            found.Sort((a, b) => string.CompareOrdinal(HierarchyPath(a.transform), HierarchyPath(b.transform)));
            rebuilt.AddRange(found);

            _interactablesField.SetValue(handler, rebuilt.ToArray());
            Debug.LogWarning($"[NetworkedInteractableHandler] interactablesToToggle had missing entries " +
                             $"(scene-migration prefab swaps). Rebuilt with {rebuilt.Count} interactables found in scene.", this);
        }

        /// <summary>"Root/Child/Grandchild", with sibling indices so same-named siblings stay distinct.</summary>
        private static string HierarchyPath(Transform t)
        {
            var path = new System.Text.StringBuilder();
            for (; t != null; t = t.parent)
                path.Insert(0, $"/{t.GetSiblingIndex():D4}{t.name}");
            return path.ToString();
        }

        private InteractableHandler _interactableHandler;
        private InteractableHandler _clientCachedHandler;
        private int[] _pendingActiveIndices;

        // Client: the pickups the server has in play, by raw index. Null until the first sync.
        private bool[] _serverActive;
        private const float CLIENT_ENFORCE_INTERVAL = 0.2f;
        private const float APPLY_RETRY_TIMEOUT = 5f;
        private const float APPLY_RETRY_INTERVAL = 0.5f;
        private const float INITIAL_SYNC_TIMEOUT = 6f;
        private const float INITIAL_SYNC_INTERVAL = 0.2f;
        private const float POOL_WATCH_INTERVAL = 0.25f;
        private const float RESTORE_POOL_TIMEOUT = 10f;
        private const int HASH_SEED = 17;
        private const int HASH_MULTIPLIER = 31;

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (!IsServerInitialized) StartCoroutine(ClientEnforceServerState());
        }

        /// <summary>
        /// Pure client: keeps the pickups as the server has them. FishNet activates scene objects on spawn and the
        /// singleplayer handler still toggles items here, so the local state drifts between syncs.
        /// </summary>
        private IEnumerator ClientEnforceServerState()
        {
            var wait = new WaitForSeconds(CLIENT_ENFORCE_INTERVAL);
            while (true)
            {
                yield return wait;
                if (_serverActive == null) continue;
                var rawItems = GetRawInteractables(GetClientHandler());
                if (rawItems == null) continue;
                int fixedCount = 0;
                for (int i = 0; i < rawItems.Length && i < _serverActive.Length; i++)
                {
                    var item = rawItems[i];
                    if (item == null || item.activeSelf == _serverActive[i]) continue;
                    item.SetActive(_serverActive[i]);
                    fixedCount++;
                }
                if (fixedCount > 0) MpCanary.Mark($"Pickups: {fixedCount} put back to the server's state");
            }
        }

        private void RememberServerState(int length, int[] activeRawIndices)
        {
            _serverActive = new bool[length];
            if (activeRawIndices == null) return;
            foreach (int index in activeRawIndices)
                if (index >= 0 && index < length) _serverActive[index] = true;
        }

        private InteractableHandler GetClientHandler()
        {
            if (_clientCachedHandler == null)
                _clientCachedHandler = FindFirstObjectByType<InteractableHandler>(FindObjectsInactive.Include);
            return _clientCachedHandler;
        }
        private readonly List<GenericInteractable> _subscribedInteractables = new();
        private readonly Dictionary<int, int> _lastProcessedPickupFrameByInstanceId = new();
        private bool _pendingDeferredPoolSync;
        private int _lastActiveStateHash = int.MinValue;
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            
            _interactableHandler = FindFirstObjectByType<InteractableHandler>(FindObjectsInactive.Include);
            
            if (_interactableHandler != null)
            {
                _interactableHandler.onPoolInitialize.AddListener(OnPoolInitialized);
                SubscribeToInteractableEvents();
                ForceInitialPoolSync();
                StartCoroutine(ServerPoolStateMonitor());
            }
            else
            {
                Debug.LogWarning("[NetworkedInteractableHandler] InteractableHandler not found in scene. Interactables may not sync.");
            }
        }
        
        /// <summary>Server: syncs the active pickups by raw index (not the shuffled pool), so clients need no shuffle.</summary>
        [Server]
        private void OnPoolInitialized()
        {
            SubscribeToInteractableEvents();
            SyncPoolToAllClients();
        }
        
        [Server]
        public void OnInteractablePickedUp(GameObject interactable, NetworkConnection picker)
        {
            if (_interactableHandler == null) return;

            var rawItems = GetRawInteractables(_interactableHandler);
            if (rawItems == null) return;

            int rawIndex = System.Array.IndexOf(rawItems, interactable);
            if (rawIndex >= 0)
            {
                int pickerId = -1;
                if (picker != null && picker.IsValid)
                {
                    pickerId = picker.ClientId;
                }

                SyncInteractablePickupRpc(rawIndex, pickerId);
            }
        }
        
        [ObserversRpc(BufferLast = true)]
        private void SyncInteractablePoolRpc(int[] activeRawIndices)
        {
            if (IsServerInitialized)
                return;

            var handler = GetClientHandler();
            if (handler == null)
            {
                Debug.LogWarning("[NetworkedInteractableHandler] Client: InteractableHandler not found");
                return;
            }
            
            var rawItems = GetRawInteractables(handler);
            if (rawItems == null || rawItems.Length == 0)
            {
                _pendingActiveIndices = activeRawIndices;
                StartCoroutine(RetryApplyInteractablePool());
                return;
            }
            
            ApplyFromRawIndices(rawItems, activeRawIndices);
        }
        
        private IEnumerator RetryApplyInteractablePool()
        {
            float timeout = APPLY_RETRY_TIMEOUT;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                yield return new WaitForSeconds(APPLY_RETRY_INTERVAL);
                elapsed += APPLY_RETRY_INTERVAL;
                
                var handler = GetClientHandler();
                if (handler == null) continue;

                var rawItems = GetRawInteractables(handler);
                if (rawItems != null && rawItems.Length > 0)
                {
                    ApplyFromRawIndices(rawItems, _pendingActiveIndices);
                    _pendingActiveIndices = null;
                    yield break;
                }
            }
            
            Debug.LogWarning("[NetworkedInteractableHandler] Client: Failed to sync interactable pool");
        }
        
        private void ApplyFromRawIndices(GameObject[] rawItems, int[] activeRawIndices)
        {
            RememberServerState(rawItems.Length, activeRawIndices);
            foreach (var item in rawItems)
            {
                if (item != null)
                    item.SetActive(false);
            }
            
            foreach (int index in activeRawIndices)
            {
                if (index >= 0 && index < rawItems.Length && rawItems[index] != null)
                {
                    rawItems[index].SetActive(true);
                }
            }
        }
        
        [ObserversRpc]
        private void SyncInteractablePickupRpc(int rawIndex, int pickerId)
        {
            if (IsServerInitialized)
                return;

            var handler = GetClientHandler();
            if (handler == null) return;

            var rawItems = GetRawInteractables(handler);
            if (rawItems == null || rawIndex < 0 || rawIndex >= rawItems.Length) return;
            
            if (rawItems[rawIndex] != null)
            {
                rawItems[rawIndex].SetActive(false);
            }
            if (_serverActive != null && rawIndex < _serverActive.Length)
                _serverActive[rawIndex] = false;
        }

        [Server]
        private void ForceInitialPoolSync()
        {
            if (_interactableHandler == null) return;
            StartCoroutine(ForceInitialPoolSyncRoutine());
        }

        [Server]
        private IEnumerator ForceInitialPoolSyncRoutine()
        {
            float timeout = INITIAL_SYNC_TIMEOUT;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                var rawItems = GetRawInteractables(_interactableHandler);
                if (rawItems != null && rawItems.Length > 0)
                {
                    SyncPoolToAllClients();
                    yield break;
                }

                elapsed += INITIAL_SYNC_INTERVAL;
                yield return new WaitForSeconds(INITIAL_SYNC_INTERVAL);
            }

            Debug.LogWarning("[NetworkedInteractableHandler] Server: Could not force initial pool sync (raw interactables unavailable).");
        }

        [Server]
        private void SyncPoolToAllClients()
        {
            if (_interactableHandler == null) return;

            var rawItems = GetRawInteractables(_interactableHandler);
            if (rawItems == null) return;

            _lastActiveStateHash = ComputeActiveStateHash(rawItems);

            var activeRawIndices = new List<int>(rawItems.Length);
            for (int i = 0; i < rawItems.Length; i++)
            {
                if (rawItems[i] != null && rawItems[i].activeSelf)
                {
                    activeRawIndices.Add(i);
                }
            }

            SyncInteractablePoolRpc(activeRawIndices.ToArray());
        }

        [Server]
        private void SubscribeToInteractableEvents()
        {
            if (_interactableHandler == null) return;

            var rawItems = GetRawInteractables(_interactableHandler);
            if (rawItems == null) return;

            foreach (var item in rawItems)
            {
                if (item == null) continue;
                var interactable = item.GetComponentInParent<GenericInteractable>();
                if (interactable == null) continue;
                if (_subscribedInteractables.Contains(interactable)) continue;

                interactable.onInteract.AddListener(OnServerInteractableUsed);
                _subscribedInteractables.Add(interactable);
            }
        }

        [Server]
        private void UnsubscribeFromInteractableEvents()
        {
            foreach (var interactable in _subscribedInteractables)
            {
                if (interactable == null) continue;
                interactable.onInteract.RemoveListener(OnServerInteractableUsed);
            }
            _subscribedInteractables.Clear();
        }

        [Server]
        private void OnServerInteractableUsed(GameObject interactionFrom, GameObject interactionTo)
        {
            if (_interactableHandler == null) return;

            if (!TryResolveInteractableAndPicker(interactionFrom, interactionTo, out GameObject interactableObject, out NetworkConnection pickerConnection))
            {
                if (!_pendingDeferredPoolSync)
                {
                    StartCoroutine(DeferredPoolSyncNextFrame());
                }
                return;
            }

            int interactableId = interactableObject.GetInstanceID();
            if (_lastProcessedPickupFrameByInstanceId.TryGetValue(interactableId, out int lastFrame) && lastFrame == Time.frameCount)
            {
                return;
            }
            _lastProcessedPickupFrameByInstanceId[interactableId] = Time.frameCount;

            OnInteractablePickedUp(interactableObject, pickerConnection);

            if (!_pendingDeferredPoolSync)
            {
                StartCoroutine(DeferredPoolSyncNextFrame());
            }
        }

        [Server]
        private IEnumerator DeferredPoolSyncNextFrame()
        {
            _pendingDeferredPoolSync = true;
            yield return null;
            _pendingDeferredPoolSync = false;

            SyncPoolToAllClients();
        }

        [Server]
        private IEnumerator ServerPoolStateMonitor()
        {
            while (IsServerInitialized)
            {
                if (_interactableHandler != null)
                {
                    var rawItems = GetRawInteractables(_interactableHandler);
                    if (rawItems != null && rawItems.Length > 0)
                    {
                        int currentHash = ComputeActiveStateHash(rawItems);
                        if (currentHash != _lastActiveStateHash)
                        {
                            SyncPoolToAllClients();
                        }
                    }
                }

                yield return new WaitForSeconds(POOL_WATCH_INTERVAL);
            }
        }

        private static int ComputeActiveStateHash(GameObject[] rawItems)
        {
            if (rawItems == null) return 0;

            unchecked
            {
                int hash = HASH_SEED;
                hash = hash * HASH_MULTIPLIER + rawItems.Length;
                for (int i = 0; i < rawItems.Length; i++)
                {
                    int value = (rawItems[i] != null && rawItems[i].activeSelf) ? (i + 1) : 0;
                    hash = hash * HASH_MULTIPLIER + value;
                }
                return hash;
            }
        }

        [Server]
        private bool TryResolveInteractableAndPicker(GameObject interactionFrom, GameObject interactionTo, out GameObject interactableObject, out NetworkConnection pickerConnection)
        {
            interactableObject = null;
            pickerConnection = null;

            var rawItems = GetRawInteractables(_interactableHandler);
            if (rawItems == null || rawItems.Length == 0) return false;

            interactableObject = ResolveRawInteractable(rawItems, interactionFrom) ?? ResolveRawInteractable(rawItems, interactionTo);
            if (interactableObject == null) return false;

            GameObject pickerCandidate = ReferenceEquals(interactableObject, interactionFrom) ? interactionTo : interactionFrom;
            if (pickerCandidate != null)
            {
                var pickerNob = pickerCandidate.GetComponentInParent<FishNet.Object.NetworkObject>();
                if (pickerNob != null && pickerNob.Owner != null && pickerNob.Owner.IsValid)
                {
                    pickerConnection = pickerNob.Owner;
                }
            }

            return true;
        }

        private static GameObject ResolveRawInteractable(GameObject[] rawItems, GameObject candidate)
        {
            if (rawItems == null || candidate == null) return null;

            int directIndex = System.Array.IndexOf(rawItems, candidate);
            if (directIndex >= 0) return rawItems[directIndex];

            Transform candidateTransform = candidate.transform;
            for (int i = 0; i < rawItems.Length; i++)
            {
                var rawItem = rawItems[i];
                if (rawItem == null) continue;

                Transform rawTransform = rawItem.transform;
                if (candidateTransform == rawTransform || candidateTransform.IsChildOf(rawTransform) || rawTransform.IsChildOf(candidateTransform))
                {
                    return rawItem;
                }
            }

            return null;
        }

        private static GameObject[] GetRawInteractables(InteractableHandler handler)
        {
            if (handler == null) return null;
            _interactablesField ??= typeof(InteractableHandler).GetField("interactablesToToggle", ReflectionFlags);
            return _interactablesField?.GetValue(handler) as GameObject[];
        }
        
        private static FieldInfo _cooldownField;
        private static MethodInfo _updateVisibleMethod;

        /// <summary>Server: the pickups still in play, in pool order, those on their post-pickup cooldown last.</summary>
        [Server]
        public int[] ServerDescribePool()
        {
            var raw = GetRawInteractables(_interactableHandler);
            if (raw == null) return new int[0];
            _cooldownField ??= typeof(InteractableHandler).GetField("_interactablesOnCooldown", ReflectionFlags);
            var order = new List<int>();
            foreach (var item in _interactableHandler.ShuffledActivePool)
            {
                int index = System.Array.IndexOf(raw, item);
                if (index >= 0 && !order.Contains(index)) order.Add(index);
            }
            if (_cooldownField?.GetValue(_interactableHandler) is List<GameObject> cooldown)
                foreach (var item in cooldown)
                {
                    int index = System.Array.IndexOf(raw, item);
                    if (index >= 0 && !order.Contains(index)) order.Add(index);
                }
            return order.ToArray();
        }

        /// <summary>Server, migrated match: restores the pickups once the singleplayer handler built its initial pool.</summary>
        [Server]
        public void ServerRestorePool(int[] order)
        {
            if (order == null || order.Length == 0 || _interactableHandler == null) return;
            StartCoroutine(RestorePoolWhenReady(order));
        }

        private IEnumerator RestorePoolWhenReady(int[] order)
        {
            float waited = 0f;
            while (_interactableHandler.ShuffledActivePool.Count == 0 && waited < RESTORE_POOL_TIMEOUT)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            var raw = GetRawInteractables(_interactableHandler);
            if (raw == null) yield break;

            var pool = _interactableHandler.ShuffledActivePool;
            pool.Clear();
            foreach (int index in order)
                if (index >= 0 && index < raw.Length && raw[index] != null) pool.Add(raw[index]);
            foreach (var item in raw)
                if (item != null && !pool.Contains(item)) item.SetActive(false);

            _updateVisibleMethod ??= typeof(InteractableHandler).GetMethod("UpdateVisibleInteractableList", ReflectionFlags);
            _updateVisibleMethod?.Invoke(_interactableHandler, new object[] { 0 });
            SyncPoolToAllClients();
            MpCanary.Mark($"Pickups restored after host migration ({pool.Count} in play)");
        }

        private void OnDestroy()
        {
            if (_interactableHandler != null)
            {
                _interactableHandler.onPoolInitialize.RemoveListener(OnPoolInitialized);
            }

            // IsServerInitialized throws on a NetworkObject that never initialized or was shut down.
            if (NetworkManager != null && IsServerInitialized)
            {
                UnsubscribeFromInteractableEvents();
            }
        }
    }
}
