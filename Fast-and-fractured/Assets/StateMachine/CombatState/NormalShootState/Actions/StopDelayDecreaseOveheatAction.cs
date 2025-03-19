using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(StopDelayDecreaseOveheatAction), menuName = "EnemyStateMachine/Actions/StopDelayDecreaseOveheatAction")]

public class StopDelayDecreaseOveheatAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        brain.StopDelayDecreaseOveheat();
    }


}
