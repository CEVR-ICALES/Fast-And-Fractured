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
    [SerializeField]
    private TMPro.TMP_Text trainingTextTime;
    [SerializeField]
    private float trainingMessageDuration = 4.5f;
    void Start()
    {
        trainingTextTime.text = string.Empty;
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
            IngameEventsManager.Instance.CreateEvent("Training.Finish",trainingMessageDuration);
            trainingTextTime.text = _trainingTimer.GetData().ToString();
            TimerSystem.Instance.CreateTimer(trainingMessageDuration, onTimerDecreaseComplete: () =>
            {
                trainingTextTime.text = string.Empty;
            });
            _trainingTimer.StopTimer();
        }
    }
}
