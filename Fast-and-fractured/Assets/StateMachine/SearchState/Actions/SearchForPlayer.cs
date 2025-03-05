using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SearchPlayer", menuName = "EnemyStateMachine/Actions/SearchPlayer")]

public class SearchForPlayer : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.SearchPlayer();
    }
}
