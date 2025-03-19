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
            // to do (call push shooting method)
            // controller.GetBehaviour<PlayerInputController>().IsPushShootMode = false;
        }
    }
}