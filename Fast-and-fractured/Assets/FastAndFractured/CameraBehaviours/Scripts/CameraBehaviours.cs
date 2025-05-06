using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;


namespace FastAndFractured
{
    public class CameraBehaviours : AbstractSingleton<CameraBehaviours>, IPausable
    {
        [SerializeField] CinemachineFreeLook freeLookCamera;
        private PlayerInputController _inputController;

        private bool _paused = false;

        private float _cameraSpeedX;
        private float _cameraSpeedY;



        void OnEnable()
        {
            _inputController = PlayerInputController.Instance;
            _cameraSpeedX = freeLookCamera.m_XAxis.m_MaxSpeed;
            _cameraSpeedY = freeLookCamera.m_YAxis.m_MaxSpeed;
            PauseManager.Instance.RegisterPausable(this);
        }

        void OnDisable()
        {
            PauseManager.Instance.UnregisterPausable(this);
        }

        void Update()
        {
            if (_inputController != null)
            {
                if (_inputController.IsUsingController)
                {
                    UpdateCameraMovement();
                }
            }
        }

        private void UpdateCameraMovement()
        {
            if (_paused) return;

            if (freeLookCamera != null && PlayerInputController.Instance.CameraInput != Vector2.zero)
            {
                // to do invert depending on user settings
                float newXAxisValue = freeLookCamera.m_XAxis.Value + _inputController.CameraInput.x * freeLookCamera.m_XAxis.m_MaxSpeed * Time.deltaTime;
                float newYAxisValue = freeLookCamera.m_YAxis.Value + -_inputController.CameraInput.y * freeLookCamera.m_YAxis.m_MaxSpeed * Time.deltaTime;

                newYAxisValue = Mathf.Clamp(newYAxisValue, 0f, 1f);

                freeLookCamera.m_XAxis.Value = newXAxisValue;
                freeLookCamera.m_YAxis.Value = newYAxisValue;
            }

        }

        public void ResetCameraPosition()
        {
            if (_paused) return;

            if (freeLookCamera != null)
            {
                freeLookCamera.m_YAxisRecentering.m_enabled = true;
                freeLookCamera.m_RecenterToTargetHeading.m_enabled = true;
                _inputController.IsResettingCamera = true;

                TimerSystem.Instance.CreateTimer(3f, onTimerDecreaseComplete: () =>
                {
                    freeLookCamera.m_YAxisRecentering.m_enabled = false;
                    freeLookCamera.m_RecenterToTargetHeading.m_enabled = false;
                    _inputController.IsResettingCamera = false;
                });
            }
        }

        public void OnPause()
        {
            freeLookCamera.m_XAxis.m_MaxSpeed = 0f;
            freeLookCamera.m_YAxis.m_MaxSpeed = 0f;
            _paused = true;
        }

        public void OnResume()
        {
            freeLookCamera.m_XAxis.m_MaxSpeed = _cameraSpeedX;
            freeLookCamera.m_YAxis.m_MaxSpeed = _cameraSpeedY;
            _paused = false;
        }
    }
}
