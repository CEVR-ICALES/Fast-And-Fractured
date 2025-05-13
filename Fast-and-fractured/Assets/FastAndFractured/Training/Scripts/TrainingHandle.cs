using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class TrainingHandle : MonoBehaviour
{
    private TrainingInputSucceded[] _trainingInputSuccededs;
    private int _trainingInputSuccededCount;
    private ITimer _trainingTimer;
    // Start is called before the first frame update
    void Start()
    {
        _trainingInputSuccededs = FindObjectsOfType<TrainingInputSucceded>();
        _trainingInputSuccededCount = 0;
        foreach (var trainingInputSucceded in _trainingInputSuccededs)
        {
            trainingInputSucceded.onInputPerformed.AddListener(IsTrainingFinished);
        }
        _trainingTimer = TimerSystem.Instance.CreateTimer(float.MaxValue,Enums.TimerDirection.INCREASE);
    }

    void IsTrainingFinished()
    {
        _trainingInputSuccededCount++;
        if (_trainingInputSuccededCount >= _trainingInputSuccededs.Length)
        {
            //Finish
            _trainingTimer.PauseTimer();
            _trainingTimer.StopTimer();
        }
    }
}
