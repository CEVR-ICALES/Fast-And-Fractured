using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "GetCarToNormalAction", menuName = "PlayerStateMachine/Actions/GetCarToNormalAction")]
    public class GetCarToNormalAction : Action
    {
       
        public override void Act(Controller controller)
        {
     
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            PhysicsBehaviour physicsBehaviour = controller.GetBehaviour<PhysicsBehaviour>();
            if (!carMovementController.IsInFlipCase())
            {
                carMovementController.IsFlipped = false;
            }
            else
            {
                controller.GetBehaviour<ApplyForceByState>().ApplyFlipStateForce(physicsBehaviour.TouchingGroundNormal,physicsBehaviour.TouchingGroundPoint);
            }
        }
    }
}
