using UnityEngine;
using FastAndFractured;
using Unity.Cinemachine;
public class ScreenShakeSourceController : MonoBehaviour
{
    [SerializeField]
    private ScreenShakeProfile screenShakeProfile;
    [SerializeField]
    private CinemachineImpulseSource cinemachineImpulseSource;

    private void Start()
    {
        if(cinemachineImpulseSource == null)
        {
            if(!TryGetComponent<CinemachineImpulseSource>(out cinemachineImpulseSource))
            {
                cinemachineImpulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            }
        }
    }

    public void PlayGlobalShake()
    {
        CameraBehaviours.Instance?.ShakeCamera(cinemachineImpulseSource);
    }

    public void PlayGlobalShakeFromProfile()
    {
        if (screenShakeProfile == null)
        {
            return;
        }
        CameraBehaviours.Instance?.ShakeCameraFromProfile(screenShakeProfile,cinemachineImpulseSource);
    }

    public void PlayLocalShake()
    {
        CameraBehaviours.Instance?.ShakeLocalCamera(cinemachineImpulseSource);
    }

    public void PlayLocalShakeFromProfile()
    {
         if (screenShakeProfile == null)
        {
            return;
        }
        CameraBehaviours.Instance?.ShakeLocalCameraFromProfile(screenShakeProfile,cinemachineImpulseSource);
    }
}
