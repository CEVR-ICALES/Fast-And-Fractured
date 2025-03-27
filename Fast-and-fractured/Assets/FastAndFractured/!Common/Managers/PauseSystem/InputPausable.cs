using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities.Managers.PauseSystem;

public class InputPausable : MonoBehaviour, IPausable
{
    [SerializeField] PlayerInputController playerInputController;
    void Start()
    {
        if (!playerInputController)
        {
            playerInputController = GetComponentInChildren<PlayerInputController>();
        }

        PauseManager.Instance.RegisterPausable(this);
    }
    public void OnPause()
    {
        if (playerInputController)
        {
            playerInputController.InputActions.PlayerInputActions.Disable();
        }
    }

    public void OnResume()
    {
        if (playerInputController)
        {
            playerInputController.InputActions.PlayerInputActions.Enable();
        }
    }
}
