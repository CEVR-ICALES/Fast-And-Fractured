using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(DecreaseOverheatTimeAction), menuName = "EnemyStateMachine/Actions/DecreaseOverheatTimeAction")]

public class DecreaseOverheatTimeAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        brain.DecreaseOverheatTime();
    }
}
