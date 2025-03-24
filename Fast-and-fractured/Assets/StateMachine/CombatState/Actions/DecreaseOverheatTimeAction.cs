using Game;
using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(DecreaseOverheatTimeAction), menuName = "EnemyStateMachine/Actions/DecreaseOverheatTimeAction")]

public class DecreaseOverheatTimeAction : Action
{
    public override void Act(Controller controller)
    {
        NormalShootHandle shootHandle = controller.GetBehaviour<NormalShootHandle>();
        shootHandle.DecreaseOverheatTime();
    }
}
