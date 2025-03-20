using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using Game;

[CreateAssetMenu(fileName = nameof(IsBelowEnduranceThresholdDecision), menuName = "EnemyStateMachine/Decisions/IsBelowEnduranceThresholdDecision")]
public class IsBelowEnduranceThresholdDecision : Decision
{
    [SerializeField] float enduranceThreshold = -1f;
    [SerializeField] bool selfEndurance = true;
    public override bool Decide(Controller controller)
    {
        EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();
        float health = 0;
        if (selfEndurance)
        {
            health = brain.GetHealth();
        }
        else
        {
            if (brain.Target.TryGetComponent(out StatsController targetStats))
            {
                health = targetStats.Endurance;
            }
            else
            {
                targetStats = brain.Target.GetComponentInChildren<StatsController>();
                if (targetStats != null)
                {
                    health = targetStats.Endurance;
                }
                else
                {
                    health = 0;
                    Debug.LogError("No endurance found!!!!!");
                }
            }
        }
        health = 40;

        return health <= enduranceThreshold;
    }


}
