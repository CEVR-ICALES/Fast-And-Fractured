using UnityEngine;
using Cinemachine;

public class CameraController : MonoBehaviour
{
    [Header("Player y Cámara Virtual")]
    public Transform player;                      // El objeto que la cámara seguirá (por ejemplo, el coche)
    public CinemachineVirtualCamera virtualCamera; // La Cinemachine Virtual Camera

    [Header("Ajustes de Cámara")]
    public float distance = 3f;       // Distancia detrás del player
    public float height = 2f;         // Altura sobre el player
    public float mouseSensitivity = 100f;

    [Header("Recenter Settings")]
    public float recenterTime = 2f;   // Tiempo de inactividad (en segundos) antes de recentering
    public float recenterSpeed = 1f;  // Velocidad a la que se recentra la cámara

    [Header("Limitaciones de Rotación")]
    public float minPitch = -10f;     // Ángulo vertical mínimo
    public float maxPitch = 45f;      // Ángulo vertical máximo

    // Variables internas para el control de la rotación
    private float yaw;                // Rotación horizontal actual
    private float pitch;              // Rotación vertical actual
    private float defaultYaw;         // Ángulo horizontal predeterminado (tomado de la dirección del player)
    private float timeSinceLastInput = 0f; // Tiempo acumulado sin input

    void Start()
    {
        if (player != null)
        {
            // Inicializamos defaultYaw y yaw con la rotación actual del player
            defaultYaw = player.eulerAngles.y;
            yaw = defaultYaw;
        }
        pitch = 10f; // Valor inicial de pitch (ajústalo según necesites)

        // Bloqueamos y ocultamos el cursor para una experiencia de cámara fluida
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        // Asignamos este objeto (el pivot) como el target de seguimiento de la Virtual Camera
        if (virtualCamera != null)
        {
            virtualCamera.Follow = transform;
        }
    }

    void LateUpdate()
    {
        if (player == null)
            return;

        // Capturamos la entrada del ratón
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        bool hasInput = Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f;

        if (hasInput)
        {
            // Si hay input, actualizamos yaw y pitch y reiniciamos el contador de inactividad
            yaw += mouseX;
            pitch -= mouseY;
            timeSinceLastInput = 0f;
        }
        else
        {
            // Si no hay input, incrementamos el contador de inactividad
            timeSinceLastInput += Time.deltaTime;
            // Si ha pasado el tiempo de inactividad, comenzamos a recenter la cámara
            if (timeSinceLastInput >= recenterTime)
            {
                // Actualizamos defaultYaw con la rotación actual del player
                defaultYaw = player.eulerAngles.y;
                yaw = Mathf.Lerp(yaw, defaultYaw, Time.deltaTime * recenterSpeed);
            }
        }

        // Limitar el pitch para evitar rotaciones extremas
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Calculamos la rotación resultante a partir de yaw y pitch
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Calculamos el offset: partiendo de (0,0,-distance) y rotándolo según la rotación
        Vector3 offset = rotation * new Vector3(0, 0, -distance);

        // Posición deseada: la posición del player + el offset + un ajuste vertical (height)
        Vector3 desiredPosition = player.position + offset + Vector3.up * height;

        // Movemos el pivot suavemente hacia la posición deseada
        transform.position = Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * 5f);

        // Hacemos que el pivot mire al player (con un ajuste vertical para centrar la vista)
        transform.LookAt(player.position + Vector3.up * (height * 0.5f));
    }
}
