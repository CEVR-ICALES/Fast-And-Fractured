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
    private LayerMask _hitMarkMask;
    [SerializeField] private LayerMask[] layerMasksForHitMark;

    [Header("Resources")]
     private Transform _pushShootPoint;
    [SerializeField] private PushShootHandle pushShootHandle;
    [SerializeField] private LineRenderer lineRenderer;

    private List<Vector3> _points = new List<Vector3>();
    private float _currentCustomGravity;
    private Vector3 _currentPosition;
    private Vector3 _previousVelocity;
    private Vector3 _initialVelocity;
    private Vector3 _currentContactPoint;
    [SerializeField]
    private float toleranceToVelocityMarginError = 0.001f;
    private int _currentIndex = 0;
    private bool _calculateTracePoints;

    private const float MRUA_DISTANCE_FORMULA_CONSTANT = 0.5f;
    private const float MARGIN_TO_FIND_COLSEST_HIT_POINT = 1.5f;
    protected override void Construct()
    {
        
    }
    protected override void Initialize()
    {
        base.Initialize();
        if (!lineRenderer) lineRenderer = GetComponent<LineRenderer>();
        if (!pushShootHandle) pushShootHandle = transform.parent.GetComponentInChildren<PushShootHandle>();
        _pushShootPoint = pushShootHandle.PushShootPoint;
        _hitMarkMask = layerMasksForHitMark[0];

        for (int i = 1; i < layerMasksForHitMark.Length; i++)
        {
            _hitMarkMask |= layerMasksForHitMark[i];
        }
        CalculateTrayectory();
        hitMark.SetActive(false);
        _initialVelocity = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);
        _calculateTracePoints = false;
        RaycastToSetHitMark();
    }

    private void FixedUpdate()
    {
        if (_calculateTracePoints) 
        {
            CalculateTrayectory();
            RaycastToSetHitMark();
        }
    }

    private void RaycastToSetHitMark()
    {
        int highestPointIndex = ReturnCurrentHighestPointIndex();
        Ray ray = new Ray(_points[highestPointIndex],Vector3.down);
        if (Physics.Raycast(ray, out var hitInfo, Mathf.Infinity, _hitMarkMask))
        {
            int impactPoint = FindClosestPointAbove(hitInfo.point + MARGIN_TO_FIND_COLSEST_HIT_POINT * Vector3.up);
            Vector3 impactHigherPoint = new Vector3(_points[impactPoint].x, _points[highestPointIndex].y, _points[impactPoint].z);
            ray = new Ray(impactHigherPoint,Vector3.down);
            if (Physics.Raycast(ray, out var hitInfo1, Mathf.Infinity,_hitMarkMask))
            {
                _currentContactPoint = hitInfo1.point;
            }
        }
    }

    private int ReturnCurrentHighestPointIndex()
    {
        int highestPoint = 0;
        float previousYPoint = float.MinValue;
        for (int i = 0; i < _points.Count; i++)
        {
            if (previousYPoint > _points[i].y) 
                return highestPoint;
            highestPoint = i;
            previousYPoint = _points[i].y;
        }
        return highestPoint;
    }

    private void CalculateTrayectory()
    {
        _initialVelocity = pushShootHandle.GetCurrentParabolicMovementOfPushShoot(out _currentCustomGravity);

        if (Vector3.SqrMagnitude(_previousVelocity - _initialVelocity) > toleranceToVelocityMarginError)
        {
            _currentPosition = _pushShootPoint.position;
            _points.Clear();
            _points.Add(_currentPosition);
            float currentTimeStep = 0;

            for (int i = 0; i < maxCalculationSteps; i++)
            {
                currentTimeStep += timeStep;
                _currentPosition = _pushShootPoint.position + _initialVelocity * currentTimeStep + MRUA_DISTANCE_FORMULA_CONSTANT * Physics.gravity * _currentCustomGravity * currentTimeStep * currentTimeStep;
                _points.Add(_currentPosition);
            }
            _previousVelocity = _initialVelocity;
        }
    }

    public int FindClosestPointAbove(Vector3 targetPoint,float startLoopIndexPoint = 0)
    {
        int closestIndex = 0;
        float minYDifference = float.MaxValue;

        for (int i = 0; i < _points.Count; i++)
        {
            float yDiff = Mathf.Abs(_points[i].y - targetPoint.y);
            if (yDiff < minYDifference&&i>=startLoopIndexPoint)
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
        _currentIndex = FindClosestPointAbove(_currentContactPoint,ReturnCurrentHighestPointIndex());
        List<Vector3> filteredCurrentPoints = RemovePointsInLowerPos(new List<Vector3>(_points), _points[_currentIndex].y);
        hitMark.transform.position = _points[_currentIndex];
        lineRenderer.positionCount = filteredCurrentPoints.Count;
        lineRenderer.SetPositions(filteredCurrentPoints.ToArray());
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
    }
}