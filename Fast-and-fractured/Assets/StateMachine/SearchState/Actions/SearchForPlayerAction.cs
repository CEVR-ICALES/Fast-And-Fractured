using StateMachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = nameof(SearchForPlayerAction), menuName = "EnemyStateMachine/Actions/SearchForPlayerAction")]

public class SearchForPlayerAction : Action
{
    public override void Act(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        brain.SearchPlayerPosition();
    }
}
