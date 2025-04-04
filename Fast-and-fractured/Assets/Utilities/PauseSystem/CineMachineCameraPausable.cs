using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Managers.PauseSystem;

public class CineMachineCameraPausable : MonoBehaviour, IPausable
{
    [SerializeField] CinemachineFreeLook inemachineCamera;
    private float _normalSpeedX = 0;
    private float _normalSpeedY = 0;

    private void Start()
    {
        if (!inemachineCamera)
        {
            inemachineCamera = GetComponentInChildren<CinemachineFreeLook>();
        }

        _normalSpeedX = inemachineCamera.m_XAxis.m_MaxSpeed;
        _normalSpeedY = inemachineCamera.m_YAxis.m_MaxSpeed;

        PauseManager.Instance.RegisterPausable(this);

    }
    public void OnPause()
    {
        inemachineCamera.m_XAxis.m_MaxSpeed = 0f;
        inemachineCamera.m_YAxis.m_MaxSpeed = 0f;
    }

    public void OnResume()
    {
        inemachineCamera.m_XAxis.m_MaxSpeed = _normalSpeedX;
        inemachineCamera.m_YAxis.m_MaxSpeed = _normalSpeedY;
    }

    void OnDestroy()
    {
        PauseManager.Instance?.UnregisterPausable(this);
    }
}
