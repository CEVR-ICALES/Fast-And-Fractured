using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Enums;

public class McChickenVisuals : MonoBehaviour
{
    [Header("Particles")]
    [SerializeField] private ParticleSystem spawnFeathersVfx;
    [SerializeField] private ParticleSystem movementFeathersVfx;

    [Header("Models")]
    [SerializeField] private GameObject chickenModel;
    [SerializeField] private GameObject eggModel;

    [Header("Legs")]
    [SerializeField] private McChickenLegsMovement[] legs;

    [Header("ChickenHead")]
    [SerializeField] private Transform headPoint;
    [SerializeField] private Transform headTargetRotationObj;
    [SerializeField] private Transform lastHeadTargetRotationObj;
    [SerializeField] private Transform nextHeadTargetRotationObj;
    [SerializeField] private float horizontalViewAngle;
    [SerializeField] private float verticalViewAngle;
    [SerializeField] private float targetDistance;
    [SerializeField] private AnimationCurve rotationMovementEasing;

    private const float MIN_HEAD_MOVE_DURATION = 0.5f; 
    private const float MAX_HEAD_MOVE_DURATION = 1f; 
    private const float MIN_TIME_BETWEEN_HEAD_CHANGES = 1f; 
    private const float MAX_TIME_BETWEEN_HEAD_CHANGES = 2f; 

    private ITimer _changeTimer;
    private ITimer _moveTimer;
    private float _currentMoveDuration;


    //this scripts will also have animations logic
    public void OnEggLaunched()
    {
        chickenModel.SetActive(false);
        eggModel.SetActive(true);
    }

    public void OnLand()
    {
        ScheduleNextHeadTargetChange();
        eggModel.SetActive(false);
        chickenModel.SetActive(true);
        spawnFeathersVfx.Play();
        movementFeathersVfx.Play();
    }

    public void OnChickenOnFloor()
    {
        NotifyLegsOfGroundState(true);
    }

    public void OnChickenOffFloor()
    {
        NotifyLegsOfGroundState(false);

    }

    private void OnDestroy()
    {
        if(_moveTimer != null)
            _moveTimer.StopTimer();
        if(_changeTimer != null)
            _changeTimer.StopTimer();
    }

    #region ChickenLegs
    private void NotifyLegsOfGroundState(bool isGround)
    {
        foreach (McChickenLegsMovement leg in legs)
        {
            leg.SetIsWalking(isGround);
        }
    }

    #endregion

    #region Head
    private void ScheduleNextHeadTargetChange()
    {
        float waitTime = Random.Range(MIN_TIME_BETWEEN_HEAD_CHANGES, MAX_TIME_BETWEEN_HEAD_CHANGES);
        _changeTimer = TimerSystem.Instance.CreateTimer(waitTime, TimerDirection.INCREASE, () =>
        {
            _changeTimer = null;
            GenerateNewTargetPosition();
        });
    }

    private void GenerateNewTargetPosition()
    {
        // get random point in the given ranges
        float randomYaw = Random.Range(-horizontalViewAngle / 2f, horizontalViewAngle / 2f);
        float randomPitch = Random.Range(-verticalViewAngle / 2f, verticalViewAngle / 2f);

        Quaternion randomRotation = Quaternion.Euler(randomPitch, randomYaw, 0f);
        nextHeadTargetRotationObj.position = headPoint.position + randomRotation * Vector3.forward * targetDistance;

        StartHeadRotationSmoothMovement();
    }

    private void StartHeadRotationSmoothMovement()
    {
        lastHeadTargetRotationObj.position = headTargetRotationObj.transform.position;
        _currentMoveDuration = Random.Range(MIN_HEAD_MOVE_DURATION, MAX_HEAD_MOVE_DURATION);

        _moveTimer = TimerSystem.Instance.CreateTimer(_currentMoveDuration, TimerDirection.INCREASE, onTimerIncreaseComplete: () =>
        {
            ScheduleNextHeadTargetChange();
            _moveTimer = null;
        }, onTimerIncreaseUpdate: (progress) =>
        {
            float easedProgress = rotationMovementEasing.Evaluate(progress);
            headTargetRotationObj.transform.position = Vector3.Lerp(lastHeadTargetRotationObj.position, nextHeadTargetRotationObj.position, easedProgress);
        });
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 forwad = headPoint.forward * targetDistance;

        Vector3 left = Quaternion.Euler(0, -horizontalViewAngle / 2, 0) * forwad;
        Vector3 right = Quaternion.Euler(0, horizontalViewAngle / 2, 0) * forwad;
        Gizmos.DrawLine(headPoint.position, headPoint.position + right);
        Gizmos.DrawLine(headPoint.position, headPoint.position + left);

        Vector3 up = Quaternion.Euler(verticalViewAngle / 2, 0 , 0) * forwad;
        Vector3 down = Quaternion.Euler(-verticalViewAngle / 2, 0 , 0) * forwad;
        Gizmos.DrawLine(headPoint.position, headPoint.position + up);
        Gizmos.DrawLine(headPoint.position, headPoint.position + down);
    }

    #endregion
}
