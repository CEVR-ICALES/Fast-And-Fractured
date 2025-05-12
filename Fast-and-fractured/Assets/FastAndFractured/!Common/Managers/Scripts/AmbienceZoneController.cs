using Enums;
using FastAndFractured;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;
using Utilities;

public class AmbienceZoneController : MonoBehaviour
{
    [SerializeField] private AmbienceZoneType _zoneType;
    [SerializeField] private EventReference ambienceEventReference;

    private EventInstance _ambienceInstance;

    private void Start()
    {
        _ambienceInstance = RuntimeManager.CreateInstance(ambienceEventReference);
        _ambienceInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_ambienceInstance.isValid())
        {
            if (other.TryGetComponent(out StatsController statsController))
            {
                if (statsController.IsPlayer)
                {
                    _ambienceInstance = RuntimeManager.CreateInstance(ambienceEventReference);
                    _ambienceInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
                    _ambienceInstance.start();
                }
            }
        }

        Debug.Log("Entered Zone");
    }

    private void OnTriggerExit(Collider other)
    {
        if (_ambienceInstance.isValid())
        {
            if (other.TryGetComponent(out StatsController statsController))
            {
                if (statsController.IsPlayer)
                {
                    _ambienceInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    _ambienceInstance.release();
                }
            }

            Debug.Log("Left Zone");
        }
    }
}
