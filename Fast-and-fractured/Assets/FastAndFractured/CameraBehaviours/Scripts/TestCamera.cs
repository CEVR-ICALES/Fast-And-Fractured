using UnityEngine;

public class TestCamera : MonoBehaviour
{
    [Header("Configuraci�n de la c�mara")]
    public Transform target;             // Referencia al coche
    public Vector3 offset = new Vector3(0f, 5f, -10f); // Offset fijo para la posici�n de la c�mara
    public float smoothSpeed = 0.125f;     // Velocidad de interpolaci�n para seguir al target
    public float mouseSensitivity = 3f;    // Sensibilidad para la rotaci�n con el rat�n
    public float minPitch = -30f;          // �ngulo m�nimo de pitch
    public float maxPitch = 60f;           // �ngulo m�ximo de pitch

    private float yaw;                   // Rotaci�n horizontal de la c�mara
    private float pitch;                 // Rotaci�n vertical de la c�mara
    private Vector3 followPosition;      // Punto de seguimiento calculado (no afecta la rotaci�n del target)

    void Start()
    {
        // Inicializamos la posici�n de seguimiento al inicio con la posici�n del target
        followPosition = target.position;
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    void LateUpdate()
    {
        // Capturamos la entrada del rat�n
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Actualizamos la rotaci�n de la c�mara �nicamente si hay movimiento del rat�n
        if (Mathf.Abs(mouseX) > 0.01f || Mathf.Abs(mouseY) > 0.01f)
        {
            yaw += mouseX * mouseSensitivity;
            pitch -= mouseY * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        }

        // La posici�n de seguimiento se interpola suavemente hacia la posici�n actual del target
        followPosition = Vector3.Lerp(followPosition, target.position, smoothSpeed);

        // Calculamos la rotaci�n basada en los �ngulos acumulados (solo se actualizan con el rat�n)
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
        // La posici�n deseada de la c�mara es el punto de seguimiento m�s el offset rotado por la rotaci�n de la c�mara
        Vector3 desiredPosition = followPosition + rotation * offset;

        transform.position = desiredPosition;
        transform.LookAt(followPosition);
    }
}
