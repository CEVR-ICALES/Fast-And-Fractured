using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsPushShootOnCooldown), menuName = "EnemyStateMachine/Decisions/IsPushShootOnCooldown")]
public class IsPushShootOnCooldown : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetBehaviour<EnemyAIBrain>();

        return brain.IsPushShootOnCooldown();
    }
}
