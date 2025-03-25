using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(UniqueAbilityAction), menuName = "EnemyStateMachine/Actions/UniqueAbilityAction")]
    public class UniqueAbilityAction : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

            brain.UseUniqueAbility();
        }

    }
}

