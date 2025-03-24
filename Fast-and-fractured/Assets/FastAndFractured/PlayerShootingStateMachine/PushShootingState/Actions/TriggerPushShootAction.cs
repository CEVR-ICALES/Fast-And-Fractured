using Game;
using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = "TriggerPushShootAction", menuName = "PlayerShootingStateMachine/Actions/TriggerPushShootAction")]

public class TriggerPushShootAction : Action
{
    public override void Act(Controller controller)
    {
        if (controller.GetBehaviour<PlayerInputController>().IsPushShooting)
        {
            PushShootHandle pushShootHandle = controller.GetBehaviour<PushShootHandle>();
            pushShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
            pushShootHandle.PushShooting();
        }
    }
}