using System.Collections.Generic;
using FastAndFractured.Progression;
using UnityEngine;

namespace FastAndFractured.GameTelemetry
{
    /// <summary>
    /// The player's progression: a level_up event, and their profile document in Firestore
    /// (level, XP, unlocked, equipped) - small, kept up to date after every change.
    /// </summary>
    public sealed class ProgressionCollector : MonoBehaviour
    {
        private const float SEND_DELAY = 3f;   // several changes in a row go as one write

        private int _level;
        private float _sendAt = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[Telemetry] Progression");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<ProgressionCollector>();
        }

        private void Awake() => _level = PlayerProgression.Level;
        private void OnEnable() => PlayerProgression.OnChanged += OnChanged;
        private void OnDisable() => PlayerProgression.OnChanged -= OnChanged;

        private void OnChanged()
        {
            int level = PlayerProgression.Level;
            if (level > _level)
                Telemetry.Track(TelemetryKeys.LEVEL_UP, ("level", level), ("total_xp", PlayerProgression.TotalXp));
            _level = level;
            _sendAt = Time.unscaledTime + SEND_DELAY;
        }

        private void Update()
        {
            if (_sendAt < 0f || Time.unscaledTime < _sendAt) return;
            _sendAt = -1f;
            Telemetry.SetPlayerProfile(new Dictionary<string, object>
            {
                ["level"] = PlayerProgression.Level,
                ["totalXp"] = PlayerProgression.TotalXp,
                ["unlocked"] = new List<string>(PlayerProgression.UnlockedIds),
                ["equipped"] = new List<string>(PlayerProgression.EquippedEntries),
            });
        }
    }
}
