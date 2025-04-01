using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WantsToChangeToCombatState : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain enemyAIBrain = controller.GetBehaviour<EnemyAIBrain>();
        return enemyAIBrain.WantsToChangeToCombatState();
    }
}
