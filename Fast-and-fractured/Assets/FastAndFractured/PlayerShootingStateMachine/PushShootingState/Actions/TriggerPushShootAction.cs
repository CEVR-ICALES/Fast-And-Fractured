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
            Debug.Log("Push Shooting");
            controller.GetBehaviour<PushShootHandle>().CurrentShootDirection = Camera.main.transform.forward;
            controller.GetBehaviour<PushShootHandle>().PushShooting();
        }
    }
}