#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Turns "does shooting work and feel right in multiplayer" into numbers.
    ///
    /// Drives the LOCAL player's weapons through PlayerInputController the same way the real buttons
    /// do (normal fire, push shot, mine, ultimate) and records every projectile and ultimate object
    /// this peer draws, once per frame, stamped with a machine-wide clock (DateTime.UtcNow) so the
    /// host's and the client's recordings of the same run can be lined up afterwards.
    ///
    /// Script syntax, semicolon separated: <c>normal:1.5</c> (hold fire for 1.5 s), <c>push</c>,
    /// <c>mine</c>, <c>ultimate</c> (clears the owner's ultimate cooldown first), <c>wait:2</c>,
    /// <c>camera:0.5</c> (FreeLook height 0..1; weapons aim where the camera looks, and an untouched
    /// camera sits on its top rig looking down, which puts every bullet into the ground).
    /// A peer that should only watch runs a script made of waits.
    ///
    /// <code>
    /// ShootingProbe.Run("pepe_client", "normal:1;wait:1.5;push;wait:2.5;mine;wait:4;ultimate;wait:7");
    /// ShootingProbe.Status        // "running ..." / "done ..."
    /// ShootingProbe.LastSummary   // per-object metrics
    /// </code>
    /// Files land in &lt;project&gt;/Logs/ShootingProbe: &lt;label&gt;_samples.csv, _events.csv, _summary.txt.
    /// </summary>
    public static class ShootingProbe
    {
        public static string LastSummary { get; internal set; } = "";
        public static string LastDirectory { get; internal set; } = "";

        public static string Status => ShootingProbeRecorder.Instance == null
            ? "idle; last: " + (string.IsNullOrEmpty(LastDirectory) ? "none" : LastDirectory)
            : ShootingProbeRecorder.Instance.Describe();

        public static string Run(string label, string script, string directory = null)
        {
            if (ShootingProbeRecorder.Instance != null)
                return "already running: " + ShootingProbeRecorder.Instance.Describe();

            if (string.IsNullOrEmpty(directory))
                directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "ShootingProbe"));

            var go = new GameObject("[ShootingProbe]") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(go);
            var recorder = go.AddComponent<ShootingProbeRecorder>();
            string error = recorder.Begin(label, script, directory);
            if (error != null)
            {
                UnityEngine.Object.Destroy(go);
                return "error: " + error;
            }
            return "started " + label;
        }

        public static string Abort()
        {
            if (ShootingProbeRecorder.Instance == null) return "idle";
            ShootingProbeRecorder.Instance.Finish("aborted");
            return "aborted";
        }
    }

    internal enum ProbeObjectKind
    {
        Projectile,     // networked projectile not initialised yet
        NormalBullet,
        PushBullet,
        Mine,
        UltTomatoUp,
        UltTomatoDown,
        UltChicken,
        UltCroquette,
        SpTomatoUp,     // singleplayer (non-networked) objects: a duplicate of a networked one
        SpTomatoDown,
        SpChicken,
        SpBullet,
    }

    [DefaultExecutionOrder(32000)]
    internal sealed class ShootingProbeRecorder : MonoBehaviour
    {
        internal static ShootingProbeRecorder Instance;

        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo ProjectileShooter = typeof(NetworkedProjectile).GetField("_shooter", Private);
        private static readonly FieldInfo ProjectileIsMine = typeof(NetworkedProjectile).GetField("_isMine", Private);
        private static readonly FieldInfo ProjectileRadius = typeof(NetworkedProjectile).GetField("_explosionRadius", Private);
        private static readonly FieldInfo ProjectileVisuals = typeof(NetworkedProjectile).GetField("_visuals", Private);
        private static readonly FieldInfo ProjectileInitialized = typeof(NetworkedProjectile).GetField("_isInitialized", Private);
        private static readonly FieldInfo MariaCroquettes = typeof(MariaAntoniaUniqueAbility).GetField("_croquetteList", Private);
        private static readonly FieldInfo AbilityOnCooldown = typeof(BaseUniqueAbility).GetField("_isOnCooldown", Private);
        private static readonly FieldInfo AbilityCooldownTimer = typeof(BaseUniqueAbility).GetField("_cooldownTimer", Private);
        private static readonly FieldInfo InputShooting = typeof(PlayerInputController).GetField("_isShooting", Private);
        private static readonly FieldInfo InputPushMode = typeof(PlayerInputController).GetField("_isPushShootMode", Private);
        private static readonly FieldInfo InputPushShooting = typeof(PlayerInputController).GetField("_isPushShooting", Private);
        private static readonly FieldInfo InputMine = typeof(PlayerInputController).GetField("_isThrowingMine", Private);
        private static readonly FieldInfo InputAbility = typeof(PlayerInputController).GetField("_isUsingAbility", Private);

        private sealed class Tracked
        {
            public int Key;
            public ProbeObjectKind Kind;
            public int NetId = -1;
            public bool LocalShot;      // fired by this peer's player
            public string Name;
            public readonly List<long> Wall = new();
            public readonly List<int> Frame = new();
            public readonly List<Vector3> Position = new();
            public readonly List<bool> Visible = new();
            public bool SeenThisFrame;
        }

        private readonly struct Step
        {
            public readonly string Action;
            public readonly float Seconds;
            public Step(string action, float seconds) { Action = action; Seconds = seconds; }
        }

        private readonly Dictionary<int, Tracked> _tracked = new();
        private readonly List<string> _events = new();
        private readonly Dictionary<int, float> _endurance = new();
        private readonly Queue<Step> _steps = new();
        private readonly List<(long wall, string action)> _fired = new();

        private string _label;
        private string _directory;
        private float _stepEndsAt;
        private float _releaseAt = -1f;
        private Action _release;
        private int _startAccepted, _startRejected, _startAbilityAccepted, _startAbilityRejected;
        private long _startWall;
        private string _currentStep = "";

        // Local weapon state, logged whenever it flips, so a gun that stops re-arming shows up.
        private NormalShootHandle _localNormal;
        private PushShootHandle _localPush;
        private bool _lastNormalCan = true, _lastPushCan = true, _lastOverheat;

        // Every car's ultimate state, logged on change on this peer (Josefino's shield is state, not
        // an object, so this is how it shows up in the report).
        private readonly Dictionary<int, (bool active, bool invulnerable)> _ultimateState = new();

        internal string Begin(string label, string script, string directory)
        {
            _label = string.IsNullOrEmpty(label) ? "probe" : label;
            _directory = directory;
            foreach (string raw in (script ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = raw.Trim().Split(':');
                string action = parts[0].Trim().ToLowerInvariant();
                float seconds = 0f;
                if (parts.Length > 1 && !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out seconds))
                    return $"bad step '{raw}'";
                if (action != "normal" && action != "push" && action != "mine" && action != "ultimate" &&
                    action != "wait" && action != "camera")
                    return $"unknown action '{action}'";
                _steps.Enqueue(new Step(action, seconds));
            }

            _startAccepted = ShootingNetworkStats.ProjectilesAccepted;
            _startRejected = ShootingNetworkStats.ProjectilesRejected;
            _startAbilityAccepted = ShootingNetworkStats.AbilityObjectsAccepted;
            _startAbilityRejected = ShootingNetworkStats.AbilityObjectsRejected;
            _startWall = DateTime.UtcNow.Ticks;
            _stepEndsAt = Time.unscaledTime;
            Instance = this;
            AddEvent("begin " + _label + " script=" + script);
            return null;
        }

        internal string Describe() =>
            $"running {_label} step='{_currentStep}' queued={_steps.Count} objects={_tracked.Count} " +
            $"t={(DateTime.UtcNow.Ticks - _startWall) / 1e7:F1}s";

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_releaseAt >= 0f && Time.unscaledTime >= _releaseAt)
            {
                _releaseAt = -1f;
                var release = _release;
                _release = null;
                release?.Invoke();
            }

            if (Time.unscaledTime < _stepEndsAt || _releaseAt >= 0f) return;

            if (_steps.Count == 0)
            {
                Finish("done");
                return;
            }

            Step step = _steps.Dequeue();
            _currentStep = step.Action;
            StartStep(step);
        }

        private void StartStep(Step step)
        {
            PlayerInputController input = PlayerInputController.ExistingInstance;
            if (step.Action == "wait")
            {
                _stepEndsAt = Time.unscaledTime + step.Seconds;
                return;
            }

            if (step.Action == "camera")
            {
                // Height as given, heading behind the local car so shots go where the car points.
                float yaw = 0f;
                foreach (var vehicle in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
                    if (vehicle.IsOwner) yaw = vehicle.transform.eulerAngles.y;
                foreach (var freeLook in FindObjectsByType<Unity.Cinemachine.CinemachineFreeLook>(FindObjectsSortMode.None))
                {
                    if (!freeLook.isActiveAndEnabled) continue;
                    freeLook.m_YAxis.Value = Mathf.Clamp01(step.Seconds);
                    freeLook.m_XAxis.Value = Mathf.DeltaAngle(0f, yaw);
                }
                AddEvent($"camera height {step.Seconds.ToString(CultureInfo.InvariantCulture)} heading {yaw:F0}");
                return;
            }

            if (input == null)
            {
                AddEvent($"skip {step.Action}: no local PlayerInputController");
                return;
            }

            _fired.Add((DateTime.UtcNow.Ticks, step.Action));
            AddEvent("fire " + step.Action + (step.Seconds > 0f ? ":" + step.Seconds.ToString(CultureInfo.InvariantCulture) : ""));

            switch (step.Action)
            {
                case "normal":
                    InputShooting.SetValue(input, true);
                    Hold(Mathf.Max(0.05f, step.Seconds), () => InputShooting.SetValue(input, false));
                    break;

                case "push":
                    // Real input: the shooting-mode button enters push mode (aim trace), the fire button
                    // shoots, releasing it leaves push mode.
                    InputPushMode.SetValue(input, true);
                    Hold(0.35f, () =>
                    {
                        InputPushShooting.SetValue(input, true);
                        AddEvent("push trigger");
                        _fired.Add((DateTime.UtcNow.Ticks, "push-trigger"));
                        Hold(0.15f, () =>
                        {
                            InputPushShooting.SetValue(input, false);
                            InputPushMode.SetValue(input, false);
                        });
                    });
                    break;

                case "mine":
                    // Same pulse as PlayerInputController's ThrowMine binding (true for one second).
                    InputMine.SetValue(input, true);
                    Hold(1f, () => InputMine.SetValue(input, false));
                    break;

                case "ultimate":
                    ClearOwnUltimateCooldown();
                    InputAbility.SetValue(input, true);
                    Hold(0.3f, () => InputAbility.SetValue(input, false));
                    break;
            }
        }

        private void Hold(float seconds, Action release)
        {
            _releaseAt = Time.unscaledTime + seconds;
            _release = release;
        }

        private void ClearOwnUltimateCooldown()
        {
            foreach (var vehicle in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
            {
                if (!vehicle.IsOwner) continue;
                var ability = vehicle.GetComponentInChildren<BaseUniqueAbility>(true);
                if (ability == null) continue;
                if (AbilityCooldownTimer?.GetValue(ability) is global::Utilities.ITimer timer && global::Utilities.TimerSystem.Instance != null &&
                    global::Utilities.TimerSystem.Instance.HasTimer(timer))
                    global::Utilities.TimerSystem.Instance.StopTimer(timer.GetData().ID);
                AbilityCooldownTimer?.SetValue(ability, null);
                AbilityOnCooldown?.SetValue(ability, false);
                AddEvent($"ultimate cooldown cleared on {ability.GetType().Name} active={ability.IsAbilityActive}");
            }
        }

        // ------------------------------------------------------------------ sampling

        private void LateUpdate()
        {
            long wall = DateTime.UtcNow.Ticks;
            int frame = Time.frameCount;
            foreach (var t in _tracked.Values) t.SeenThisFrame = false;
            TraceLocalWeapons();
            TraceUltimates();

            foreach (var projectile in FindObjectsByType<NetworkedProjectile>(FindObjectsSortMode.None))
            {
                bool initialized = ProjectileInitialized != null && (bool)ProjectileInitialized.GetValue(projectile);
                ProbeObjectKind kind = ProbeObjectKind.Projectile;
                if (initialized)
                {
                    bool mine = (bool)ProjectileIsMine.GetValue(projectile);
                    float radius = (float)ProjectileRadius.GetValue(projectile);
                    kind = mine ? ProbeObjectKind.Mine : radius > 0f ? ProbeObjectKind.PushBullet : ProbeObjectKind.NormalBullet;
                }
                var visuals = ProjectileVisuals?.GetValue(projectile) as GameObject;
                Transform drawn = visuals != null ? visuals.transform : projectile.transform;
                bool visible = visuals != null ? visuals.activeInHierarchy : projectile.gameObject.activeInHierarchy;
                var shooter = ProjectileShooter?.GetValue(projectile) as NetworkObject;
                Record(projectile.gameObject, kind, projectile.NetworkObject, drawn.position, visible,
                    shooter != null && shooter.IsOwner, wall, frame);
            }

            foreach (var tomato in FindObjectsByType<NetworkedAscendingTomato>(FindObjectsSortMode.None))
                RecordRendered(tomato.gameObject, ProbeObjectKind.UltTomatoUp, wall, frame);
            foreach (var tomato in FindObjectsByType<NetworkedDescendingTomato>(FindObjectsSortMode.None))
                RecordRendered(tomato.gameObject, ProbeObjectKind.UltTomatoDown, wall, frame);
            foreach (var chicken in FindObjectsByType<NetworkedMcChickenBehaviour>(FindObjectsSortMode.None))
                RecordRendered(chicken.gameObject, ProbeObjectKind.UltChicken, wall, frame);

            foreach (var tomato in FindObjectsByType<AscendingTomatoBehaviour>(FindObjectsSortMode.None))
                if (tomato.GetComponent<NetworkObject>() == null) RecordRendered(tomato.gameObject, ProbeObjectKind.SpTomatoUp, wall, frame);
            foreach (var tomato in FindObjectsByType<DescendingTomatoBehaviour>(FindObjectsSortMode.None))
                if (tomato.GetComponent<NetworkObject>() == null) RecordRendered(tomato.gameObject, ProbeObjectKind.SpTomatoDown, wall, frame);
            foreach (var chicken in FindObjectsByType<McChicken>(FindObjectsSortMode.None))
                if (chicken.GetComponent<NetworkObject>() == null) RecordRendered(chicken.gameObject, ProbeObjectKind.SpChicken, wall, frame);
            foreach (var bullet in FindObjectsByType<BulletBehaviour>(FindObjectsSortMode.None))
                if (bullet.GetComponent<NetworkObject>() == null) RecordRendered(bullet.gameObject, ProbeObjectKind.SpBullet, wall, frame);

            if (MariaCroquettes != null)
            {
                foreach (var maria in FindObjectsByType<MariaAntoniaUniqueAbility>(FindObjectsSortMode.None))
                {
                    if (!(MariaCroquettes.GetValue(maria) is List<GameObject> croquettes)) continue;
                    foreach (var croquette in croquettes)
                        if (croquette != null) RecordRendered(croquette, ProbeObjectKind.UltCroquette, wall, frame);
                }
            }

            foreach (var stats in FindObjectsByType<StatsController>(FindObjectsSortMode.None))
            {
                int id = stats.GetInstanceID();
                float endurance = stats.Endurance;
                if (_endurance.TryGetValue(id, out float previous) && Mathf.Abs(previous - endurance) > 0.01f)
                    AddEvent($"endurance {stats.transform.root.name}/{stats.name} {previous:F1} -> {endurance:F1}");
                _endurance[id] = endurance;
            }
        }

        private void TraceUltimates()
        {
            foreach (var vehicle in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
            {
                var ability = vehicle.GetComponentInChildren<BaseUniqueAbility>(true);
                var stats = vehicle.GetComponentInChildren<StatsController>(true);
                bool active = ability != null && ability.IsAbilityActive;
                bool invulnerable = stats != null && stats.IsInvulnerable;
                int id = vehicle.GetInstanceID();
                string who = (vehicle.IsOwner ? "own " : "other ") + (ability != null ? ability.GetType().Name : vehicle.name);
                if (!_ultimateState.TryGetValue(id, out var previous))
                {
                    _ultimateState[id] = (active, invulnerable);
                    continue;
                }
                if (previous.active != active) AddEvent($"ultimate {who} active={active}");
                if (previous.invulnerable != invulnerable) AddEvent($"ultimate {who} invulnerable={invulnerable}");
                _ultimateState[id] = (active, invulnerable);
            }
        }

        private void TraceLocalWeapons()
        {
            if (_localNormal == null || _localPush == null)
            {
                foreach (var vehicle in FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None))
                {
                    if (!vehicle.IsOwner) continue;
                    Transform root = vehicle.transform.root;
                    _localNormal = root.GetComponentInChildren<NormalShootHandle>(true);
                    _localPush = root.GetComponentInChildren<PushShootHandle>(true);
                }
                if (_localNormal == null || _localPush == null) return;
            }

            if (_localNormal.CanShoot != _lastNormalCan)
            {
                _lastNormalCan = _localNormal.CanShoot;
                AddEvent($"normal canShoot={_lastNormalCan} heat={_localNormal.CountOverHeat:F2}");
            }
            if (_localNormal.IsOverHeat != _lastOverheat)
            {
                _lastOverheat = _localNormal.IsOverHeat;
                AddEvent($"normal overheated={_lastOverheat}");
            }
            if (_localPush.CanShoot != _lastPushCan)
            {
                _lastPushCan = _localPush.CanShoot;
                AddEvent($"push/mine canShoot={_lastPushCan}");
            }
        }

        private void RecordRendered(GameObject go, ProbeObjectKind kind, long wall, int frame)
        {
            var renderer = go.GetComponentInChildren<Renderer>();
            Vector3 position = renderer != null ? renderer.transform.position : go.transform.position;
            bool visible = go.activeInHierarchy && (renderer == null || renderer.enabled);
            Record(go, kind, go.GetComponent<NetworkObject>(), position, visible, false, wall, frame);
        }

        private void Record(GameObject go, ProbeObjectKind kind, NetworkObject nob, Vector3 position, bool visible,
            bool localShot, long wall, int frame)
        {
            int key = go.GetInstanceID();
            if (!_tracked.TryGetValue(key, out Tracked t))
            {
                t = new Tracked { Key = key, Kind = kind, Name = go.name };
                _tracked[key] = t;
            }
            if (t.Kind == ProbeObjectKind.Projectile && kind != ProbeObjectKind.Projectile) t.Kind = kind;
            if (nob != null && nob.ObjectId >= 0 && nob.IsSpawned) t.NetId = nob.ObjectId;
            t.LocalShot |= localShot || (nob != null && nob.IsOwner && kind >= ProbeObjectKind.UltTomatoUp && kind <= ProbeObjectKind.UltChicken);
            t.SeenThisFrame = true;
            t.Wall.Add(wall);
            t.Frame.Add(frame);
            t.Position.Add(position);
            t.Visible.Add(visible);
        }

        private void AddEvent(string text) =>
            _events.Add($"{DateTime.UtcNow.Ticks},{Time.frameCount},{text.Replace(',', ';')}");

        // ------------------------------------------------------------------ report

        internal void Finish(string reason)
        {
            AddEvent("end " + reason);
            try
            {
                Directory.CreateDirectory(_directory);
                string stem = Path.Combine(_directory, _label);
                WriteSamples(stem + "_samples.csv");
                File.WriteAllText(stem + "_events.csv", "wall,frame,event\n" + string.Join("\n", _events));
                string summary = BuildSummary(reason);
                File.WriteAllText(stem + "_summary.txt", summary);
                ShootingProbe.LastSummary = summary;
                ShootingProbe.LastDirectory = _directory;
            }
            catch (Exception exception)
            {
                ShootingProbe.LastSummary = "failed to write report: " + exception.Message;
            }
            Destroy(gameObject);
        }

        private void WriteSamples(string path)
        {
            var sb = new StringBuilder("key,kind,netId,local,wall,frame,x,y,z,visible\n");
            foreach (var t in _tracked.Values)
                for (int i = 0; i < t.Wall.Count; i++)
                {
                    Vector3 p = t.Position[i];
                    sb.Append(t.Key).Append(',').Append(t.Kind).Append(',').Append(t.NetId).Append(',')
                      .Append(t.LocalShot ? 1 : 0).Append(',').Append(t.Wall[i]).Append(',').Append(t.Frame[i]).Append(',')
                      .Append(p.x.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(p.y.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(p.z.ToString("R", CultureInfo.InvariantCulture)).Append(',')
                      .Append(t.Visible[i] ? 1 : 0).Append('\n');
                }
            File.WriteAllText(path, sb.ToString());
        }

        private string BuildSummary(string reason)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"ShootingProbe '{_label}' ({reason}) on {(FishNet.InstanceFinder.IsServerStarted ? "HOST" : "CLIENT")}, " +
                          $"{(DateTime.UtcNow.Ticks - _startWall) / 1e7:F1}s");
            sb.AppendLine($"server verdicts during run: projectiles accepted={ShootingNetworkStats.ProjectilesAccepted - _startAccepted} " +
                          $"rejected={ShootingNetworkStats.ProjectilesRejected - _startRejected}; ultimate objects " +
                          $"accepted={ShootingNetworkStats.AbilityObjectsAccepted - _startAbilityAccepted} " +
                          $"rejected={ShootingNetworkStats.AbilityObjectsRejected - _startAbilityRejected}");
            sb.AppendLine("kind            key      net  local  latency_ms  life_ms  frames  visible  max_step_m  snapbacks  residual_mm");

            var counts = new Dictionary<ProbeObjectKind, int>();
            foreach (var t in _tracked.Values)
            {
                counts.TryGetValue(t.Kind, out int c);
                counts[t.Kind] = c + 1;

                int visibleFrames = 0;
                foreach (bool v in t.Visible) if (v) visibleFrames++;
                double lifeMs = (t.Wall[t.Wall.Count - 1] - t.Wall[0]) / 1e4;
                string latency = t.LocalShot ? SpawnLatencyMs(t).ToString("F0", CultureInfo.InvariantCulture) : "-";
                Motion(t, out float maxStep, out int snapBacks, out float residualMm);
                sb.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "{0,-14} {1,8} {2,5} {3,6} {4,11} {5,8:F0} {6,7} {7,8} {8,11:F2} {9,10} {10,12:F0}",
                    t.Kind, t.Key, t.NetId, t.LocalShot ? "yes" : "no", latency, lifeMs, t.Wall.Count, visibleFrames,
                    maxStep, snapBacks, residualMm));
            }

            sb.Append("objects per kind:");
            foreach (var pair in counts) sb.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            sb.AppendLine();
            sb.AppendLine("events:");
            foreach (string e in _events)
            {
                string[] parts = e.Split(new[] { ',' }, 3);
                double t = (long.Parse(parts[0]) - _startWall) / 1e7;
                sb.AppendLine($"  {t,6:F2}s  {parts[2]}");
            }
            return sb.ToString();
        }

        /// <summary>Time from the input that fired this object to the first frame it was drawn.</summary>
        private double SpawnLatencyMs(Tracked t)
        {
            string wanted = t.Kind switch
            {
                ProbeObjectKind.NormalBullet => "normal",
                ProbeObjectKind.PushBullet => "push-trigger",
                ProbeObjectKind.Mine => "mine",
                _ => "ultimate",
            };
            long first = t.Wall[0];
            long best = long.MinValue;
            foreach (var (wall, action) in _fired)
                if (action == wanted && wall <= first && wall > best) best = wall;
            return best == long.MinValue ? double.NaN : (first - best) / 1e4;
        }

        /// <summary>
        /// Smoothness of what was drawn while visible: the largest single-frame step, the number of
        /// frames that stepped BACKWARDS against the object's own direction of travel by more than
        /// 5 cm (a correction snapping it back), and the largest deviation from a smooth local fit.
        /// </summary>
        private static void Motion(Tracked t, out float maxStep, out int snapBacks, out float residualMm)
        {
            maxStep = 0f;
            snapBacks = 0;
            residualMm = 0f;
            const int w = 3;
            for (int i = 1; i < t.Position.Count; i++)
            {
                if (!t.Visible[i] || !t.Visible[i - 1]) continue;
                Vector3 step = t.Position[i] - t.Position[i - 1];
                maxStep = Mathf.Max(maxStep, step.magnitude);

                int a = Mathf.Max(0, i - w), b = Mathf.Min(t.Position.Count - 1, i + w);
                Vector3 travel = t.Position[b] - t.Position[a];
                if (travel.sqrMagnitude > 0.01f && Vector3.Dot(step, travel.normalized) < -0.05f) snapBacks++;

                if (i >= w && i + w < t.Position.Count)
                {
                    double t0 = t.Wall[i];
                    Vector3 fit = QuadraticFitAt(t, i - w, i + w, i, t0);
                    residualMm = Mathf.Max(residualMm, (t.Position[i] - fit).magnitude * 1000f);
                }
            }
        }

        private static Vector3 QuadraticFitAt(Tracked t, int from, int to, int skip, double centre)
        {
            // Least squares y = a + b x + c x^2 per axis over the window (centre sample excluded).
            double s0 = 0, s1 = 0, s2 = 0, s3 = 0, s4 = 0;
            var ty = new Vector3[3];
            double[,] tsum = new double[3, 3];
            for (int j = from; j <= to; j++)
            {
                if (j == skip || !t.Visible[j]) continue;
                double x = (t.Wall[j] - centre) / 1e7;
                double x2 = x * x;
                s0 += 1; s1 += x; s2 += x2; s3 += x2 * x; s4 += x2 * x2;
                Vector3 p = t.Position[j];
                for (int axis = 0; axis < 3; axis++)
                {
                    tsum[axis, 0] += p[axis];
                    tsum[axis, 1] += x * p[axis];
                    tsum[axis, 2] += x2 * p[axis];
                }
            }
            double det = Det(s0, s1, s2, s1, s2, s3, s2, s3, s4);
            if (Math.Abs(det) < 1e-12) return t.Position[skip];
            var result = Vector3.zero;
            for (int axis = 0; axis < 3; axis++)
                result[axis] = (float)(Det(tsum[axis, 0], s1, s2, tsum[axis, 1], s2, s3, tsum[axis, 2], s3, s4) / det);
            return result;
        }

        private static double Det(double a, double b, double c, double d, double e, double f, double g, double h, double i) =>
            a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
    }
}
#endif
