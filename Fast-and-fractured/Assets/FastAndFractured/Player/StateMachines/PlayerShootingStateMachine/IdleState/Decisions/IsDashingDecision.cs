using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(IsDashingDecision), menuName = "PlayerShootingStateMachine/Decisions/IsDashingDecision")]
    public class IsDashingDecision : Decision
    {
        public override bool Decide(Controller controller)
        {
            PlayerInputController playerInputController = controller.GetBehaviour<PlayerInputController>();
            CarMovementController carMovementController = controller.GetBehaviour<CarMovementController>();
            return playerInputController.IsDashing && carMovementController.CanDash;
        }
    }
}
