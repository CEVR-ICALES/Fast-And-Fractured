using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsShootOverheated), menuName = "EnemyStateMachine/Decisions/IsShootOverheated")]
public class IsShootOverheated : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.IsShootOverheated();
    }
}
