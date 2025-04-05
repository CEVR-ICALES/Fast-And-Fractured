using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using FastAndFractured;

[CreateAssetMenu(fileName = nameof(AboveRecoveryHealthPercentageDecision), menuName = "EnemyStateMachine/Decisions/IsAboveRecoveryHealthPercentage")]
public class AboveRecoveryHealthPercentageDecision : Decision
{
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();
        StatsController statsController = controller.GetComponent<StatsController>();

        return statsController.Endurance >= brain.RecoveryThresholdPercentageForSearch;
    }

}
