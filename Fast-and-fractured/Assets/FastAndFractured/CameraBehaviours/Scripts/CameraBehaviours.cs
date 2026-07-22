using System.Collections;
using System.Collections.Generic;
using Enums;
using Unity.Cinemachine;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;


namespace FastAndFractured
{
    public class CameraBehaviours : AbstractSingleton<CameraBehaviours>, IPausable
    {
        [SerializeField] CinemachineFreeLook freeLookCamera;
        private PlayerInputController _inputController;
        private StatsController _statsController;

        private bool _paused = false;

        private float _cameraSpeedX;
        private float _cameraSpeedY;

        [Header("Camera Shake")]
        [SerializeField] 
        private CinemachineImpulseListener commonCinemachineImpulseListener;
        [SerializeField]
        private CinemachineImpulseListener localCinemachineImpulseListener;
        [SerializeField]
        private ScreenShakeProfile defaultProfile;
        private CinemachineImpulseDefinition _cinemachineImpulseDefinition;
        // [SerializeField]
        // private CinemachineImpulseChannels _cinemachineImpulseChanels;
        protected override void Initialize()
        {
            base.Initialize();
            LevelControllerButBetter.Instance.onLevelPreStart.AddListener(
                () => TimerSystem.Instance.CreateTimer(0.01f, onTimerDecreaseComplete: () =>
            {
                _statsController = LevelControllerButBetter.Instance.LocalPlayer.GetComponent<StatsController>();
            }));
            if(commonCinemachineImpulseListener==null){
            commonCinemachineImpulseListener = GetComponent<CinemachineImpulseListener>();
            }
        }

        void OnEnable()
        {
            _inputController = PlayerInputController.Instance;
            _cameraSpeedX = freeLookCamera.m_XAxis.m_MaxSpeed;
            _cameraSpeedY = freeLookCamera.m_YAxis.m_MaxSpeed;
            PauseManager.Instance.RegisterPausable(this);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
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

            // if (_inputController.CameraMouseInput != Vector2.zero)
            // {
            //     HUDManager.Instance.UpdateUIElement(UIDynamicElementType.SHOOTING_CROSSHAIR, !(Mathf.Abs(freeLookCamera.m_XAxis.Value) > _statsController.NormalShootAngle / 2));
            // }
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

                // HUDManager.Instance.UpdateUIElement(UIDynamicElementType.SHOOTING_CROSSHAIR, !(Mathf.Abs(freeLookCamera.m_XAxis.Value) > _statsController.NormalShootAngle / 2));
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

        #region Camera Effects
        public void ShakeCamera(CinemachineImpulseSource impulseSource)
        {
            SetupDefaultScreenShakeListenerSettings(commonCinemachineImpulseListener);
            impulseSource.GenerateImpulseWithForce(defaultProfile.impactForce);
        }

        public void ShakeCameraFromProfile(ScreenShakeProfile screenShakeProfile, CinemachineImpulseSource impulseSource)
        {
            SetupScreenShakeSettings(screenShakeProfile, impulseSource,commonCinemachineImpulseListener);
            impulseSource.GenerateImpulseWithForce(screenShakeProfile.impactForce);
        }

        public void ShakeLocalCamera(CinemachineImpulseSource impulseSource)
        {
            SetupDefaultScreenShakeListenerSettings(localCinemachineImpulseListener);
            impulseSource.GenerateImpulseWithForce(defaultProfile.impactForce);
        }

        public void ShakeLocalCameraFromProfile(ScreenShakeProfile screenShakeProfile, CinemachineImpulseSource impulseSource)
        {
            SetupScreenShakeSettings(screenShakeProfile, impulseSource,localCinemachineImpulseListener);
            impulseSource.GenerateImpulseWithForce(screenShakeProfile.impactForce);
        }

        private void SetupScreenShakeSettings(ScreenShakeProfile screenShakeProfile, CinemachineImpulseSource impulseSource, CinemachineImpulseListener cinemachineImpulseListener)
        {
            //Source 
           _cinemachineImpulseDefinition = impulseSource.ImpulseDefinition;
           _cinemachineImpulseDefinition.ImpulseDuration = screenShakeProfile.impactTime;
           impulseSource.DefaultVelocity = screenShakeProfile.defaultVelocity;
           _cinemachineImpulseDefinition.CustomImpulseShape = screenShakeProfile.customImpulseShape;
            //Listener
            cinemachineImpulseListener.ReactionSettings.AmplitudeGain = screenShakeProfile.listenerAmplitude;
            cinemachineImpulseListener.ReactionSettings.FrequencyGain = screenShakeProfile.listenerFrequency;
            cinemachineImpulseListener.ReactionSettings.Duration = screenShakeProfile.listenerDuration;
        }

        private void SetupDefaultScreenShakeListenerSettings(CinemachineImpulseListener cinemachineImpulseListener)
        {
            cinemachineImpulseListener.ReactionSettings.AmplitudeGain = defaultProfile.listenerAmplitude;
            cinemachineImpulseListener.ReactionSettings.FrequencyGain = defaultProfile.listenerFrequency;
            cinemachineImpulseListener.ReactionSettings.Duration = defaultProfile.listenerDuration;
        }
        #endregion
    }
}
