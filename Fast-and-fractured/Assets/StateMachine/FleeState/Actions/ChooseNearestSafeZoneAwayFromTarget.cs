using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(ChooseNearestSafeZoneAwayFromTarget), menuName = "EnemyStateMachine/Actions/ChooseNearestSafeZoneAwayFromTarget")]
    public class ChooseNearestSafeZoneAwayFromTarget : Action
    {
        public override void Act(Controller controller)
        {
            EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

            brain.ChooseNearestSafeZoneAwayFromTarget();
        }
    }
}
