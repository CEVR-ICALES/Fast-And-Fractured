using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using FishNet;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Diagnostic recorder for the host push/ultimate input chain and the client camera tremble. Writes
    /// &lt;project&gt;/../MpAutoRecord.log only when something happens during a match.
    /// </summary>
    public static class MpAutoRecorder
    {
        private const float CameraJerkSpikeMetres = 0.05f;
        private const int HistoryFrames = 30;
        private const int MaxSpikesRecorded = 12;
        private const float BindRetrySeconds = 1f;

        private static Runner _runner;

        // Off by default (it samples every frame): Tools > Fast And Fractured > Diagnostics.
#if UNITY_EDITOR
        private const string EnabledPrefsKey = "FastAndFractured.Multiplayer.MpAutoRecorder.Enabled";

        public static bool IsEnabled
        {
            get => UnityEditor.EditorPrefs.GetBool(EnabledPrefsKey, false);
            set => UnityEditor.EditorPrefs.SetBool(EnabledPrefsKey, value);
        }

        private const string MenuPath = "Tools/Fast And Fractured/Diagnostics/Mp Auto Recorder (Enabled)";

        [UnityEditor.MenuItem(MenuPath)]
        private static void ToggleEnabled()
        {
            IsEnabled = !IsEnabled;
        }

        [UnityEditor.MenuItem(MenuPath, true)]
        private static bool ToggleEnabledValidate()
        {
            UnityEditor.Menu.SetChecked(MenuPath, IsEnabled);
            return true;
        }
#else
        public static bool IsEnabled => false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_runner != null) return;
            if (!IsEnabled) return;
            var go = new GameObject("[MpAutoRecorder]");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            _runner = go.AddComponent<Runner>();
        }

        private static string LogPath =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "MpAutoRecord.log");

        private sealed class Runner : MonoBehaviour
        {
            private readonly List<string> _lines = new();
            private readonly Queue<string> _history = new();
            private bool _bound;
            private bool _headerWritten;
            private int _spikes;
            private float _nextBindAttempt;

            private PlayerInputController _localInput;
            private PlayerInputController _otherInput;
            private PlayerInputProvider _provider;
            private PushShootHandle _push;
            private BaseUniqueAbility _ability;
            private string _lastInputState = "";

            private Transform _camera;
            private Transform _visuals;
            private Transform _body;
            private Rigidbody _rb;
            private Vector3 _prevCam, _prevCamDelta;
            private bool _hasPrevCam;

            private void LateUpdate()
            {
                if (!InstanceFinder.IsClientStarted) return;

                if (!_bound)
                {
                    if (Time.unscaledTime < _nextBindAttempt) return;
                    _nextBindAttempt = Time.unscaledTime + BindRetrySeconds;
                    TryBind();
                    return;
                }

                SampleInputChain();
                SampleCamera();
            }

            private void TryBind()
            {
                NetworkObject owned = null;
                foreach (NetworkedVehicle v in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
                {
                    NetworkObject nob = v.GetComponent<NetworkObject>();
                    if (nob == null || !nob.IsOwner) continue;
                    owned = nob;
                    _body = v.transform;
                    _visuals = v.transform.Find("Visuals");
                    _rb = v.GetComponent<Rigidbody>();
                    break;
                }
                if (owned == null) return;

                Transform root = owned.transform.root;
                _provider = root.GetComponentInChildren<PlayerInputProvider>(true);
                _push = root.GetComponentInChildren<PushShootHandle>(true);
                _ability = root.GetComponentInChildren<BaseUniqueAbility>(true);

                foreach (PlayerInputController pic in FindObjectsByType<PlayerInputController>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    NetworkObject nob = pic.GetComponentInParent<NetworkObject>(true);
                    if (nob != null && nob.IsOwner) _localInput = pic;
                    else _otherInput = pic;
                }

                if (Camera.main != null) _camera = Camera.main.transform;
                if (_localInput == null || _provider == null || _camera == null) return;

                _bound = true;
                WriteHeader();
            }

            private void WriteHeader()
            {
                if (_headerWritten) return;
                _headerWritten = true;

                var sb = new StringBuilder();
                sb.AppendLine("=== MpAutoRecorder ===");
                sb.AppendLine($"time            : {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"scene           : {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
                sb.AppendLine($"peer            : server={InstanceFinder.IsServerStarted} client={InstanceFinder.IsClientStarted}");
                sb.AppendLine($"local car       : {(_body == null ? "none" : _body.name)}");
                sb.AppendLine($"PlayerInputControllers: local={(_localInput == null ? "NULL" : _localInput.transform.root.name)}, " +
                              $"other={(_otherInput == null ? "none" : _otherInput.transform.root.name)}");
                sb.AppendLine($"PlayerHUD alive : {FindObjectsByType<PlayerHUD>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length}");
                sb.AppendLine($"ability         : {(_ability == null ? "NULL" : _ability.GetType().Name)}");
                sb.AppendLine($"push handle     : {(_push == null ? "NULL" : "ok")}");
                sb.AppendLine();
                sb.AppendLine("--- input chain changes (push shoot / ultimate) ---");
                _lines.Add(sb.ToString());
                Flush();
            }

            private void SampleInputChain()
            {
                string state =
                    $"localPushMode={Safe(() => _localInput.IsPushShootMode)} " +
                    $"localIsPush={Safe(() => _localInput.IsPushShooting)} " +
                    $"otherIsPush={(_otherInput == null ? "-" : Safe(() => _otherInput.IsPushShooting))} " +
                    $"provIsPush={Safe(() => _provider.IsPushShooting)} " +
                    $"provFirePush={Safe(() => _provider.FirePushAction)} " +
                    $"provNormal={Safe(() => _provider.IsNormalShooting)} " +
                    $"pushHandle={(_push == null ? "-" : Safe(() => _push.IsPushShooting))} " +
                    $"abilityActive={(_ability == null ? "-" : Safe(() => _ability.isAbilityActive))}";

                if (state == _lastInputState) return;
                _lastInputState = state;
                _lines.Add($"f{Time.frameCount} {state}");
                Flush();
            }

            private static string Safe(System.Func<bool> read)
            {
                try { return read().ToString(); }
                catch { return "?"; }
            }

            private void SampleCamera()
            {
                if (_camera == null) return;

                Vector3 cam = _camera.position;
                if (_hasPrevCam)
                {
                    Vector3 delta = cam - _prevCam;
                    float jerk = (delta - _prevCamDelta).magnitude;

                    string row = string.Format(CultureInfo.InvariantCulture,
                        "f{0} cam=({1:F3},{2:F3},{3:F3}) camStep={4:F4} jerk={5:F4} vis={6} body={7} rb={8} speed={9:F2}",
                        Time.frameCount, cam.x, cam.y, cam.z, delta.magnitude, jerk,
                        Fmt(_visuals == null ? Vector3.zero : _visuals.position),
                        Fmt(_body == null ? Vector3.zero : _body.position),
                        Fmt(_rb == null ? Vector3.zero : _rb.position),
                        _rb == null ? 0f : _rb.linearVelocity.magnitude);

                    _history.Enqueue(row);
                    while (_history.Count > HistoryFrames) _history.Dequeue();

                    // Only spikes are recorded, with the frames leading up to them.
                    if (jerk > CameraJerkSpikeMetres && _spikes < MaxSpikesRecorded)
                    {
                        _spikes++;
                        _lines.Add($"\n--- camera jerk spike #{_spikes} ({jerk:F4} m) at frame {Time.frameCount}, previous {_history.Count} frames ---");
                        _lines.AddRange(_history);
                        if (_spikes == MaxSpikesRecorded)
                            _lines.Add("(spike limit reached; no further camera spikes recorded)");
                        Flush();
                    }

                    _prevCamDelta = delta;
                }

                _prevCam = cam;
                _hasPrevCam = true;
            }

            private static string Fmt(Vector3 v) =>
                string.Format(CultureInfo.InvariantCulture, "({0:F3},{1:F3},{2:F3})", v.x, v.y, v.z);

            private void Flush()
            {
                try { File.WriteAllLines(LogPath, _lines); }
                catch { /* a diagnostic must never break the game */ }
            }

            private void OnDestroy() => Flush();
        }
    }
}
