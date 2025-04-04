using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using FastAndFractured;

[CreateAssetMenu(fileName = nameof(HasTargetToShootDecision), menuName = "StateMachine/HasTargetToShootDecision")]
public class HasTargetToShootDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.HasTargetToShoot();
    }

}
