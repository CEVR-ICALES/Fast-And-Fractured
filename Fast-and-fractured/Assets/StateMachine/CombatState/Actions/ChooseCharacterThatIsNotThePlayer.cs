using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(ChooseCharacterThatIsNotThePlayer),
    menuName = "EnemyStateMachine/Actions/ChooseCharacterThatIsNotThePlayer")]
    public class ChooseCharacterThatIsNotThePlayer : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

            brain.ChooseCharacterThatIsNotPlayer();
        }
    }
}
