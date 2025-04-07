using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using FastAndFractured;
using Utilities;

[CreateAssetMenu(fileName = nameof(HasXSecondsPassedDecision), menuName = "StateMachine/HasXSecondsPassedDecision")]
public class HasXSecondsPassedDecision : Decision
{
    [SerializeField] private float secondsToWait = 3f;
    private bool _done = false;
    ITimer _timer = null;
    public override bool Decide(Controller controller)
    {
        if (_timer == null)
        {
            _done = false;
            _timer = TimerSystem.Instance.CreateTimer(secondsToWait, 
                onTimerDecreaseComplete: () =>
                {
                    _done = true;
                });

        }

        return _done;
    }
}
