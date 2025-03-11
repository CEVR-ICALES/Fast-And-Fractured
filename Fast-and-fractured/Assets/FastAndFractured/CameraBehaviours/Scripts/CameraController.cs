using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    #region Inspector Variables
    [Header("Player and Virtual Camera")]
    public Transform player;
    public CinemachineVirtualCamera virtualCamera;

    [Header("Camera Settings")]
    public float distance = 3f;
    public float height = 2f;
    public float mouseSensitivity = 100f;

    [Header("Recenter Settings")]
    public float recenterTime = 2f;
    public float recenterSpeed = 1f;

    //Minimal and maximum vertical angle
    [Header("Rotation Limitations")]
    public float minPitch = -10f;
    public float maxPitch = 45f;
    #endregion

    #region Private Variables
    private float _yaw;
    private float _pitch;
    private float _defaultYaw;
    private float _timeSinceLastInput = 0f;
    #endregion

    void Start()
    {
        if (player != null)
        {
            _defaultYaw = player.eulerAngles.y;
            _yaw = _defaultYaw;
        }
        _pitch = 10f;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (virtualCamera != null)
            virtualCamera.Follow = transform;
    }

    void LateUpdate()
    {
        if (player == null)
            return;
        UpdateCameraRotation();
        UpdateCameraPosition();
    }

    /// <summary>
    /// Updates camera rotation based on mouse input and resets camera position if there is no input in X seconds
    /// Calls RecenterCamera() if no input is detected for X period of time
    /// </summary>
    private void UpdateCameraRotation()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        bool hasInput = Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f;

        if (hasInput)
        {
            _yaw += mouseX;
            _pitch -= mouseY;
            _timeSinceLastInput = 0f;
        }
        else
        {
            RecenterCamera();
        }

        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
    }

    /// <summary>
    /// Calculates and updates the camera position
    /// </summary>
    private void UpdateCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        Vector3 desiredPosition = player.position + offset + Vector3.up * height;

        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 5f);

        transform.LookAt(player.position + Vector3.up * (height * 0.5f));
    }

    /// <summary>
    /// Recenters the camera to align with the player forward direction after X seconds of inactivity
    /// </summary>
    private void RecenterCamera()
    {
        _timeSinceLastInput += Time.deltaTime;

        if (_timeSinceLastInput > recenterTime)
        {
            _defaultYaw = player.eulerAngles.y;
            _yaw = Mathf.Lerp(_yaw, _defaultYaw, Time.deltaTime * recenterSpeed);
        }
    }
}