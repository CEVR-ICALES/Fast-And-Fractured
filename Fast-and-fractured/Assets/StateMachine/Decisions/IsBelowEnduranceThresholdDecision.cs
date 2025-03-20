using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(IsBelowEnduranceThresholdDecision), menuName = "EnemyStateMachine/Decisions/IsBelowEnduranceThresholdDecision")]
public class IsBelowEnduranceThresholdDecision : Decision
{
    [SerializeField] float enduranceThreshold = -1f;
    [SerializeField] bool selfEndurance = true;
    public override bool Decide(Controller controller)
    {
        float health = 0;
        if (selfEndurance)
        {
            EnemyAIBrain brain = controller.GetComponent<EnemyAIBrain>();
            health = brain.GetHealth();
        } else
        {
            //TODO
            //get target endurance
        }
        

        return health <= enduranceThreshold;
    }

    
}
