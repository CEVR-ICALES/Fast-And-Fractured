using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Action = StateMachine.Action;
using FastAndFractured;

namespace StateMachine
{
    [Obsolete("This action is not used anymore use   IsUniqueAbilityFinishedDecision instead ")]
    public class CheckUniqueAbilityProgressAction : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

            brain.IsUniqueAbilityFinished();
        }
    }
}