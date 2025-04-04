using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(ChoosePlayer), menuName = "EnemyStateMachine/Actions/ChoosePlayer")]
    public class ChoosePlayer : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

            brain.ChoosePlayer();
            FinishAction();
        }
    }

}
