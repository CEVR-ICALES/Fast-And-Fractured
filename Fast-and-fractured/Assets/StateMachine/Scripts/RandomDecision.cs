using StateMachine;
using System; 
using UnityEngine;

[CreateAssetMenu(fileName = "RandomDecision", menuName = "StateMachine/RandomDecision")]
 
public class DecisionRandomDecision : Decision
{
    [SerializeField] int seed=-1;
    public override bool Decide(Controller controller)
    {

        if (seed != -1)
        {
            UnityEngine
                .Random.seed = seed;

        }
        return  UnityEngine.Random.Range(0, 2)==1; 
    }
}