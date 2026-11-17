using System.Collections.Generic;
using System.Reflection;
using FishNet.Object;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative BreakablePlatform: the server times the crack and the break (the singleplayer timer
    /// would run per peer) and broadcasts them to every peer.
    /// </summary>
    [RequireComponent(typeof(BreakablePlatform))]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedBreakablePlatform : NetworkBehaviour
    {
        private const float DEFAULT_BREAK_TIME = 15f;
        private const float DEFAULT_SEMI_BREAK_FRACTION = 0.5f;

        private BreakablePlatform _wrapped;

        private static MethodInfo _semiBreakMethod;
        private static MethodInfo _breakMethod;
        private static FieldInfo _isBreakingField;
        private static FieldInfo _breakTimeField;
        private static FieldInfo _semiBreakPctField;
        private static bool _reflectionCached;

        private int _currentCharacters;
        private ITimer _semiBreakTimer;
        private ITimer _breakTimer;
        private bool _isBreaking;

        // 0 intact, 1 cracked (semi-break), 2 broken - for the host migration snapshot.
        private int _stage;

        private static void EnsureReflectionCached()
        {
            if (_reflectionCached) return;
            const BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Instance;
            _semiBreakMethod  = typeof(BreakablePlatform).GetMethod("SemiBreakPlatform", Flags);
            _breakMethod      = typeof(BreakablePlatform).GetMethod("BreakPlatform", Flags);
            _isBreakingField  = typeof(BreakablePlatform).GetField("isBreaking", Flags);
            _breakTimeField   = typeof(BreakablePlatform).GetField("breakTime", Flags);
            _semiBreakPctField = typeof(BreakablePlatform).GetField("semiBreakTimePercentage", Flags);
            _reflectionCached = true;
        }

        private void Awake()
        {
            _wrapped = GetComponent<BreakablePlatform>();
            EnsureReflectionCached();

            // BreakablePlatform checks this field in both trigger callbacks: its timer path stays off on every peer.
            _isBreakingField?.SetValue(_wrapped, true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized || _isBreaking) return;
            if (other.GetComponent<StatsController>() == null) return;

            _currentCharacters++;
            if (_currentCharacters == 1)
                StartOrResumeBreakTimers();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServerInitialized || _isBreaking) return;
            if (other.GetComponent<StatsController>() == null) return;

            _currentCharacters = Mathf.Max(0, _currentCharacters - 1);
            if (_currentCharacters == 0)
                PauseBreakTimers();
        }

        public override void OnStopNetwork()
        {
            base.OnStopNetwork();
            MatchTimers.Stop(ref _semiBreakTimer);
            MatchTimers.Stop(ref _breakTimer);
        }

        [ObserversRpc]
        private void RpcPlayContact()
        {
            GameplayAudio.Play(GameAudioCues.CommonPlatformCrack, transform.position);
        }

        private void StartOrResumeBreakTimers()
        {
            if (_breakTimer != null)
            {
                _semiBreakTimer?.ResumeTimer();
                _breakTimer.ResumeTimer();
                return;
            }

            RpcPlayContact();
            float breakTime  = _breakTimeField   != null ? (float)_breakTimeField.GetValue(_wrapped)   : DEFAULT_BREAK_TIME;
            float semiPct    = _semiBreakPctField != null ? (float)_semiBreakPctField.GetValue(_wrapped) : DEFAULT_SEMI_BREAK_FRACTION;

            _semiBreakTimer = MatchTimers.After(breakTime * semiPct, OnSemiBreakTimerComplete);
            _breakTimer     = MatchTimers.After(breakTime,           OnBreakTimerComplete);
        }

        private void PauseBreakTimers()
        {
            _semiBreakTimer?.PauseTimer();
            _breakTimer?.PauseTimer();
        }

        private void OnSemiBreakTimerComplete()
        {
            _semiBreakTimer = null;
            if (this != null && base.IsServerInitialized) RpcSemiBreak();
        }

        private void OnBreakTimerComplete()
        {
            _breakTimer = null;
            if (this != null && base.IsServerInitialized) RpcBreak();
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcSemiBreak(bool playAudio = true)
        {
            if (_wrapped == null || _stage >= 1) return;
            _stage = 1;
            if (playAudio) GameplayAudio.Play(GameAudioCues.CommonPlatformCrack, transform.position);
            _semiBreakMethod?.Invoke(_wrapped, null);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcBreak(bool playAudio = true)
        {
            _isBreaking = true;
            if (_wrapped == null || _stage >= 2) return;
            _stage = 2;
            if (playAudio) GameplayAudio.Play(GameAudioCues.CommonPlatformSplinter, transform.position);
            _isBreakingField?.SetValue(_wrapped, true);
            _breakMethod?.Invoke(_wrapped, null);
        }

        private static List<NetworkedBreakablePlatform> AllInHierarchyOrder()
        {
            var list = new List<NetworkedBreakablePlatform>(FindObjectsByType<NetworkedBreakablePlatform>(FindObjectsSortMode.None));
            var keys = new Dictionary<NetworkedBreakablePlatform, string>();
            foreach (var p in list) keys[p] = NetworkedWorldBreakables.PathKey(p.transform);
            list.Sort((a, b) => string.CompareOrdinal(keys[a], keys[b]));
            return list;
        }

        /// <summary>Server: how far each platform got, in hierarchy order.</summary>
        public static byte[] DescribeStages()
        {
            var list = AllInHierarchyOrder();
            var stages = new byte[list.Count];
            for (int i = 0; i < list.Count; i++) stages[i] = (byte)list[i]._stage;
            return stages;
        }

        /// <summary>Server, migrated match: crack/break the platforms that were, on every peer.</summary>
        public static void ServerRestoreStages(byte[] stages)
        {
            if (stages == null) return;
            var list = AllInHierarchyOrder();
            if (list.Count != stages.Length)
            {
                MpCanary.Mark($"Breakable platforms: snapshot has {stages.Length}, map has {list.Count} - not restored");
                return;
            }
            for (int i = 0; i < list.Count; i++)
            {
                var platform = list[i];
                if (!platform.IsServerInitialized) continue;
                // The singleplayer break expects the crack first.
                if (stages[i] >= 1) platform.RpcSemiBreak(false);
                if (stages[i] >= 2) platform.RpcBreak(false);
            }
        }
    }
}
