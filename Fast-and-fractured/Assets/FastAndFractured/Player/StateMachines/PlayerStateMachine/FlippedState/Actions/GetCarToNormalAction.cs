using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "GetCarToNormalAction", menuName = "PlayerStateMachine/Actions/GetCarToNormalAction")]
    public class GetCarToNormalAction : Action
    {
        private ITimer _flipForceTimer;
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
                if (_flipForceTimer == null)
                {
                    controller.GetBehaviour<ApplyForceByState>().ApplyFlipStateForce(physicsBehaviour.TouchingGroundNormal, physicsBehaviour.TouchingGroundPoint);
                    _flipForceTimer = TimerSystem.Instance.CreateTimer(0.5f, onTimerDecreaseComplete: () =>
                    {
                        _flipForceTimer = null;
                    });
                }
            }
        }
    }
}
