using UnityEngine;
using FastAndFractured;

namespace FastAndFractured.Multiplayer
{
    public struct VehicleInputData
    {
        public Vector2 MoveInput;
        public float AccelerateInput;
        public bool HandbrakeInput;
        public bool DashInput;
    }

    /// <summary>
    /// Multiplayer input provider: the owner reads the local PlayerInputProvider, other peers the replicated input.
    /// Exposes everything PlayerInputProvider does, so no cast is needed.
    /// </summary>
    public class NetworkInputProvider : MonoBehaviour, IInputProvider
    {
        private VehicleInputData _currentInput;
        private PlayerInputProvider _localProvider;

        // Shooting input (replicated for non-owners)
        private bool _networkNormalShooting;
        private bool _networkPushShooting;
        private bool _networkFirePushAction;
        private bool _networkThrowingMine;
        private Vector3 _networkAimDirection;

        public void Initialize() { }

        public void Initialize(PlayerInputProvider localProvider)
        {
            _localProvider = localProvider;
        }

        public void SetNetworkInput(VehicleInputData input)
        {
            _currentInput = input;
        }

        public void SetNetworkShootInput(bool normalShoot, bool pushShoot, bool firePush, bool mine, Vector3 aimDir)
        {
            _networkNormalShooting = normalShoot;
            _networkPushShooting = pushShoot;
            _networkFirePushAction = firePush;
            _networkThrowingMine = mine;
            _networkAimDirection = aimDir;
        }

        // IInputProvider
        public Vector2 MoveInput => _localProvider != null ? _localProvider.MoveInput : _currentInput.MoveInput;
        public bool IsBraking => _localProvider != null ? _localProvider.IsBraking : _currentInput.HandbrakeInput;
        public bool IsDashing => _localProvider != null ? _localProvider.IsDashing : _currentInput.DashInput;

        public float IsAccelerating => _localProvider != null ? _localProvider.IsAccelerating : Mathf.Max(0f, _currentInput.AccelerateInput);
        public float IsReversing => _localProvider != null ? _localProvider.IsReversing : Mathf.Max(0f, -_currentInput.AccelerateInput);
        public bool IsNormalShooting => _localProvider != null ? _localProvider.IsNormalShooting : _networkNormalShooting;
        public bool IsPushShooting => _localProvider != null ? _localProvider.IsPushShooting : _networkPushShooting;
        public bool FirePushAction => _localProvider != null ? _localProvider.FirePushAction : _networkFirePushAction;
        public bool IsThrowingMine => _localProvider != null ? _localProvider.IsThrowingMine : _networkThrowingMine;
        public Vector3 AimDirection => _localProvider != null ? _localProvider.AimDirection : _networkAimDirection;

        /// <summary>True if this provider is backed by a local PlayerInputProvider (i.e., this is the owner).</summary>
        public bool HasLocalProvider => _localProvider != null;
    }
}
