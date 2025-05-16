using FastAndFractured;
using FMOD.Studio;
using FMODUnity;
using UnityEngine;

public class TriggerAudioController : MonoBehaviour
{
    [SerializeField] private EventReference audioEventReference;

    private EventInstance _audioInstance;

    [SerializeField] private bool isAmbienceZone;

    private void Start()
    {
        _audioInstance = RuntimeManager.CreateInstance(audioEventReference);
        _audioInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out StatsController statsController))
        {
            if (statsController.IsPlayer)
            {
                _audioInstance = RuntimeManager.CreateInstance(audioEventReference);
                _audioInstance.set3DAttributes(RuntimeUtils.To3DAttributes(transform.position));
                _audioInstance.start();

                Debug.LogError("ENTERED ZONE!!!!!!!!!!!!");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.TryGetComponent(out StatsController statsController))
        {
            if (statsController.IsPlayer)
            {
                if (isAmbienceZone)
                {
                    _audioInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
                    _audioInstance.release();
                    Debug.LogError("EXITED ZONE!!!!!!!!!!");
                }
            }
        }
    }
}
