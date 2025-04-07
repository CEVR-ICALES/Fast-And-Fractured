using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using StateMachine;
using UnityEngine;
[CreateAssetMenu(fileName = nameof(HasSomeoneThatIsNotTheTargetMadeEnoughDamageDecision), menuName = "EnemyStateMachine/Decisions/HasSomeoneThatIsNotTheTargetMadeEnoughDamageDecision")]
public class HasSomeoneThatIsNotTheTargetMadeEnoughDamageDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain enemyAIBrain = controller.GetBehaviour<EnemyAIBrain>();
        return enemyAIBrain.HasSomeoneThatIsNotTheTargetMadeEnoughDamage();
    }
}