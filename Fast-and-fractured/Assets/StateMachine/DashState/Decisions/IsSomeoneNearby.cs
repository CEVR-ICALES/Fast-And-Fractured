using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsSomeoneNearby), menuName = "EnemyStateMachine/Decisions/IsSomeoneNearby")]
public class IsSomeoneNearby : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.EnemySweep();
    }

    
}
