using System.Collections;
using System.Reflection;
using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Server-authoritative wrapper for BreakablePlatform (used in ConstructionMap).
    ///
    /// The singleplayer component uses TimerSystem.Instance (a singleplayer scene singleton)
    /// to schedule semi-break and full-break events. In multiplayer each client would start
    /// its own independent timer, causing platforms to break at different times per client —
    /// resulting in players falling through on one machine but not another.
    ///
    /// This wrapper neutralizes BreakablePlatform's local trigger path and drives the timer
    /// via server coroutines. RpcSemiBreak and RpcBreak broadcast visual/physics state
    /// changes so all peers apply them simultaneously.
    ///
    /// Setup: add this component and a NetworkObject to each BreakablePlatform GameObject
    /// in the scene. The NetworkObject is required because we broadcast RPCs.
    /// </summary>
    [RequireComponent(typeof(BreakablePlatform))]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedBreakablePlatform : NetworkBehaviour
    {
        private BreakablePlatform _wrapped;

        // Cached reflection handles for BreakablePlatform's private state.
        private static MethodInfo _semiBreakMethod;
        private static MethodInfo _breakMethod;
        private static FieldInfo _isBreakingField;
        private static FieldInfo _breakTimeField;
        private static FieldInfo _semiBreakPctField;
        private static bool _reflectionCached;

        private int _currentCharacters;
        private Coroutine _semiBreakCoroutine;
        private Coroutine _breakCoroutine;
        private bool _isBreaking;

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

            // BreakablePlatform checks this field at the top of both trigger callbacks. Keep
            // the component enabled so Start initializes its visual references, but permanently
            // neutralize its singleplayer timer path on every multiplayer peer.
            _isBreakingField?.SetValue(_wrapped, true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized || _isBreaking) return;
            if (other.GetComponent<StatsController>() == null) return;

            _currentCharacters++;
            if (_currentCharacters == 1)
                StartBreakTimers();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!base.IsServerInitialized || _isBreaking) return;
            if (other.GetComponent<StatsController>() == null) return;

            _currentCharacters = Mathf.Max(0, _currentCharacters - 1);
            if (_currentCharacters == 0)
                StopBreakTimers();
        }

        private void StartBreakTimers()
        {
            float breakTime  = _breakTimeField   != null ? (float)_breakTimeField.GetValue(_wrapped)   : 15f;
            float semiPct    = _semiBreakPctField != null ? (float)_semiBreakPctField.GetValue(_wrapped) : 0.5f;

            _semiBreakCoroutine = StartCoroutine(BreakAfterDelay(breakTime * semiPct, semiBreak: true));
            _breakCoroutine     = StartCoroutine(BreakAfterDelay(breakTime,           semiBreak: false));
        }

        private void StopBreakTimers()
        {
            if (_semiBreakCoroutine != null) { StopCoroutine(_semiBreakCoroutine); _semiBreakCoroutine = null; }
            if (_breakCoroutine     != null) { StopCoroutine(_breakCoroutine);     _breakCoroutine     = null; }
        }

        private IEnumerator BreakAfterDelay(float delay, bool semiBreak)
        {
            yield return new WaitForSeconds(delay);
            if (semiBreak)
                RpcSemiBreak();
            else
                RpcBreak();
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcSemiBreak()
        {
            if (_wrapped == null) return;
            _semiBreakMethod?.Invoke(_wrapped, null);
        }

        [ObserversRpc(RunLocally = true)]
        private void RpcBreak()
        {
            _isBreaking = true;
            if (_wrapped == null) return;
            _isBreakingField?.SetValue(_wrapped, true);
            _breakMethod?.Invoke(_wrapped, null);
        }
    }
}
