using System.Collections;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class CameraBehaviours : AbstractSingleton<CameraBehaviours>
    {
        [SerializeField] CinemachineFreeLook freeLookCamera;
        private PlayerInputController _inputController;
    

        void OnEnable()
        {
            
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

        public void SetCameraParameters(Transform follow, Transform lookAt)
        {
            _inputController = PlayerInputController.Instance;
            freeLookCamera.Follow = follow;
            freeLookCamera.LookAt = lookAt;
        }

        private void UpdateCameraMovement()
        {
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
    }
}
