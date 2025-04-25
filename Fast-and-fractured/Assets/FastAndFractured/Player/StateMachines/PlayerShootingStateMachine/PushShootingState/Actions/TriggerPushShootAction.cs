using FastAndFractured;
using UnityEngine;
using Utilities;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "TriggerPushShootAction", menuName = "PlayerShootingStateMachine/Actions/TriggerPushShootAction")]

    public class TriggerPushShootAction : Action
    {
        public override void Act(Controller controller)
        {
            PushShootHandle pushShootHandle = controller.GetBehaviour<PushShootHandle>();
            pushShootHandle.CurrentShootDirection = controller.GetBehaviour<CameraHolder>().CameraToHold.transform.forward;
            if (controller.GetBehaviour<PlayerInputController>().IsPushShooting)
            {
                pushShootHandle.PushShooting();
            }
        }
    }
}
