using UnityEngine;
using FastAndFractured;
using Unity.Cinemachine;
public class ScreenShakeSourceController : MonoBehaviour
{
    [SerializeField]
    private ScreenShakeProfile[] screenShakeProfiles;
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

    public void PlayGlobalShake(CameraBehaviours cameraBehaviour)
    {
        cameraBehaviour?.ShakeCamera(cinemachineImpulseSource);
    }

    public void PlayGlobalShakeFromProfile(CameraBehaviours cameraBehaviours, ScreenShakeProfileType profileType)
    {
        ScreenShakeProfile screenShakeProfile = GetScreenShakeProfileByType(profileType);
        if (screenShakeProfile == null)
        {
            return;
        }
        cameraBehaviours?.ShakeCameraFromProfile(screenShakeProfile,cinemachineImpulseSource);
    }

    public void PlayLocalShake(CameraBehaviours cameraBehaviours)
    {
        cameraBehaviours?.ShakeLocalCamera(cinemachineImpulseSource);
    }

    public void PlayLocalShakeFromProfile(CameraBehaviours cameraBehaviours,ScreenShakeProfileType profileType)
    {
        ScreenShakeProfile screenShakeProfile = GetScreenShakeProfileByType(profileType);
        if(screenShakeProfile == null)
        {
            return;
        }
        cameraBehaviours?.ShakeLocalCameraFromProfile(screenShakeProfile,cinemachineImpulseSource);
    }

    private ScreenShakeProfile GetScreenShakeProfileByType(ScreenShakeProfileType profileType)
    {
        foreach(ScreenShakeProfile screenShakeProfile in screenShakeProfiles)
        {
            if(screenShakeProfile.profileType == profileType)
                return screenShakeProfile;
        }
        return null;
    }
}
