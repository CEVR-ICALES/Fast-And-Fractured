using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

public class FogMovement : MonoBehaviour
{
    public GameObject fogParent;
    public LocalVolumetricFog mainFog;
    public LocalVolumetricFog secondaryFog;

    public float growthSpeed = 1f;

    [SerializeField] private Vector3 spawnPosition;
    [SerializeField] private Vector3 mirrorPosition;
    private Vector3 direction;

    private float currentGrowth = 0f;

    private Vector3 initialVolumeSizeMain;
    private Vector3 initialVolumeSizeSecondary;

    private Vector3 initialPositionMain;
    private Vector3 initialPositionSecondary;

    private void Start()
    {
        SpawnFogs();
    }

    private void Update()
    {
        GrowFogs();
    }

    private void SpawnFogs()
    {
        float randomX = Random.Range(-15f, 15f);
        float randomZ = Random.Range(-15f, 15f);
        spawnPosition = new Vector3(randomX, fogParent.transform.position.y, randomZ);

        fogParent.transform.position = spawnPosition;

        mirrorPosition = -spawnPosition;

        direction = (mirrorPosition - spawnPosition).normalized;

        mainFog.gameObject.SetActive(true);
        secondaryFog.gameObject.SetActive(true);

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        fogParent.transform.rotation = targetRotation;

        initialVolumeSizeMain = mainFog.parameters.size;
        initialVolumeSizeSecondary = secondaryFog.parameters.size;

        initialPositionMain = mainFog.transform.position;
        initialPositionSecondary = secondaryFog.transform.position;
    }

    private void GrowFogs()
    {
        float maxGrowth = 2f * spawnPosition.magnitude;

        if (currentGrowth < maxGrowth)
        {
            currentGrowth += growthSpeed * Time.deltaTime;
                
            if (currentGrowth > maxGrowth)
                currentGrowth = maxGrowth;
        }

        float newZSizeMain = initialVolumeSizeMain.z + currentGrowth;
        float newZSizeSecondary = initialVolumeSizeSecondary.z + currentGrowth;

        mainFog.parameters.size = new Vector3(initialVolumeSizeMain.x, initialVolumeSizeMain.y, newZSizeMain);
        secondaryFog.parameters.size = new Vector3(initialVolumeSizeSecondary.x, initialVolumeSizeSecondary.y, newZSizeSecondary);

        Vector3 offset = direction * (currentGrowth / 2f);

        mainFog.transform.position = spawnPosition + offset;
        secondaryFog.transform.position = spawnPosition + offset;
    }
}
