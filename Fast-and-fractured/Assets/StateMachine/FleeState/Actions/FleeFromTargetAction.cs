using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "FleeFromTarget", menuName = "EnemyStateMachine/Actions/FleeFromTarget")]

public class FleeFromTargetAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();
        brain.RunAwayFromCurrentTarget();
    }


}
