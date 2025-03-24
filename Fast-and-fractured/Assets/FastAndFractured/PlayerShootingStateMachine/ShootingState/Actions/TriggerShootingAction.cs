using Game;
using StateMachine;
using UnityEngine;
using Utilities;
[CreateAssetMenu(fileName = "TriggerShootingAction", menuName = "PlayerShootingStateMachine/Actions/TriggerShootingAction")]

public class TriggerShootingAction : Action
{
    public override void Act(Controller controller)
    {
        if (controller.GetBehaviour<PlayerInputController>().IsShooting)
        {
            NormalShootHandle normalShootHandle = controller.GetComponent<NormalShootHandle>();
            normalShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
            normalShootHandle.NormalShooting();
        }   
    }
}