using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Cinemachine Virtual Camera")]
    public CinemachineVirtualCamera virtualCamera;

    [Header("Camera Offsets")]
    public float normalOffsetZ = -5f;
    public float normalOffsetY = 2f;
    public float aimOffsetX = 2f;
    public float aimSmoothing = 5f;

    [Header("Targetting")]
    public Transform player;
    public CinemachineTargetGroup targetGroup;

    [Header("Camera Shake Variables")]
    public float shakeIntensity = 2f;
    public float shakeDuration = 0.2f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 100f;
    public Transform playerTransform;

    private float _yaw = 0f;
    private float _pitch = 0f;

    private Vector3 _originalOffset;
    private Vector3 _currentRotation;
    private Vector3 _rotationSmoothVelocity;

    private CinemachineComposer _composer;
    private CinemachineFramingTransposer _transposer;
    private CinemachineBasicMultiChannelPerlin _noise;

    void Start()
    {
        _transposer = virtualCamera.GetCinemachineComponent<CinemachineFramingTransposer>();
        _composer = virtualCamera.GetCinemachineComponent<CinemachineComposer>();
        _originalOffset = new Vector3(0, normalOffsetY, normalOffsetZ);

        _noise = virtualCamera.GetCinemachineComponent<CinemachineBasicMultiChannelPerlin>();

        if (virtualCamera.Follow == null && player != null)
        {
            virtualCamera.Follow = player;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        HandleAiming();
        HandleMouseLook();
    }

    public void HandleAiming()
    {
        bool isAiming = Input.GetButton("Fire2");

        Vector3 targetOffset = isAiming
            ? new Vector3(aimOffsetX, normalOffsetY, normalOffsetZ)
            : _originalOffset;

        _transposer.m_TrackedObjectOffset = Vector3.Lerp(_transposer.m_TrackedObjectOffset, targetOffset, Time.deltaTime * aimSmoothing);
    }

    public void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
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
