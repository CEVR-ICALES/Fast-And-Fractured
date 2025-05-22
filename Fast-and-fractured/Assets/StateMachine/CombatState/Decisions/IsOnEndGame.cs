using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsOnEndGame), menuName = "EnemyStateMachine/Decisions/IsOnEndGame")]
    public class IsOnEndGame : Decision
    {
        public override bool Decide(Controller controller)
        {
           return controller.GetBehaviour<EnemyAIBrain>().IsOnEndGame();
        }
    }
}
