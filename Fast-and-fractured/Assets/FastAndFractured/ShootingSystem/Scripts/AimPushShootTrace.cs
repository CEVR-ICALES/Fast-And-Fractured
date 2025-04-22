using System.Collections;
using System.Collections.Generic;
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
    [Range(0.01f, 0.1f)]
    public float timeStep = 0.02f;
    [SerializeField] private int maxCalculationSteps = 128;
    [Range(0.01f, 1.0f)]
    [SerializeField] private float tolerance;

    [Header("HitMark")]
    [SerializeField] private GameObject hitMark;
    private List<AimPushShootHitMarkCollision> collisions;

    private LayerMask _groundMask = 3;
    private LayerMask _staticMask = 10;
    private LayerMask combinedMask;
    private bool _collision = false;

    private List<Vector3> points;
    private Vector3 _currentVelocity;
    private float _currentCustomGravity;
    private Vector3 _currentPosition;
    private Vector3 _previousVelocity = Vector3.zero;
    private List<Vector3> _raycastPoints;
    private Vector3 higherPoint;
    private int _pointsPerFrame;
    private bool _currentFinished = true;
    public UnityEvent currentFinishedEvent;
    private int _raycastCount = 0;
    private Vector3 _initialSpeed;

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

        _previousVelocity = Vector3.one;
        _initialSpeed = Vector3.zero;

        combinedMask = (1 << _groundMask) | (1 << _staticMask);
    }

    private void FixedUpdate()
    {
        if (!_currentFinished)
        {
            for (int currentPoints = 0; currentPoints < _pointsPerFrame&&!_currentFinished; currentPoints++)
            {
                if (points.Count >= maxCalculationSteps)
                {
                    _currentFinished = true;
                    lineRenderer.positionCount = points.Count;
                    lineRenderer.SetPositions(points.ToArray());
                    lineRenderer.Simplify(tolerance);
                }
                else
                {
                    _currentPosition = GetNextPosition(_currentPosition, _currentVelocity);
                    points.Add(_currentPosition);
                    _currentVelocity += Physics.gravity * _currentCustomGravity * timeStep;
                    if(currentPoints%2==0)
                    {
                        _raycastPoints.Add(_currentPosition);
                        if (_raycastPoints.Count >= 2)
                        {
                            RaycastHit hit = new RaycastHit();
                            Vector3 vectorForRaycast = _raycastPoints[_raycastCount+1] - _raycastPoints[_raycastCount];
                            Vector3 direction = vectorForRaycast.normalized;
                            float magnitude = vectorForRaycast.magnitude;
                            Ray ray = new Ray(_raycastPoints[0], direction);
                            if (Physics.Raycast(ray, out hit, magnitude, combinedMask))
                            {
                                SetHitMark(hit.point, hit.normal);
                            }
                            Debug.DrawRay(higherPoint, direction * magnitude, Color.green);
                            _raycastCount++;
                        }
                    }
                }
            }
            _raycastPoints.Clear();
            _raycastCount = 0;
        }
    }
    public void DrawTrajectory()
    {
        _initialSpeed = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);
        if (_previousVelocity.ToString() != _initialSpeed.ToString()&&_currentFinished)
        {
            _currentVelocity = _initialSpeed;
            _currentPosition = pushShootPoint.position;
            _currentFinished = false;
            _previousVelocity = _currentVelocity;
            points.Clear();
            points.Add(_currentPosition);
            _pointsPerFrame = maxCalculationSteps / frames;
            _collision = true;
            _raycastPoints = new List<Vector3>();
            _raycastPoints.Clear();
            _raycastCount = 0;
        }
    }

    Vector3 GetNextPosition(Vector3 currentPoint, Vector3 velocity)
    {
        return currentPoint + velocity * timeStep;
    }

    private void SetHitMark(Vector3 point, Vector3 normal)
    {
       hitMark.SetActive(true);
       hitMark.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
       hitMark.transform.Rotate(90, 0, 0);
       points = RemovePointsInLowerPos(point.y);
       lineRenderer.positionCount = points.Count;
       lineRenderer.SetPositions(points.ToArray());
       lineRenderer.Simplify(tolerance);
       _currentFinished = true;
       _collision = false;
    }

    private List<Vector3> RemovePointsInLowerPos(float yPosition)
    {
        List<Vector3> listWithRemovedPoints = new List<Vector3>(points);
        bool breakFor = false;
        for (int i = points.Count -1; i >=0&&!breakFor; i--)
        {
            if (yPosition > points[i].y)
            {
                listWithRemovedPoints.Remove(points[i]);
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
