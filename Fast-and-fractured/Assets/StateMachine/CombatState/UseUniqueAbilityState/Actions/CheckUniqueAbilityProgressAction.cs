using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CheckUniqueAbilityProgressAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();


        brain.IsUniqueAbilityFinished();
    }

    
}
