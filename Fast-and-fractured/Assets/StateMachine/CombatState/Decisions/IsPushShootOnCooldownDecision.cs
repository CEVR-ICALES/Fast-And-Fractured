using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsPushShootOnCooldownDecision), menuName = "EnemyStateMachine/Decisions/IsPushShootOnCooldownDecision")]
public class IsPushShootOnCooldownDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.IsPushShootReady();
    }
}
