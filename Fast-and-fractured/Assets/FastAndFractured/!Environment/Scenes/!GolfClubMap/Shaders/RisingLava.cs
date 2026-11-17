using FastAndFractured;
using UnityEngine;
using Utilities;

public class RisingLava : MonoBehaviour
{
    [SerializeField] private float _delayBeforeRise = 0f;
    [SerializeField] private float _riseDuration = 0f;
    [SerializeField] private float _riseDistance = 0f;

    Vector3 _startPosition;
    Vector3 _endPosition;
    LevelController _levelController;
    System.Func<float> _networkClock;
    bool _started;

    void Start()
    {
        _startPosition = transform.position;
        _endPosition = _startPosition + _riseDistance * Vector3.up;

        _levelController = LevelController.HasInstance ? LevelController.Instance : null;
        if (_levelController == null || _levelController.HasMatchClockStarted)
            Begin();
        else
            _levelController.onMatchClockStarted += Begin;
    }

    void OnDestroy()
    {
        if (_levelController != null)
            _levelController.onMatchClockStarted -= Begin;
    }

    void Begin()
    {
        if (_started) return;
        _started = true;
        _networkClock = _levelController != null ? _levelController.NetworkMatchSeconds : null;
        if (_networkClock != null) return;
        TimerSystem.Instance.CreateTimer(_delayBeforeRise,
            onTimerDecreaseComplete: () => StartRise());
    }

    void Update()
    {
        if (_networkClock != null)
            RisingLogic(_networkClock() - _delayBeforeRise);
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
        float progressPercent = Mathf.Clamp01(progress / _riseDuration);
        transform.position = _startPosition + _riseDistance * progressPercent * Vector3.up;
    }
}
