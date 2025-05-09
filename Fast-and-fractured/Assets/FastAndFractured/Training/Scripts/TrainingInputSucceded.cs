using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
namespace FastAndFractured
{
    public enum InputMechanics
    {
        MOVING,
        MOVING_CAMERA,
        DASH,
        BRAKE,
        DRIFT,
        RECENTERING,
        NORMAL_SHOOT,
        PUSH_SHOOT,
        MINE_SHOOT,
        UNIQUE_ABILITY
    }
    public class TrainingInputSucceded : MonoBehaviour
    {
        private bool _inputPressed = false;
        private bool _isWaitingInput =  false;
        private Action _waitInputAction;

        [SerializeField]
        private InputMechanics inputMechanics;
        [SerializeField]
        private List<TrafficLightLamp> trafficLightLamps;
        private CarMovementController _playerCarMovement;
        private NormalShootHandle _playerNormalShootHandle;
        private PushShootHandle _playerPushShootHandle;
        private BaseUniqueAbility _uniqueAbility;

        public UnityEvent onInputPerformed;

        private void Start()
        {
            switch (inputMechanics)
            {
                case InputMechanics.MOVING:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.MoveInput!=Vector2.zero);
                    };
                    break;
                case InputMechanics.MOVING_CAMERA:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.CameraInput != Vector2.zero);
                    };
                    break;
                case InputMechanics.DASH:
                    _playerCarMovement = PlayerInputController.Instance.gameObject.GetComponentInChildren<CarMovementController>();
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(_playerCarMovement.IsDashing);
                    };
                    break;
                case InputMechanics.BRAKE:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsBraking);
                    };
                    break;
                case InputMechanics.NORMAL_SHOOT:
                    _playerNormalShootHandle = PlayerInputController.Instance.gameObject.GetComponentInChildren<NormalShootHandle>();
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsShooting&&_playerNormalShootHandle.CanShoot);
                    };
                    break;
                case InputMechanics.DRIFT:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsBraking&&PlayerInputController.Instance.MoveInput.x!=0);
                    };
                    break;
                case InputMechanics.RECENTERING:
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsResettingCamera);
                    };
                    break;
                case InputMechanics.PUSH_SHOOT:
                    _playerPushShootHandle = PlayerInputController.Instance.gameObject.GetComponentInChildren<PushShootHandle>();
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsPushShooting);
                    };
                    break;
                case InputMechanics.MINE_SHOOT:
                    _playerPushShootHandle = PlayerInputController.Instance.gameObject.GetComponentInChildren<PushShootHandle>();
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(PlayerInputController.Instance.IsThrowingMine && _playerPushShootHandle.CanShoot);
                    }; break;
                case InputMechanics.UNIQUE_ABILITY:
                    _uniqueAbility = PlayerInputController.Instance.gameObject.GetComponentInChildren<BaseUniqueAbility>();
                    _waitInputAction = () =>
                    {
                        SeeIfInputPressed(_uniqueAbility.isAbilityActive);
                    }; break;
            }
            trafficLightLamps[0].TurnOn();
            trafficLightLamps[1].TurnOff();
        }

        private void Update()
        {
            if (_isWaitingInput)
            {
                _waitInputAction();
            }
        }

        private void SeeIfInputPressed(bool input)
        {
            if (!_inputPressed)
            {
                _inputPressed = input;
                if (input)
                {
                    trafficLightLamps[0].TurnOff();
                    trafficLightLamps[1].TurnOn();
                    onInputPerformed?.Invoke();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.TryGetComponent(out StatsController character))
            {
                if (character.IsPlayer)
                {
                    _isWaitingInput = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out StatsController character))
            {
                if (character.IsPlayer)
                {
                    _isWaitingInput = false;
                }
            }
        }
    }
}
