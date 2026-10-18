using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// Entry point for gameplay statistics, called by the collectors. Events queue on disk and leave in batches to
    /// Firestore and GA4. Release builds share unless turned off; development builds only when turned on.
    /// </summary>
    public static class Telemetry
    {
        private const string SHARE_KEY = "Telemetry_Share";
        private const string QUEUE_FILE = "telemetry_queue.json";
        private const float FIRST_FLUSH_DELAY = 10f;

        private static readonly List<TelemetryEvent> Pending = new List<TelemetryEvent>();
        private static readonly List<TelemetryBatch> Batches = new List<TelemetryBatch>();
        private static Runner _runner;
        private static long _sessionId;

        /// <summary>Editor and development builds: data marked dev, kept apart from real players.</summary>
        public static bool IsDevelopment => Application.isEditor || Debug.isDebugBuild;

        /// <summary>The player's choice; release builds share by default, development builds do not.</summary>
        public static bool Sharing
        {
            get => PlayerPrefs.GetInt(SHARE_KEY, IsDevelopment ? 0 : 1) == 1;
            set
            {
                PlayerPrefs.SetInt(SHARE_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
                if (!value) { Pending.Clear(); Batches.Clear(); DeleteQueueFile(); }
            }
        }

        public static string Version => Application.version;

        /// <summary>The game is closing: last chance to Track (it is saved and sent next time).</summary>
        public static event Action Quitting;
        public static long SessionId => _sessionId;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Pending.Clear();
            Batches.Clear();
            _runner = null;
            _sessionId = 0;
            Quitting = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            _sessionId = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var go = new GameObject("[Telemetry]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            _runner = go.AddComponent<Runner>();
            LoadQueueFile();
        }

        public static void Track(string key, params (string name, object value)[] data)
        {
            var map = new Dictionary<string, object>();
            if (data != null) foreach (var (name, value) in data) map[name] = value;
            Track(key, map);
        }

        public static void Track(string key, Dictionary<string, object> data)
        {
            if (!Sharing || string.IsNullOrEmpty(key)) return;
            Pending.Add(new TelemetryEvent { key = key, time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), data = data ?? new Dictionary<string, object>() });
            if (Pending.Count > TelemetryConfig.MAX_QUEUED_EVENTS) Pending.RemoveAt(0);
            if (Debug.isDebugBuild) Debug.Log($"[Telemetry] {key} {JsonConvert.SerializeObject(data)}");
        }

        /// <summary>Send what is queued now (end of a match, the tutorial...).</summary>
        public static void Flush() => _runner?.RequestFlush();

        /// <summary>The player's own profile document (progression), replaced as a whole.</summary>
        public static void SetPlayerProfile(Dictionary<string, object> progress)
        {
            if (!Sharing) return;
            _runner?.SendPlayer(new Dictionary<string, object>
            {
                ["steamHash"] = TelemetryIdentity.SteamHash,
                ["version"] = Version,
                ["dev"] = IsDevelopment,
                ["progress"] = progress,
                ["updatedAt"] = DateTime.UtcNow,
            });
        }

        private static string QueuePath => Path.Combine(Application.persistentDataPath, QUEUE_FILE);

        private static void SaveQueueFile()
        {
            try
            {
                MoveEventsToBatches();
                if (Batches.Count == 0) { DeleteQueueFile(); return; }
                File.WriteAllText(QueuePath, JsonConvert.SerializeObject(Batches));
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] Could not save the queue: " + e.Message); }
        }

        private static void LoadQueueFile()
        {
            try
            {
                if (!File.Exists(QueuePath)) return;
                var saved = JsonConvert.DeserializeObject<List<TelemetryBatch>>(File.ReadAllText(QueuePath));
                if (saved != null) Batches.AddRange(saved.FindAll(b => b != null && b.events != null && !b.Done));
                DeleteQueueFile();
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] Could not read the saved queue: " + e.Message); DeleteQueueFile(); }
        }

        private static void DeleteQueueFile()
        {
            try { if (File.Exists(QueuePath)) File.Delete(QueuePath); } catch { /* next time */ }
        }

        private static void MoveEventsToBatches()
        {
            while (Pending.Count > 0)
            {
                int count = Mathf.Min(Pending.Count, TelemetryConfig.MAX_EVENTS_PER_BATCH);
                Batches.Add(new TelemetryBatch { events = Pending.GetRange(0, count) });
                Pending.RemoveRange(0, count);
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private readonly FirestoreSink _firestore = new FirestoreSink();
            private readonly AnalyticsSink _analytics = new AnalyticsSink();
            private bool _sending, _flushRequested;
            private float _nextFlush;

            private void Start() => _nextFlush = Time.unscaledTime + FIRST_FLUSH_DELAY;   // leftovers from last time go soon

            public void RequestFlush() => _flushRequested = true;

            private void Update()
            {
                if (_sending || !Sharing) return;
                if (!_flushRequested && Time.unscaledTime < _nextFlush) return;
                _flushRequested = false;
                _nextFlush = Time.unscaledTime + TelemetryConfig.FLUSH_INTERVAL;
                MoveEventsToBatches();
                if (Batches.Count > 0) StartCoroutine(SendAll());
            }

            private IEnumerator SendAll()
            {
                _sending = true;
                string steamHash = TelemetryIdentity.SteamHash;
                foreach (var batch in Batches.ToArray())
                {
                    if (!batch.sentToAnalytics)
                        yield return _analytics.Send(batch.events, steamHash, _sessionId, Version, IsDevelopment, ok => batch.sentToAnalytics = ok);
                    if (!batch.sentToFirestore)
                        yield return _firestore.SendEvents(batch.events, steamHash, Version, IsDevelopment, ok => batch.sentToFirestore = ok);
                    if (batch.Done) Batches.Remove(batch);
                    else break;   // offline: try again later, in order
                }
                _sending = false;
            }

            public void SendPlayer(Dictionary<string, object> fields) =>
                StartCoroutine(_firestore.SendPlayer(fields, ok =>
                {
                    if (!ok && Debug.isDebugBuild) Debug.LogWarning("[Telemetry] Player profile not sent; it goes again with the next change.");
                }));

            private void OnApplicationQuit()
            {
                try { Quitting?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
                SaveQueueFile();
            }
            private void OnApplicationPause(bool paused) { if (paused) SaveQueueFile(); }
        }
    }
}
