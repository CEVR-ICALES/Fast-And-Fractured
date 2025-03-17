using Game;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = "TriggerShootingAction", menuName = "PlayerShootingStateMachine/Actions/TriggerShootingAction")]

public class TriggerShootingAction : Action
{
    public override void Act(Controller controller)
    {
        if (controller.GetBehaviour<PlayerInputController>().IsShooting)
        {
            controller.GetBehaviour<NormalShootHandle>().NormalShooting();
        }   
    }
}