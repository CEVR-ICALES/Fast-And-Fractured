using Game;
using StateMachine;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(IsOverheatDecision), menuName = "PlayerShootingStateMachine/Decisions/IsOverheatDecision")]
public class IsOverheatDecision : Decision
{
    public override bool Decide(Controller controller)
    {
       return controller.GetBehaviour<NormalShootHandle>().IsOverHeat;
    }
}
