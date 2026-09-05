using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SkyRotation : MonoBehaviour
{
    [SerializeField] private float rotationSpeed = 1f;

    private PhysicallyBasedSky sky;
    private float rotationY;

    private void Start()
    {
        // Busca todos los Volumes activos en la escena.
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);

        foreach (Volume volume in volumes)
        {
            // Solo nos interesan los Volumes activos.
            if (!volume.isActiveAndEnabled)
                continue;

            if (volume.profile != null &&
                volume.profile.TryGet(out PhysicallyBasedSky foundSky))
            {
                sky = foundSky;

                rotationY = sky.spaceRotation.value.y;
                sky.spaceRotation.overrideState = true;

                break;
            }
        }

        if (sky == null)
        {
            Debug.LogError("SkyRotate: No se ha encontrado un Physically Based Sky activo.");
        }
    }

    private void Update()
    {
        if (sky == null)
            return;

        rotationY += rotationSpeed * Time.deltaTime;

        if (rotationY >= 360f)
            rotationY -= 360f;

        Vector3 rotation = sky.spaceRotation.value;
        rotation.y = rotationY;

        sky.spaceRotation.value = rotation;
    }
}