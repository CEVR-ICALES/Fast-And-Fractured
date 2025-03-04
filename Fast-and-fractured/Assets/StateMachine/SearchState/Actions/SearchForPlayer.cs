using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SearchForPlayer : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetComponentInController<EnemyAIBrain>();

        brain.SearchPlayer();
    }
}
