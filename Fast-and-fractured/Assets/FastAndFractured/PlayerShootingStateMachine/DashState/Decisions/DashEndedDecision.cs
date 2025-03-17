using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(DashEndedDecision), menuName = "PlayerShootingStateMachine/Decisions/DashEndedDecision")]
public class DashEndedDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        return !controller.GetBehaviour<CarMovementController>().IsDashing;
    }
}
