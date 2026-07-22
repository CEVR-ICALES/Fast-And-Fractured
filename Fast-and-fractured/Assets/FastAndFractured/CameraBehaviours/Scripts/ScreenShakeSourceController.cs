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

    public void PlayGlobalShake()
    {
        CameraBehaviours.Instance?.ShakeCamera(cinemachineImpulseSource);
    }

    public void PlayGlobalShakeFromProfile(ScreenShakeProfileType profileType)
    {
        ScreenShakeProfile screenShakeProfile = GetScreenShakeProfileByType(profileType);
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

    public void PlayLocalShakeFromProfile(ScreenShakeProfileType profileType)
    {
        ScreenShakeProfile screenShakeProfile = GetScreenShakeProfileByType(profileType);
        if(screenShakeProfile == null)
        {
            return;
        }
        CameraBehaviours.Instance?.ShakeLocalCameraFromProfile(screenShakeProfile,cinemachineImpulseSource);
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
