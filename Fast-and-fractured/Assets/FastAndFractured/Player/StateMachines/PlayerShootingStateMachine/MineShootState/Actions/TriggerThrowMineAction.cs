using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using FastAndFractured.Abstractions;
using UnityEngine;
using Utilities;
namespace StateMachine {

    [CreateAssetMenu(fileName = "TriggerThrowMineAction", menuName = "PlayerShootingStateMachine/Actions/TriggerThrowMineAction")]
    public class TriggerThrowMineAction : Action
    {
        public override void Act(Controller controller)
        {
            var shootController = controller.transform.root.GetComponentInChildren<IShootController>();
            if (shootController != null)
            {
                shootController.TryMineShoot();
            }
        }
    }
}
