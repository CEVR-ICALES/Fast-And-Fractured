using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "GetCarToNormalAction", menuName = "PlayerStateMachine/Actions/GetCarToNormalAction")]
    public class GetCarToNormalAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ApplyForceByState>().ApplyFlipStateForce();
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            if (!carMovementController.IsInWall())
            {
                carMovementController.IsFlipped = false;
            }
        }
    }
}
