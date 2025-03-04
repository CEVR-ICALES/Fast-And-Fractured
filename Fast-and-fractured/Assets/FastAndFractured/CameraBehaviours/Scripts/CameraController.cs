using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    public CinemachineVirtualCamera virtualCamera;

    public float normalOffsetZ = -5f;
    public float normalOffsetY = -5f;
    public float aimOffsetX = 2f;
    public float aimSmoothing = 5f;

    public Transform player;
    public CinemachineTargetGroup targetGroup;

    public float shakeIntensity = 2f;
    public float shakeDuration = .2f;

    private Vector3 _originalOffset;
    private CinemachineFramingTransposer _transposer;
    private CinemachineBasicMultiChannelPerlin _noise;

    void Start()
    {
        _transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        _originalOffset = new Vector3 (0, normalOffsetY,normalOffsetZ);

        _noise = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();
    }

    void Update()
    {
        HandleAiming();
    }

    public void HandleAiming()
    {
        if (Input.GetButton("Fire2"))
        {
            Vector3 aimOffset = new Vector3(aimOffsetX, normalOffsetY, normalOffsetZ);
            _transposer.m_TrackedObjectOffset = Vector3.Lerp(_transposer.m_TrackedObjectOffset, aimOffset, Time.deltaTime * aimSmoothing);
        }
    }

    #region CameraShake Methods
    public void ShakeCamera()
    {
        _noise.m_AmplitudeGain = shakeIntensity;
        Invoke(nameof(ResetShake), shakeDuration);
    }

    public void ResetShake()
    {
        _noise.m_AmplitudeGain = 0;
    }
    #endregion
}
