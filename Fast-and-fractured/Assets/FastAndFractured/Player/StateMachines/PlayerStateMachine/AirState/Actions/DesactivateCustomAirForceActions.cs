using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;

namespace StateMachine
{
    [CreateAssetMenu(fileName = "DesactivateCustomAirForceActions", menuName = "PlayerStateMachine/Actions/DesactivateCustomAirForceActions")]
    public class DesactivateCustomAirForceActions : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<ApplyForceByState>().ToggleCustomGravity(false);
            controller.GetBehaviour<ApplyForceByState>().ToggleAirFriction(false);
        }
    }
}
