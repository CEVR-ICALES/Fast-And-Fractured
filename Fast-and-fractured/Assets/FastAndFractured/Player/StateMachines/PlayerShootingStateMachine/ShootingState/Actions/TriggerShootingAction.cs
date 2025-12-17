using FastAndFractured;
using FastAndFractured.Abstractions;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "TriggerShootingAction", menuName = "PlayerShootingStateMachine/Actions/TriggerShootingAction")]
    public class TriggerShootingAction : Action
    {
        public override void Act(Controller controller)
        {
            var shootController = controller.transform.root.GetComponentInChildren<IShootController>();

            if (shootController != null && controller.GetBehaviour<PlayerInputController>().IsShooting)
            {
                var normalShootHandle = controller.GetBehaviour<NormalShootHandle>();
                normalShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
                shootController.TryNormalShoot();
            }
        }
    }
}
