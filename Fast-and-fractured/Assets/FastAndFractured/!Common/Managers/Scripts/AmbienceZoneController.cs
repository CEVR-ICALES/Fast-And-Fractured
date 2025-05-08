using Enums;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Utilities;

public class AmbienceZoneController : MonoBehaviour
{
    public AmbienceZoneType zoneType;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            SoundManager.Instance.SetAmbienceZone((float)zoneType);
    }
}
