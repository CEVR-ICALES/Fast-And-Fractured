using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(EmergencyTargetAction), menuName = "EnemyStateMachine/Actions/EmergencyTargetAction")]
    public class EmergencyTargetAction : Action
    {
        public override void Act(Controller controller)
        {
            throw new System.NotImplementedException();
        }

    }
}