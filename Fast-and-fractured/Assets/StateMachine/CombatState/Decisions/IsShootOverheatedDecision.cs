using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsShootOverheatedDecision), menuName = "EnemyStateMachine/Decisions/IsShootOverheatedDecision")]
public class IsShootOverheatedDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.IsShootOverheated();
    }
}
