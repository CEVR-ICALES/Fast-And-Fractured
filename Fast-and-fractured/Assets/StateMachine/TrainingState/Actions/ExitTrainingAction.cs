using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(ExitTrainingAction), menuName = "EnemyStateMachine/Actions/ExitTrainingAction")]
    public class ExitTrainingAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<EnemyAIBrain>().DesactivateTrainingAction();
        }
    }
}
