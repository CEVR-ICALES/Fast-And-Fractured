using FastAndFractured;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

    [CreateAssetMenu(fileName = nameof(HasItemBeenTakenDecision), menuName = "EnemyStateMachine/Decisions/HasItemBeenTakenDecision")]
public class HasItemBeenTakenDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        return brain.TargetToGo.activeSelf;
    }
}
