using UnityEngine;
using Utilities;

public class RisingLava : MonoBehaviour
{
    [SerializeField] private float _delayBeforeRise = 0f;
    [SerializeField] private float _riseDuration = 0f;
    [SerializeField] private float _riseDistance = 0f;

    Vector3 _startPosition;
    Vector3 _endPosition;

    void Start()
    {
        _startPosition = transform.position;
        _endPosition = _startPosition + _riseDistance * Vector3.up;
        TimerSystem.Instance.CreateTimer(_delayBeforeRise,
            onTimerDecreaseComplete: () => StartRise());
    }

   
    void StartRise()
    {
        TimerSystem.Instance.CreateTimer(_riseDuration, 
            Enums.TimerDirection.INCREASE, 
            onTimerIncreaseUpdate: (progress) => RisingLogic(progress),
            onTimerIncreaseComplete: () => transform.position = _endPosition);
    }

    void RisingLogic(float progress)
    {
        float progressPercent = progress / _riseDuration;
        transform.position = _startPosition + _riseDistance * progressPercent * Vector3.up;
        Debug.Log(progressPercent);
        Debug.Log(_riseDistance * progressPercent * Vector3.up);
    }
}
