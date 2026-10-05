using StateMachine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Drives the singleplayer state machine Controller from the FishNet network tick instead of
    /// Unity's Update. This decouples the state machine from frame rate so all state-machine-driven
    /// physics (ApplyMovementAction, HandleAutoDeceleration, dash transitions, etc.) run inside
    /// the same tick boundary as NetworkedVehicle.Replicate.
    ///
    /// Without this driver, Controller.Update fires at frame rate (60Hz+) while Replicate fires at
    /// the network tick rate, causing duplicate ProcessMovementInput calls and physics applied
    /// outside the prediction snapshot. With it, the state machine becomes part of the deterministic
    /// tick loop and replays during reconcile re-execute its logic correctly.
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

        /// <summary>
        /// Suppresses Controller.Update by disabling the component. The state machine's Awake/Start
        /// has already run by this point so its current state is initialized.
        /// </summary>
        public void TakeOverFromUpdate()
        {
            _takenOver = true;
            if (_controller != null)
                _controller.enabled = false;
        }

        /// <summary>
        /// Undoes TakeOverFromUpdate: the Controller runs from Unity's Update again and the
        /// LateUpdate re-assert stops. Used for the owner's child state machines (the shooting
        /// Controller), which must run at frame rate exactly like singleplayer — nothing ticks
        /// them from the network loop.
        /// </summary>
        public void ReleaseToUpdate()
        {
            _takenOver = false;
            if (_controller != null)
                _controller.enabled = true;
        }

        /// <summary>
        /// GameLoopManager.ActivateCharacterControls (SP flow) re-enables every character's
        /// Controller when the match countdown ends, which silently undoes TakeOverFromUpdate.
        /// The state machine then runs at frame rate (Controller.Update) AND per network tick
        /// (NetworkedVehicle.Replicate) simultaneously: forces double-apply, cars end up faster
        /// than singleplayer and remote cars jitter as the local frame-rate simulation fights
        /// the replicated one. Re-assert the takeover every frame — it's a single bool check.
        /// </summary>
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

        /// <summary>
        /// Tick the state machine: run current state's actions and evaluate transitions.
        /// Called from NetworkedVehicle.Replicate on every tick (including replays during reconcile).
        /// </summary>
        public void TickStateMachine(uint tick)
        {
            if (!_ready || _controller == null) return;
            if (MatchStartGate.IsLocked(tick)) return;   // countdown still running

            var state = _controller.GetCurrentState();
            if (state == null) return;

            // Diagnostic: the canary file is the only record that survives a main-thread hang, and
            // state transitions are rare enough to log every one. The last state written before the
            // log stops is the state whose actions or transitions are hanging.
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
