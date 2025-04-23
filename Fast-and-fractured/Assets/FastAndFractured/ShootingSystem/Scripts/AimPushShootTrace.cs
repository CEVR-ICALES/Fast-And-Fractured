using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using FastAndFractured;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Utilities;

public class AimPushShootTrace : MonoBehaviour
{
    [Header("Settings")]
    [Range(1f, 15f)]
    [SerializeField] private int frames = 5;
    private ITimer _showTraceTimer;
    [Range(0.01f, 0.1f)]
    public float timeStep = 0.02f;
    [SerializeField] private int maxCalculationSteps = 128;
    [Range(0.01f, 1.0f)]
    [SerializeField] private float tolerance;

    [Header("HitMark")]
    [SerializeField] private GameObject hitMark;
    private AimPushShootHitMarkCollision _hitMarkCollision;

    private LayerMask _groundMask = 3;
    private LayerMask _staticMask = 10;
    private LayerMask combinedMask;

    private List<Vector3> points;
    private List<Vector3> previousPoints;
    private Vector3 _currentVelocity;
    private float _currentCustomGravity;
    private Vector3 _currentPosition;
    private Vector3 _previousVelocity = Vector3.zero;
    private bool _currentFinished = true;
    public UnityEvent currentFinishedEvent;
    private Vector3 _initialSpeed;
    private Vector3 _rangePoint;
    private Vector3 _currentPoint;
    private Vector3 _currentNormal;
    private int positionToFollowHitMark;

    [Header("Resources")]
    [SerializeField] private Transform pushShootPoint;
    [SerializeField] private PushShootHandle pushShootHandle;
    [SerializeField] private LineRenderer lineRenderer;
   
    // Start is called before the first frame update
    void Start()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
        }
        points = new List<Vector3>();
        _hitMarkCollision = hitMark.GetComponent<AimPushShootHitMarkCollision>();
        _previousVelocity = Vector3.one;
        _initialSpeed = Vector3.zero;
        CalculateTrayectory();
        _rangePoint = RangePoint();
        hitMark.transform.position = _rangePoint;
        _hitMarkCollision.moveMyPosition.AddListener(MoveHitMark);
        _hitMarkCollision.onCollision.AddListener(SetPointAndNormalForHitMark);
        combinedMask = (1 << _groundMask) | (1 << _staticMask);
        _showTraceTimer = null;
    }

    private void Update()
    {
       CalculateTrayectory();
    }

    private void MoveHitMark(Vector3 newPosition)
    { 
       positionToFollowHitMark = FindPointColsestToThisPosition(newPosition);
    }

    private void SetPointAndNormalForHitMark(Vector3 point, Vector3 normal, bool colliding)
    {
        _currentPoint = point;
        _currentNormal = normal;
    }

    private int FindPointColsestToThisPosition(Vector3 newPosition)
    {
        Vector3 closestPoint = points[0];
        foreach (var point in points) {
            if ((newPosition - closestPoint).magnitude > (newPosition - point).magnitude)
            {
                closestPoint = point;
            }
        }
        return points.IndexOf(closestPoint);
    }

    private Vector3 RangePoint()
    {
        Vector3 initialPoint = points[0];
        foreach (var point in points)
        {
            if ((point - initialPoint).magnitude >= 15f)
            {
                return point;
            }
        }
        return initialPoint;
    }
    private void CalculateTrayectory()
    {
        _initialSpeed = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);
        if (_previousVelocity.ToString() != _initialSpeed.ToString())
        {
            previousPoints = points;
            _currentVelocity = _initialSpeed;
            _currentPosition = pushShootPoint.position;
            _currentFinished = false;
            _previousVelocity = _currentVelocity;
            points.Clear();
            points.Add(_currentPosition);

            for (int currentPoints = 0; currentPoints < maxCalculationSteps; currentPoints++)
            {
                _currentPosition = GetNextPosition(_currentPosition, _currentVelocity);
                points.Add(_currentPosition);
                _currentVelocity += Physics.gravity * _currentCustomGravity * timeStep;
            }
        }
    }

    public void DrawTrayectory(bool draw)
    {
        if (draw) {
            SetHitMark();
        }
        else
        {

        }
    }

    Vector3 GetNextPosition(Vector3 currentPoint, Vector3 velocity)
    {
        return currentPoint + velocity * timeStep;
    }

    private void SetHitMark()
    {
        if (_showTraceTimer == null)
        {
            List<Vector3> preivousPoints = new List<Vector3>(previousPoints);
            List<Vector3> interpolationPoints = new List<Vector3>(previousPoints);
            List<Vector3> currentPoints = new List<Vector3>(points);
            int cicles = 3;
            int currentCicles = 0;
            float timePerInterpolationChange = 1f / (frames * cicles);
            float timeStep = 0;
            _showTraceTimer = TimerSystem.Instance.CreateTimer(Time.deltaTime * frames, onTimerDecreaseComplete: () =>
            {
                _hitMarkCollision.PositionReference = points[positionToFollowHitMark];
                hitMark.SetActive(true);
                 hitMark.transform.SetPositionAndRotation(_currentPoint, Quaternion.LookRotation(_currentNormal));
                 currentPoints = RemovePointsInLowerPos(currentPoints, _currentPoint.y);
                 lineRenderer.SetPositions(currentPoints.ToArray());
                lineRenderer.enabled = true;
                _showTraceTimer = null;
            }, onTimerDecreaseUpdate: (float timer) =>
            {
                currentCicles = 0;
                while (currentCicles < cicles)
                {
                    timeStep += timePerInterpolationChange;
                    for (int i = 0; i < preivousPoints.Count; i++)
                    {
                        interpolationPoints[i] = Vector3.Lerp(preivousPoints[i], currentPoints[i], timePerInterpolationChange);
                    }
                    lineRenderer.positionCount = interpolationPoints.Count;
                    lineRenderer.SetPositions(interpolationPoints.ToArray());
                    lineRenderer.Simplify(tolerance);
                    currentCicles++;
                }
            });
        }
    }

    private void UnsetHitMark()
    {
        if(_showTraceTimer !=null)
            _showTraceTimer.StopTimer();
        hitMark.SetActive(false);
        lineRenderer.enabled = false;
    }

    private List<Vector3> RemovePointsInLowerPos(List<Vector3> currentPoints,float yPosition)
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
    private void OnDisable()
    {

    }
}
