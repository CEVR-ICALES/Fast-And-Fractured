using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using Utilities;
namespace StateMachine {

    [CreateAssetMenu(fileName = "TriggerThrowMineAction", menuName = "PlayerShootingStateMachine/Actions/TriggerThrowMineAction")]
    public class TriggerThrowMineAction : Action
    {
        public override void Act(Controller controller)
        {
            PushShootHandle pushShootHandle = controller.GetBehaviour<PushShootHandle>();
            pushShootHandle.MineShoot();
        }
    }
}
