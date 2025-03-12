using UnityEngine;

public class TestCamera : MonoBehaviour
{
    [Header("Configuración de la cámara")]
    public Transform target;             // Referencia al coche
    public Vector3 offset = new Vector3(0f, 5f, -10f); // Offset fijo para la posición de la cámara
    public float smoothSpeed = 0.125f;     // Velocidad de interpolación para seguir al target
    public float mouseSensitivity = 3f;    // Sensibilidad para la rotación con el ratón
    public float minPitch = -30f;          // Ángulo mínimo de pitch
    public float maxPitch = 60f;           // Ángulo máximo de pitch

    private float yaw;                   // Rotación horizontal de la cámara
    private float pitch;                 // Rotación vertical de la cámara
    private Vector3 followPosition;      // Punto de seguimiento calculado (no afecta la rotación del target)

    void Start()
    {
        // Inicializamos la posición de seguimiento al inicio con la posición del target
        followPosition = target.position;
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void LateUpdate()
    {
        // Capturamos la entrada del ratón
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Actualizamos la rotación de la cámara únicamente si hay movimiento del ratón
        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            yaw += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // La posición de seguimiento se interpola suavemente hacia la posición actual del target
        followPosition = Vector3.Lerp(followPosition, target.position, smoothSpeed);

        // Calculamos la rotación basada en los ángulos acumulados (solo se actualizan con el ratón)
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        // La posición deseada de la cámara es el punto de seguimiento más el offset rotado por la rotación de la cámara
        Vector3 desiredPosition = followPosition + rotation * offset;

        transform.position = desiredPosition;
        transform.LookAt(followPosition);
    }
}
