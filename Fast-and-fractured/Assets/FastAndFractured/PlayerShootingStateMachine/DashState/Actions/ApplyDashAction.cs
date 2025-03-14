using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = "ApplyDashAction", menuName = "PlayerShootingStateMachine/Actions/ApplyDashAction")]

public class ApplyDashAction : Action
{
    public override void Act(Controller controller)
    {
        controller.GetBehaviour<CarMovementController>().HandleDashWithPhysics();
    }
}