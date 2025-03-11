using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsDashReadyDecision), menuName = "EnemyStateMachine/Decisions/IsDashReadyDecision")]
public class IsDashReadyDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();

        return brain.IsDashReady();
    }
}
