using FastAndFractured;
using FastAndFractured.Abstractions;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "TriggerPushShootAction", menuName = "PlayerShootingStateMachine/Actions/TriggerPushShootAction")]

    public class TriggerPushShootAction : Action
    {
        public override void Act(Controller controller)
        {
            var shootController = controller.transform.root.GetComponentInChildren<IShootController>();
            PushShootHandle pushShootHandle = controller.GetBehaviour<PushShootHandle>();
            pushShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
            if (shootController != null && controller.GetBehaviour<PlayerInputController>().IsPushShooting)
            {
                shootController.TryPushShoot();
            }
        }
    }
}
