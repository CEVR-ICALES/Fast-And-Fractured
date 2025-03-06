using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

public class IsDamagedEnough : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();

        return brain.IsAboveDamageThreshold();
    }

    
}
