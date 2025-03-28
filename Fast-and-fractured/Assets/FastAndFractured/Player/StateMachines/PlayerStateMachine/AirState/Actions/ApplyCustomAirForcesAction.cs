using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
namespace StateMachine
{
    [CreateAssetMenu(fileName = "ApplyCustomAirForcesAction", menuName = "PlayerStateMachine/Actions/ApplyCustomAirForcesAction")]
    public class ApplyCustomAirForcesAction : Action
    {
        public override void Act(Controller controller)
        {
            controller.GetBehaviour<RollPrevention>().ToogleCustomGravity(true, controller.GetBehaviour<PhysicsBehaviour>().Rb);
        }
    }
}
