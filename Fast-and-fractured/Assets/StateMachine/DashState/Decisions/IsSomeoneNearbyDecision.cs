using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsSomeoneNearbyDecision), menuName = "EnemyStateMachine/Decisions/IsSomeoneNearbyDecision")]
public class IsSomeoneNearbyDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.EnemySweep();
    }

    
}
