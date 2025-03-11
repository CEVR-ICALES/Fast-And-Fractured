using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;

[CreateAssetMenu(fileName = nameof(ANDDecision), menuName = "StateMachine/Decision/ANDDecision")]

public class ANDDecision : Decision
{
    [SerializeField] Decision[] decisions;
    public override bool Decide(Controller controller)
    {
        bool success = true;
        for (int i = 0; i < decisions.Length && success; i++)
        {
            success = decisions[i].Decide(controller);
        }
        return success;
    }

}
