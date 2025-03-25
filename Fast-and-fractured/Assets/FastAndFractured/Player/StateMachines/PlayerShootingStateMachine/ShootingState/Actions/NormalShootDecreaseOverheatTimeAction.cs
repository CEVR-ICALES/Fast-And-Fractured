using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;

namespace StateMachine
{
    [CreateAssetMenu(fileName = nameof(PauseOverheatTimeAction), menuName = "PlayerShootingStateMachine/Actions/PauseOverheatTimeAction")]

    public class PauseOverheatTimeAction : Action
    {
        public override void Act(Controller controller)
        {
        }
    }
}

