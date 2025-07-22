using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using FastAndFractured;
using FastAndFractured.Core;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Events;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Utilities;


public class AimPushShootTrace : AbstractAutoInitializableMonoBehaviour
{
    [Header("Settings")]
    [Range(1f, 5f)] [SerializeField] private int frames = 1;
    [Range(0.01f, 0.1f)] [SerializeField] public float timeStep = 0.0223f;
    [SerializeField] private int maxCalculationSteps = 128;
    [Range(0.01f, 1.0f)] [SerializeField] private float tolerance = 0.25f;
    [SerializeField] private float magnitudeDiferenceFactorBetweenColisionPoints = 1.5f;

    [Header("HitMark")]
    [SerializeField] private GameObject hitMark;
    [SerializeField] private AimPushShootHitMarkCollision hitMarkCollision;
    [SerializeField] private AimPushShootHitMarkCollision hitMarkCollision2;
    [SerializeField] private float delayTimeForSecondThrow = 0.2f;

    [Header("Resources")]
     private Transform _pushShootPoint;
    [SerializeField] private PushShootHandle pushShootHandle;
    [SerializeField] private LineRenderer lineRenderer;

    private List<Vector3> _points = new List<Vector3>();
    private List<Vector3> previousPoints = new List<Vector3>();
    private Vector3 _currentVelocity;
    private float _currentCustomGravity;
    private Vector3 _currentPosition;
    private Vector3 _previousVelocity;
    private Vector3 _initialSpeed;
    private Vector3 _currentContactPoint;
    private int _previousContactIndex;
    [SerializeField]
    private float toleranceToVelocityMarginError = 0.001f;
    private int _currentIndex = 0;
    private ITimer _showTraceTimer;
    private bool _calculateTracePoints;

    private void OnEnable()
    {
        hitMarkCollision.onCollision.AddListener(SetPointForHitMark);
        hitMarkCollision2.onCollision.AddListener(SetPointForHitMark);
    }
    protected override void Construct()
    {
        
    }
    protected override void Initialize()
    {
        base.Initialize();
        if (!lineRenderer) lineRenderer = GetComponent<LineRenderer>();
        if (!pushShootHandle) pushShootHandle = transform.parent.GetComponentInChildren<PushShootHandle>();
        _pushShootPoint = pushShootHandle.PushShootPoint;
        CalculateTrayectory();
        hitMark.SetActive(false);
        _previousContactIndex = 0;
        _initialSpeed = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);
        _calculateTracePoints = false;
        ThrowSimulatedProyectile();
    }

    private void FixedUpdate()
    {
        if (_calculateTracePoints) 
        {
            ThrowSimulatedProyectile();
        }
    }

    void LateUpdate()
    {
        if (_calculateTracePoints)
        {
            CalculateTrayectory();
        }
    }

    private void CalculateTrayectory()
    {
        _initialSpeed = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);

        if (Vector3.SqrMagnitude(_previousVelocity - _initialSpeed) > toleranceToVelocityMarginError)
        {
            previousPoints = new List<Vector3>(_points);
            _currentPosition = _pushShootPoint.position;
            _points.Clear();
            _points.Add(_currentPosition);
            float currentTimeStep = 0;

            for (int i = 0; i < maxCalculationSteps; i++)
            {
                currentTimeStep += timeStep;
                _currentPosition = _pushShootPoint.position + _initialSpeed * currentTimeStep + 0.5f * Physics.gravity * _currentCustomGravity * currentTimeStep * currentTimeStep;
                _points.Add(_currentPosition);
            }
            _previousVelocity = _initialSpeed;
        }
    }

    public int FindClosestPointAbove(Vector3 targetPoint)
    {
        int closestIndex = 0;
        float minYDifference = float.MaxValue;

        for (int i = 0; i < _points.Count; i++)
        {
            float yDiff = Mathf.Abs(_points[i].y - targetPoint.y);
            if (yDiff < minYDifference)
            {
                minYDifference = yDiff;
                closestIndex = i;
            }
        }
        return closestIndex;
    }

    private List<Vector3> RemovePointsInLowerPos(List<Vector3> currentPoints, float yPosition)
    {
        List<Vector3> listWithRemovedPoints = new List<Vector3>(currentPoints);
        bool breakFor = false;
        for (int i = currentPoints.Count -1; i >=0&&!breakFor; i--)
        {
            if (yPosition > currentPoints[i].y)
            {
                listWithRemovedPoints.Remove(currentPoints[i]);
            }
            else
            {
                breakFor = true;
            }
        }
        return listWithRemovedPoints;
    }

    private void SetHitMark()
    {
        hitMark.SetActive(true);
        lineRenderer.enabled = true;
        _currentIndex = FindClosestPointAbove(_currentContactPoint);
        int indexToFollow = _previousContactIndex;
        bool condition;
        if (_previousContactIndex >= _points.Count)
        {
            condition = true;
        }
        else
        {
            condition = Mathf.Abs(_points[_previousContactIndex].y - _points[_currentIndex].y) > magnitudeDiferenceFactorBetweenColisionPoints;
        }

        if (condition)
        {
            _previousContactIndex = _currentIndex;
            indexToFollow = _currentIndex;
        }
        else
        {
            hitMark.transform.position = _points[_previousContactIndex];
        }
        List<Vector3> filteredCurrentPoints = RemovePointsInLowerPos(new List<Vector3>(_points), _points[_currentIndex].y);
        List<Vector3> filteredPreviousPoints = new List<Vector3>();
        bool noPreviousPoint = previousPoints.Count == 0;
        if (!noPreviousPoint)
        {
          filteredPreviousPoints = RemovePointsInLowerPos(new List<Vector3>(previousPoints), previousPoints[_currentIndex].y);
        }
        lineRenderer.positionCount = filteredPreviousPoints.Count;
        lineRenderer.SetPositions(filteredCurrentPoints.ToArray());
    }

    private void SetPointForHitMark(Vector3 point)
    {
       _currentContactPoint = point;
    }


    private void ThrowSimulatedProyectile()
    {
        hitMarkCollision.SimulateThrow(_initialSpeed, _pushShootPoint.position, Physics.gravity * _currentCustomGravity);
        TimerSystem.Instance.CreateTimer(delayTimeForSecondThrow, onTimerDecreaseComplete: () => { hitMarkCollision2.SimulateThrow(_initialSpeed, _pushShootPoint.position, Physics.gravity * _currentCustomGravity); });
    }

    public void DrawTrayectory(bool draw)
    {
        if (draw) SetHitMark();
        else UnsetHitMark();
        _calculateTracePoints = draw;
    }

    private void UnsetHitMark()
    {
        hitMark.SetActive(false);
        lineRenderer.enabled = false;
    }

    private void OnDisable() 
    { 
        UnsetHitMark(); 
        hitMarkCollision.onCollision?.RemoveAllListeners();
        hitMarkCollision2?.onCollision?.RemoveAllListeners();
    }
}