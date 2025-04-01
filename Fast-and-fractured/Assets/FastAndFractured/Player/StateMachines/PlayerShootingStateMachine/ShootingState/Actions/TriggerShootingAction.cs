using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "TriggerShootingAction", menuName = "PlayerShootingStateMachine/Actions/TriggerShootingAction")]
    public class TriggerShootingAction : Action
    {
        public override void Act(Controller controller)
        {
            if (controller.GetBehaviour<PlayerInputController>().IsShooting)
            {
                NormalShootHandle normalShootHandle = controller.GetBehaviour<NormalShootHandle>();
                normalShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
                normalShootHandle.NormalShooting();
            }
        }
    }
}
