using System.Collections.Generic;
using FishNet;
using FishNet.Broadcast;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Server → clients: where the loose props that moved are now.</summary>
    public struct LoosePropsBroadcast : IBroadcast
    {
        public int RegistryCount;   // sanity check: both peers must have found the same props
        public int[] Indices;
        public Vector3[] Positions;
        public Quaternion[] Rotations;
    }

    /// <summary>
    /// The map's loose physics props (cones, bricks, benches...): the server sends the pose of every prop that
    /// moved and clients follow it kinematically, then hand it back to local physics at the server's resting
    /// pose. Found at runtime and keyed by hierarchy path.
    /// </summary>
    public sealed class NetworkedLooseProps : MonoBehaviour
    {
        private const float SEND_INTERVAL = 0.05f;          // 20 Hz while something moves
        private const float MIN_MOVE_SQR = 0.02f * 0.02f;   // 2 cm
        private const float MIN_TURN_DEGREES = 1f;
        private const float FOLLOW_SHARPNESS = 15f;
        private const float RELEASE_AFTER = 0.6f;           // no news from the server for this long: it has settled
        private const float STRAY_CHECK_INTERVAL = 0.5f;
        private const float STRAY_GRACE = 1.5f;
        private const float STRAY_DISTANCE_SQR = 0.5f * 0.5f;
        private const float STRAY_MAX_SPEED = 0.5f;

        // Trampoline parts: rigidbodies of the trampoline's own logic, not props.
        private static readonly string[] ExcludedComponents = { "landingCheck", "TrampolineAnimations" };

        private Rigidbody[] _bodies = new Rigidbody[0];
        private Collider[][] _colliders = new Collider[0][];
        private Vector3[] _sentPosition = new Vector3[0];
        private Quaternion[] _sentRotation = new Quaternion[0];
        private bool _collected;
        private bool _clientRegistered;
        private float _nextSend;

        private Vector3[] _serverPosition = new Vector3[0];
        private Quaternion[] _serverRotation = new Quaternion[0];
        private float[] _lastNews = new float[0];
        private bool[] _driven = new bool[0];
        private LayerMask[] _excludeBefore = new LayerMask[0];
        private float _nextStrayCheck;
        private int _charactersMask;

        public int Count => _bodies.Length;

        private void Start()
        {
            var roots = new List<GameObject>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                // Only the map: other loaded scenes differ between peers.
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                var sceneRoots = scene.GetRootGameObjects();
                bool isMap = false;
                foreach (var root in sceneRoots)
                    if (root.GetComponentInChildren<LevelController>(true) != null) { isMap = true; break; }
                if (isMap) roots.AddRange(sceneRoots);
            }
            _bodies = Collect(roots.ToArray());

            int n = _bodies.Length;
            _colliders = new Collider[n][];
            _sentPosition = new Vector3[n];
            _sentRotation = new Quaternion[n];
            _serverPosition = new Vector3[n];
            _serverRotation = new Quaternion[n];
            _lastNews = new float[n];
            _driven = new bool[n];
            _excludeBefore = new LayerMask[n];
            for (int i = 0; i < n; i++)
            {
                _colliders[i] = _bodies[i].GetComponentsInChildren<Collider>(true);
                _sentPosition[i] = _serverPosition[i] = _bodies[i].position;
                _sentRotation[i] = _serverRotation[i] = _bodies[i].rotation;
                _lastNews[i] = float.NegativeInfinity;
            }
            int characters = LayerMask.NameToLayer("Characters");
            _charactersMask = characters >= 0 ? 1 << characters : 0;
            _collected = true;
            MpCanary.Mark($"Loose props: {n} synced");
        }

        private void OnEnable()
        {
            var client = InstanceFinder.ClientManager;
            if (client == null) return;
            client.RegisterBroadcast<LoosePropsBroadcast>(OnPosesReceived);
            _clientRegistered = true;
        }

        private void OnDisable()
        {
            if (_clientRegistered && InstanceFinder.ClientManager != null)
                InstanceFinder.ClientManager.UnregisterBroadcast<LoosePropsBroadcast>(OnPosesReceived);
            _clientRegistered = false;
        }

        private void Update()
        {
            if (!_collected) return;
            if (InstanceFinder.IsServerStarted) ServerSendMoved();
            else ClientFollow();
        }

        private void ServerSendMoved()
        {
            if (Time.unscaledTime < _nextSend) return;
            _nextSend = Time.unscaledTime + SEND_INTERVAL;

            List<int> indices = null;
            for (int i = 0; i < _bodies.Length; i++)
            {
                var body = _bodies[i];
                if (body == null) continue;
                Vector3 position = body.position;
                Quaternion rotation = body.rotation;
                if ((position - _sentPosition[i]).sqrMagnitude < MIN_MOVE_SQR &&
                    Quaternion.Angle(rotation, _sentRotation[i]) < MIN_TURN_DEGREES) continue;
                _sentPosition[i] = position;
                _sentRotation[i] = rotation;
                (indices ??= new List<int>()).Add(i);
            }
            if (indices == null) return;

            var msg = new LoosePropsBroadcast
            {
                RegistryCount = _bodies.Length,
                Indices = indices.ToArray(),
                Positions = new Vector3[indices.Count],
                Rotations = new Quaternion[indices.Count],
            };
            for (int k = 0; k < indices.Count; k++)
            {
                msg.Positions[k] = _sentPosition[indices[k]];
                msg.Rotations[k] = _sentRotation[indices[k]];
            }
            InstanceFinder.ServerManager.Broadcast(msg);
        }

        private void OnPosesReceived(LoosePropsBroadcast msg, Channel channel)
        {
            if (InstanceFinder.IsServerStarted || !_collected || msg.Indices == null) return;
            if (msg.RegistryCount != _bodies.Length)
            {
                MpCanary.Mark($"Loose props: server has {msg.RegistryCount}, this peer {_bodies.Length} - skipped");
                return;
            }
            float now = Time.unscaledTime;
            for (int k = 0; k < msg.Indices.Length; k++)
            {
                int i = msg.Indices[k];
                if (i < 0 || i >= _bodies.Length || _bodies[i] == null) continue;
                _serverPosition[i] = msg.Positions[k];
                _serverRotation[i] = msg.Rotations[k];
                _lastNews[i] = now;
                if (!_driven[i]) Drive(i, true);
            }
        }

        private void ClientFollow()
        {
            float now = Time.unscaledTime;
            float blend = 1f - Mathf.Exp(-FOLLOW_SHARPNESS * Time.deltaTime);
            for (int i = 0; i < _bodies.Length; i++)
            {
                if (!_driven[i] || _bodies[i] == null) continue;
                Transform t = _bodies[i].transform;
                if (now - _lastNews[i] > RELEASE_AFTER)
                {
                    t.SetPositionAndRotation(_serverPosition[i], _serverRotation[i]);
                    Drive(i, false);
                    continue;
                }
                t.SetPositionAndRotation(Vector3.Lerp(t.position, _serverPosition[i], blend),
                                         Quaternion.Slerp(t.rotation, _serverRotation[i], blend));
            }

            if (now < _nextStrayCheck) return;
            _nextStrayCheck = now + STRAY_CHECK_INTERVAL;
            for (int i = 0; i < _bodies.Length; i++)
            {
                var body = _bodies[i];
                if (_driven[i] || body == null || !body.gameObject.activeInHierarchy) continue;
                if (now - _lastNews[i] < STRAY_GRACE) continue;
                if ((body.position - _serverPosition[i]).sqrMagnitude < STRAY_DISTANCE_SQR) continue;
                if (body.linearVelocity.sqrMagnitude > STRAY_MAX_SPEED * STRAY_MAX_SPEED) continue;
                // This peer moved it and the server did not: put it back where the server has it.
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.transform.SetPositionAndRotation(_serverPosition[i], _serverRotation[i]);
                body.position = _serverPosition[i];
                body.rotation = _serverRotation[i];
                body.Sleep();
            }
        }

        /// <summary>Client: hand the prop to the server's poses, or give it back to local physics.</summary>
        private void Drive(int i, bool drive)
        {
            var body = _bodies[i];
            _driven[i] = drive;
            if (drive)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                foreach (var collider in _colliders[i])
                {
                    if (collider == null) continue;
                    collider.excludeLayers |= _charactersMask;
                }
                return;
            }
            body.position = _serverPosition[i];
            body.rotation = _serverRotation[i];
            body.isKinematic = false;
            foreach (var collider in _colliders[i])
            {
                if (collider == null) continue;
                collider.excludeLayers &= ~_charactersMask;
            }
            body.Sleep();
        }

        /// <summary>The map's non-kinematic rigidbodies by hierarchy path, without networked objects and trampoline parts.</summary>
        public static Rigidbody[] Collect(GameObject[] roots)
        {
            var list = new List<KeyValuePair<string, Rigidbody>>();
            foreach (var root in roots)
            {
                if (root == null) continue;
                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                {
                    if (body.isKinematic) continue;
                    if (body.GetComponentInParent<NetworkObject>(true) != null) continue;
                    if (HasExcludedComponent(body.gameObject)) continue;
                    list.Add(new KeyValuePair<string, Rigidbody>(NetworkedWorldBreakables.PathKey(body.transform), body));
                }
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list.ConvertAll(kv => kv.Value).ToArray();
        }

        private static bool HasExcludedComponent(GameObject go)
        {
            foreach (var behaviour in go.GetComponents<MonoBehaviour>())
            {
                if (behaviour == null) continue;
                string name = behaviour.GetType().Name;
                foreach (string excluded in ExcludedComponents)
                    if (name == excluded) return true;
            }
            return false;
        }
    }
}
