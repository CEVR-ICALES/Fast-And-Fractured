using System;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEngine;
using Action = StateMachine.Action;

[Obsolete("This action is not used anymore use   IsUniqueAbilityFinishedDecision instead ")]
public class CheckUniqueAbilityProgressAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();


        brain.IsUniqueAbilityFinished();
    }

    
}
