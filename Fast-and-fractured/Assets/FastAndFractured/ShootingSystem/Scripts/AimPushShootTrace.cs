using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using FastAndFractured;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Timeline;
using UnityEngine.UIElements;
using Utilities;

//public class AimPushShootTrace : MonoBehaviour
//{
//    [Header("Settings")]
//    [Range(1f, 15f)]
//    [SerializeField] private int frames = 5;
//    private ITimer _showTraceTimer;
//    [Range(0.01f, 0.1f)]
//    public float timeStep = 0.02f;
//    [SerializeField] private int maxCalculationSteps = 128;
//    [Range(0.01f, 1.0f)]
//    [SerializeField] private float tolerance;

//    [Header("HitMark")]
//    [SerializeField] private GameObject hitMark;
//    [SerializeField]
//    private AimPushShootHitMarkCollision _hitMarkCollision;

//    private LayerMask _groundMask = 3;
//    private LayerMask _staticMask = 10;
//    private LayerMask combinedMask;

//    private List<Vector3> points;
//    private List<Vector3> previousPoints;
//    private Vector3 _currentVelocity;
//    private float _currentCustomGravity;
//    private Vector3 _currentPosition;
//    private Vector3 _previousVelocity = Vector3.zero;
//    private bool _currentFinished = true;
//    public UnityEvent currentFinishedEvent;
//    private Vector3 _initialSpeed;
//    private Vector3 _rangePoint;
//    private Vector3 _currentPoint;
//    private Vector3 _currentNormal;
//    private Vector3 _previousCurrentPoint;
//    private int _currentIndex = 0;
//    [SerializeField]
//    private float magnitudeDiferenceFactorBetweenColisionPoints = 1.5f;


//    [Header("Resources")]
//    [SerializeField] private Transform pushShootPoint;
//    [SerializeField] private PushShootHandle pushShootHandle;
//    [SerializeField] private LineRenderer lineRenderer;

//    // Start is called before the first frame update
//    void Start()
//    {
//        if (lineRenderer == null)
//        {
//            lineRenderer = GetComponent<LineRenderer>();
//        }
//        points = new List<Vector3>();
//        _previousVelocity = Vector3.one;
//        _initialSpeed = Vector3.zero;
//        _currentPoint = Vector3.zero;
//        _currentNormal = Vector3.zero;
//        _previousCurrentPoint = Vector3.one + Vector3.one;
//        CalculateTrayectory();
//        _hitMarkCollision.onCollision.AddListener(SetPointAndNormalForHitMark);
//        combinedMask = (1 << _groundMask) | (1 << _staticMask);
//        _showTraceTimer = null;
//    }

//    private void Update()
//    {
//       CalculateTrayectory();
//    }

//    private void SetPointAndNormalForHitMark(Vector3 point, Vector3 normal, bool colliding)
//    {
//        _currentPoint = point;
//        _currentNormal = normal;
//    }

//        public int FindClosestPointAbove(Vector3 targetPoint)
//        {
//            Vector3 closestPoint = points[0];
//            float minYDifference = float.MaxValue;

//            foreach (Vector3 point in points)
//            {
//                if (point.y > targetPoint.y)
//                {
//                    float yDifference = point.y - targetPoint.y;

//                    if (yDifference < minYDifference)
//                    {
//                        minYDifference = yDifference;
//                        closestPoint = point;
//                    }
//                }
//            }

//            return points.IndexOf(closestPoint);
//        }

//    private Vector3 RangePoint()
//    {
//        Vector3 initialPoint = points[0];
//        foreach (var point in points)
//        {
//            if ((point - initialPoint).magnitude >= 15f)
//            {
//                return point;
//            }
//        }
//        return initialPoint;
//    }
//    private void CalculateTrayectory()
//    {
//        _initialSpeed = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);
//        if (_previousVelocity.ToString() != _initialSpeed.ToString())
//        {
//            previousPoints = points;
//            _currentVelocity = _initialSpeed;
//            _currentPosition = pushShootPoint.position;
//            _currentFinished = false;
//            _previousVelocity = _currentVelocity;
//            points.Clear();
//            points.Add(_currentPosition);

//            for (int currentPoints = 0; currentPoints < maxCalculationSteps; currentPoints++)
//            {
//                _currentPosition = GetNextPosition(_currentPosition, _currentVelocity);
//                points.Add(_currentPosition);
//                _currentVelocity += Physics.gravity * _currentCustomGravity * timeStep;
//            }
//        }
//    }

//    public void DrawTrayectory(bool draw)
//    {
//        if (draw) {
//            SetHitMark();
//        }
//        else
//        {

//        }
//    }

//    Vector3 GetNextPosition(Vector3 currentPoint, Vector3 velocity)
//    {
//        return currentPoint + velocity * timeStep;
//    }

//    private void ThrowSimulatedProyectile()
//    {
//        _hitMarkCollision.SimulateThrow(_initialSpeed,pushShootPoint.position, Physics.gravity * _currentCustomGravity);
//    }

//    private void SetHitMark()
//    {
//        if (_showTraceTimer == null)
//        {
//            ThrowSimulatedProyectile();
//            List<Vector3> preivousPoints = new List<Vector3>(previousPoints);
//            List<Vector3> interpolationPoints = new List<Vector3>(previousPoints);
//            List<Vector3> currentPoints = new List<Vector3>(points);
//            if (Mathf.Abs(_previousCurrentPoint.y - _currentPoint.y) > magnitudeDiferenceFactorBetweenColisionPoints)
//            {
//                _currentIndex = FindClosestPointAbove(_currentPoint);
//            }
//            currentPoints = RemovePointsInLowerPos(currentPoints, currentPoints[_currentIndex].y);
//            interpolationPoints = RemovePointsInLowerPos(interpolationPoints, interpolationPoints[_currentIndex].y);
//            Vector3 interpolationMarkPosition = Vector3.zero;
//            //int previousIndex = _currentIndex;
//            int cicles = 15;
//            int currentCicles = 0;
//            float timePerInterpolationChange = 1f / (frames * cicles);
//            float timeStep = 0;
//            _showTraceTimer = TimerSystem.Instance.CreateTimer(Time.deltaTime * frames, onTimerDecreaseComplete: () =>
//            {
//                hitMark.SetActive(true);
//                hitMark.transform.SetPositionAndRotation(currentPoints[_currentIndex], Quaternion.LookRotation(_currentNormal));
//                _previousCurrentPoint = _currentPoint;
//                lineRenderer.enabled = true;
//                _showTraceTimer = null;
//            }, onTimerDecreaseUpdate: (float timer) =>
//            {
//                currentCicles = 0;
//                while (currentCicles < cicles)
//                {
//                    timeStep += timePerInterpolationChange;
//                    for (int i = 0; i < currentPoints.Count; i++)
//                    {
//                        interpolationPoints[i] = Vector3.Slerp(preivousPoints[i], currentPoints[i], timePerInterpolationChange);
//                    }
//                    //interpolationMarkPosition = Vector3.Slerp(preivousPoints[previousIndex], currentPoints[_currentIndex], timePerInterpolationChange);
//                    lineRenderer.positionCount = interpolationPoints.Count;
//                    lineRenderer.SetPositions(interpolationPoints.ToArray());
//                    //hitMark.transform.SetPositionAndRotation(interpolationMarkPosition, Quaternion.LookRotation(_currentNormal));
//                    lineRenderer.Simplify(tolerance);
//                    currentCicles++;
//                }
//            });
//        }
//    }

//    private void UnsetHitMark()
//    {
//        if(_showTraceTimer !=null)
//            _showTraceTimer.StopTimer();
//        hitMark.SetActive(false);
//        lineRenderer.enabled = false;
//    }

//   
//    private void OnDisable()
//    {

//    }
//}

public class AimPushShootTrace : MonoBehaviour
{
    [Header("Settings")]
    [Range(1f, 15f)] [SerializeField] private int frames = 5;
    [Range(0.01f, 0.1f)] [SerializeField] public float timeStep = 0.02f;
    [SerializeField] private int maxCalculationSteps = 128;
    [Range(0.01f, 1.0f)] [SerializeField] private float tolerance;
    [SerializeField] private float magnitudeDiferenceFactorBetweenColisionPoints = 1.5f;

    [Header("HitMark")]
    [SerializeField] private GameObject hitMark;
    [SerializeField] private AimPushShootHitMarkCollision _hitMarkCollision;

    [Header("Resources")]
    [SerializeField] private Transform pushShootPoint;
    [SerializeField] private PushShootHandle pushShootHandle;
    [SerializeField] private LineRenderer lineRenderer;

    private List<Vector3> points = new List<Vector3>();
    private List<Vector3> previousPoints = new List<Vector3>();
    private Vector3 _currentVelocity;
    private float _currentCustomGravity;
    private Vector3 _currentPosition;
    private Vector3 _previousVelocity;
    private bool _currentFinished = true;
    public UnityEvent currentFinishedEvent;
    private Vector3 _initialSpeed;
    private Vector3 _currentPoint;
    private Vector3 _currentNormal;
    private Vector3 _previousCurrentPoint;
    private int _currentIndex = 0;
    private ITimer _showTraceTimer;

    void Start()
    {
        if (!lineRenderer) lineRenderer = GetComponent<LineRenderer>();
        _hitMarkCollision.onCollision.AddListener(SetPointAndNormalForHitMark);
        CalculateTrayectory();
    }

    void Update()
    {
        CalculateTrayectory();
    }

    private void CalculateTrayectory()
    {
        _initialSpeed = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);

        if (Vector3.SqrMagnitude(_previousVelocity - _initialSpeed) > 0.001f)
        {
            previousPoints = new List<Vector3>(points);
            _currentVelocity = _initialSpeed;
            _currentPosition = pushShootPoint.position;
            points.Clear();
            points.Add(_currentPosition);

            for (int i = 0; i < maxCalculationSteps; i++)
            {
                _currentVelocity += Physics.gravity * _currentCustomGravity * timeStep;
                _currentPosition += _currentVelocity * timeStep;
                points.Add(_currentPosition);
            }
            _previousVelocity = _initialSpeed;
        }
    }

    public int FindClosestPointAbove(Vector3 targetPoint)
    {
        int closestIndex = 0;
        float minYDifference = float.MaxValue;

        for (int i = 0; i < points.Count; i++)
        {
            float yDiff = points[i].y - targetPoint.y;
            if (yDiff > 0 && yDiff < minYDifference)
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
        _currentIndex = FindClosestPointAbove(_currentPoint);
        hitMark.transform.SetPositionAndRotation(points[_currentIndex], Quaternion.LookRotation(_currentNormal));
        if (_showTraceTimer == null)
        {
            ThrowSimulatedProyectile();
            List<Vector3> filteredCurrentPoints = RemovePointsInLowerPos(new List<Vector3>(points), points[_currentIndex].y);
            List<Vector3> filteredPreviousPoints = RemovePointsInLowerPos(new List<Vector3>(previousPoints), previousPoints[_currentIndex].y);
           
            _showTraceTimer = TimerSystem.Instance.CreateTimer(
                Time.deltaTime * frames,
                onTimerDecreaseComplete: () =>
                {
                    hitMark.SetActive(true);
                    _previousCurrentPoint = _currentPoint;
                    lineRenderer.enabled = true;
                    _showTraceTimer = null;
                },
                onTimerDecreaseUpdate: (float timer) =>
                {
                    float progress = 1f - (timer / (Time.deltaTime * frames));
                    lineRenderer.positionCount = filteredCurrentPoints.Count;

                    for (int i = 0; i < filteredCurrentPoints.Count; i++)
                    {
                        Vector3 interpolatedPos = Vector3.Slerp(
                            filteredPreviousPoints[i],
                            filteredCurrentPoints[i],
                            progress
                        );
                        lineRenderer.SetPosition(i, interpolatedPos);
                    }
                    lineRenderer.Simplify(tolerance);
                }
            );
        }
    }

    private void SetPointAndNormalForHitMark(Vector3 point, Vector3 normal, bool colliding) =>
        (_currentPoint, _currentNormal) = (point, normal);


    private void ThrowSimulatedProyectile() =>
        _hitMarkCollision.SimulateThrow(_initialSpeed, pushShootPoint.position, Physics.gravity * _currentCustomGravity);

    public void DrawTrayectory(bool draw)
    {
        if (draw) SetHitMark();
        else UnsetHitMark();
    }

    private void UnsetHitMark()
    {
        if (_showTraceTimer != null) _showTraceTimer.StopTimer();
        hitMark.SetActive(false);
        lineRenderer.enabled = false;
    }

    private void OnDisable() => UnsetHitMark();
}