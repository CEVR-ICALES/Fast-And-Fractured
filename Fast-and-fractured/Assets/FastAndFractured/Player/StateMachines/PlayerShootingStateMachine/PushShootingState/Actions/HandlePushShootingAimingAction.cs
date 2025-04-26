using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "HandlePushShootingAimingAction", menuName = "PlayerShootingStateMachine/Actions/HandlePushShootingAimingAction")]
    
    public class HandlePushShootingAimingAction : Action
    {
        [SerializeField]
        private bool activateAim = true;
        public override void Act(Controller controller)
        {
            if (controller.GetBehaviour<PhysicsBehaviour>().Rb.velocity != Vector3.zero)
            {
                controller.GetBehaviour<AimPushShootTrace>().DrawTrayectory(activateAim);
            }
        }
    }
}
