using System.Collections;
using System.Collections.Generic;
using Game;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(PauseOverheatTimeAction), menuName = "PlayerShootingStateMachine/Actions/PauseOverheatTimeAction")]

public class PauseOverheatTimeAction : Action
{
    public override void Act(Controller controller)
    {
        NormalShootHandle normalShootHandle = controller.GetBehaviour<NormalShootHandle>();
        normalShootHandle.PauseOverheatTime();
    }
}
 