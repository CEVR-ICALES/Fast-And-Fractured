using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class SkyRotation : MonoBehaviour
{
    private const float FULL_ROTATION = 360f;

    [SerializeField] private float rotationSpeed = 1f;

    private PhysicallyBasedSky sky;
    private float rotationY;

    private void Start()
    {
        Volume[] volumes = FindObjectsByType<Volume>(FindObjectsSortMode.None);

        foreach (Volume volume in volumes)
        {
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
            Debug.LogError("SkyRotation: No active Physically Based Sky was found.");
    }

    private void Update()
    {
        if (sky == null)
            return;

        rotationY += rotationSpeed * Time.deltaTime;

        if (rotationY >= FULL_ROTATION)
            rotationY -= FULL_ROTATION;

        Vector3 rotation = sky.spaceRotation.value;
        rotation.y = rotationY;

        sky.spaceRotation.value = rotation;
    }
}