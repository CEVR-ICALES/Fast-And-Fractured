using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(WantsToChangeToFleeState), menuName = "EnemyStateMachine/Decisions/WantsToChangeToFleeState")]
public class WantsToChangeToFleeState : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain enemyAIBrain = controller.GetBehaviour<EnemyAIBrain>();
        return enemyAIBrain.WantsToChangeToFleeState();
    }
}
