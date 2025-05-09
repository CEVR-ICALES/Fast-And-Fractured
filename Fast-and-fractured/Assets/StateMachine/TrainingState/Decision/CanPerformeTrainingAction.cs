using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(CanPerformeTrainingAction), menuName = "EnemyStateMachine/Decisions/CanPerformeTrainingAction")]
    public class CanPerformeTrainingAction : Decision
    {
        public override bool Decide(Controller controller)
        {
            return controller.GetBehaviour<EnemyAIBrain>().DoTrainingAction;
        }
    }
}
