using System.Collections;
using System.Collections.Generic;
using Game;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(NormalShootDecreaseOverheatTimeAction), menuName = "PlayerShootingStateMachine/Actions/NormalShootDecreaseOverheatTimeAction")]

public class NormalShootDecreaseOverheatTimeAction : Action
{
    public override void Act(Controller controller)
    {
        NormalShootHandle normalShootHandle= controller.GetBehaviour<NormalShootHandle>();
        normalShootHandle.DecreaseOverheatTime();
    }
}
 