using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(SearchForPlayer), menuName = "EnemyStateMachine/Actions/SearchForPlayer")]

public class SearchForPlayer : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.SearchPlayerPosition();
    }
}
