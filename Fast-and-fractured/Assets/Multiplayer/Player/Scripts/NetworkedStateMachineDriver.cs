using StateMachine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Runs the singleplayer state machine Controller from NetworkedVehicle.Replicate instead of Update, so its
    /// physics happens inside the tick and replays with it.
    /// </summary>
    [RequireComponent(typeof(Controller))]
    public class NetworkedStateMachineDriver : MonoBehaviour
    {
        private Controller _controller;
        private bool _ready;

        public bool IsReady => _ready;

        private void Awake()
        {
            _controller = GetComponent<Controller>();

            if (SingleplayerReflectionCache.Controller_UpdateStateActions == null ||
                SingleplayerReflectionCache.Controller_EvaluateStateTransitions == null)
            {
                Debug.LogError("[NetworkedStateMachineDriver] Failed to resolve Controller methods via reflection. State machine will not tick.", this);
                return;
            }

            _ready = true;
        }

        private bool _takenOver;
        private bool _loggedReassert;

        public void TakeOverFromUpdate()
        {
            _takenOver = true;
            if (_controller != null)
                _controller.enabled = false;
        }

        /// <summary>Undoes TakeOverFromUpdate for the owner's frame-rate state machines (shooting).</summary>
        public void ReleaseToUpdate()
        {
            _takenOver = false;
            if (_controller != null)
                _controller.enabled = true;
        }

        /// <summary>GameLoopManager re-enables every Controller when the countdown ends: re-assert the takeover each frame.</summary>
        private void LateUpdate()
        {
            if (!_takenOver || _controller == null || !_controller.enabled)
                return;

            _controller.enabled = false;
            if (!_loggedReassert)
            {
                _loggedReassert = true;
                Debug.Log($"[NetworkedStateMachineDriver] Re-disabled Controller on '{gameObject.name}' " +
                          "(GameLoopManager.ActivateCharacterControls re-enabled it). The state machine " +
                          "must only tick from the network tick.", this);
            }
        }

        /// <summary>Runs the current state's actions and transitions; every tick, replays included.</summary>
        public void TickStateMachine(uint tick)
        {
            if (!_ready || _controller == null) return;
            if (MatchStartGate.IsLocked(tick)) return;   // countdown still running

            var state = _controller.GetCurrentState();
            if (state == null) return;

            // State transitions go to the canary: the last one before the log stops is the hanging state.
            if (!ReferenceEquals(state, _lastLoggedState))
            {
                _lastLoggedState = state;
                MpCanary.Mark($"SM '{gameObject.name}' -> {state.name}");
            }

            SingleplayerReflectionCache.Controller_UpdateStateActions.Invoke(_controller, null);
            SingleplayerReflectionCache.Controller_EvaluateStateTransitions.Invoke(_controller, null);
        }

        private Object _lastLoggedState;
    }
}
