using Game;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAIBrain : MonoBehaviour
{
    //Setted in inspector
    [SerializeField] NavMeshAgent agent;
    NavMeshPath _currentPath;
    Vector3[] _previousPath;
    [SerializeField] float fleeMagnitude = 5f;
    [SerializeField] float sweepRadius = 20f;
    [SerializeField] float shootingMarginErrorAngle = 2f;
    [SerializeField] LayerMask sweepLayerMask;


    private Vector3 _positionToDrive;
    private GameObject _player;
    private GameObject _targetToShoot;
    //path index starts at 1 because 0 is their actual position
    private int _pathIndex = START_CORNER_INDEX;
    private float _minDistanceUntilNextCorner = 3f;
    public Vector3 PositionToDrive { get => _positionToDrive; set => _positionToDrive = value; }
    public GameObject Player { get => _player; set => _player = value; }
    public GameObject Target { get => _targetToShoot; set => _targetToShoot = value; }

    [SerializeField] NormalShootHandle normalShootHandle;
    [SerializeField] PushShootHandle pushShootHandle;
    [SerializeField] CarMovementController carMovementController;
    [SerializeField] PhysicsBehaviour physicsBehaviour;
    [SerializeField] StatsController statsController;

    public enum PathMode
    {
        SIMPLE,
        ADVANCED
    }

    PathMode pathMode = PathMode.ADVANCED;

    const float MAX_ANGLE_DIRECTION = 90f;
    const float FRONT_ANGLE = 20f;
    const int START_CORNER_INDEX = 1;
    Vector3 startPosition;
    private void Awake()
    {
        // TODO change this to the correct way of referencing the player
        _player = GameObject.FindGameObjectWithTag("Player");

        //Testing 
        _targetToShoot = _player;
        if (!agent)
        {
            agent = GetComponentInChildren<NavMeshAgent>();
        }
        if (!normalShootHandle)
        {
            normalShootHandle = GetComponentInChildren<NormalShootHandle>();
        }   
        if (!pushShootHandle)
        {
            pushShootHandle = GetComponentInChildren<PushShootHandle>();
        }
        if (!carMovementController)
        {
            carMovementController = GetComponentInChildren<CarMovementController>();
        }
        if (!physicsBehaviour)
        {
            physicsBehaviour = GetComponentInChildren<PhysicsBehaviour>();
        }
        if (!statsController)
        {
            statsController = GetComponentInChildren<StatsController>();
        }
        physicsBehaviour.Rb = GetComponent<Rigidbody>();
        _currentPath = new NavMeshPath();
        _previousPath = new Vector3[0];
        startPosition=  this.transform.position;
    }
    public void ReturnToStartPosition()
    {
        this.transform.position = startPosition;
        StopMovement();
    }
    public float GetHealth()
    {
        return statsController.Endurance;
    }

    #region Actions

    #region Common 
    public void GoToPosition()
    {
  
        //If it's negative, go left
        //If it's positive, go right
        float angle = GetAngleDirection();

        //Left/Right
        float inputX = Mathf.Min(angle / MAX_ANGLE_DIRECTION, 1f);
        //Forward/Backward
        float inputY = 1f;
        //Debug.Log("DIRECTION:" + direction);
        //Debug.Log("XXXXXXXXXX:" + inputX);
        //Debug.Log("YYYYYYYYYY:" + inputY);

        Vector2 input = new Vector2(inputX, inputY);


        carMovementController.HandleSteeringInput(input);
    }

    #endregion

    #region SearchState

    [Obsolete("We don't use setDestination anymore")]
    public void GoToPositionDeprecated()
    {
        agent.SetDestination(_positionToDrive);
    }

    public void SearchPlayerPosition()
    {
        AssignTarget(_player);
        _positionToDrive = _player.transform.position;
    }
    #endregion

    #region CombatState
    public void NormalShoot()
    {
        normalShootHandle.CurrentShootDirection = GetShootingDirectionWithError();
        normalShootHandle.NormalShooting();
    }
    public void StopDelayDecreaseOveheat()
    {
        normalShootHandle.StopDelayDecreaseOverheat();
    }
    public void DecreaseOverheatTime()
    {
        normalShootHandle.DecreaseOverheatTime();
    }
    public void PushShoot()
    {
        pushShootHandle.CurrentShootDirection = GetShootingDirectionWithError();
        pushShootHandle.PushShooting();
    }

    public void UseUniqueAbility()
    {
        //TODO
        //Use unique ability
    }
    #endregion

    #region DashState
    public void Dash()
    {
        //TODO
        carMovementController.HandleDashWithPhysics();
    }
   
    #endregion

    #region FleeState
    public void RunAwayFromCurrentTarget()
    {
        _positionToDrive = -CalcNormalizedTargetDirection() * fleeMagnitude;  
    }
   
    #endregion
    #endregion

    #region Decisions
    public bool EnemySweep()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, sweepRadius, sweepLayerMask);
     
        return colliders.Length > 0;
    }

    public bool IsPushShootReady()
    {  
        return pushShootHandle.CanShoot;
    }

    public bool IsShootOverheated()
    { 
        return normalShootHandle.IsOverHeat;
    }
    public bool IsUniqueAbilityFinished()
    {
        //TODO
        //Ask if unique ability is finished
        return true;
    }

    public bool IsDashReady()
    { 
        return carMovementController.CanDash;
    }

    public bool IsDashFinished()
    {
        return !carMovementController.IsDashing;
    }

    public bool IsInValidRange(float distance)
    {
        return distance > Vector3.Distance(transform.position, _positionToDrive);
    }

    public bool IsInFront()
    {
        float angle = GetAngleDirection();
        return (angle > -FRONT_ANGLE && angle < FRONT_ANGLE);
    }
    #endregion

    #region Helpers

    private void AssignTarget(GameObject target)
    {
        _targetToShoot = target;
        _positionToDrive = _targetToShoot.transform.position;
    }

    private Vector3 CalcNormalizedTargetDirection()
    {
        return (_targetToShoot.transform.position - transform.position).normalized;
    }

    private float GetAngleDirection()
    { 
        Vector3 direction;
        switch (pathMode)
        {
            default:
            case PathMode.SIMPLE:
                direction = CalcNormalizedTargetDirection();
                break;
            case PathMode.ADVANCED:
                if (agent.CalculatePath(_positionToDrive, _currentPath))
                {
                    if (_previousPath.Length != _currentPath.corners.Length)
                    {
                        ResetIndexPath();
                    }
                    
                    for (int i = 0; i < _currentPath.corners.Length - 1; i++)
                        Debug.DrawLine(_currentPath.corners[i], _currentPath.corners[i + 1], Color.yellow);

                    _positionToDrive = GetActivePathPoint();
                    CheckIfGoToNextPathPoint();
                }
                else
                {
                    Debug.LogError("No path to follow" + _currentPath.ToString());
                };
          
                break;
        }
        direction = (GetActivePathPoint() - transform.position).normalized;

        //If it's negative, go left
        //If it's positive, go right
        return Vector3.SignedAngle(transform.forward, direction, Vector3.up);
    }
    private Vector3 GetActivePathPoint()
    {
        switch (_currentPath.corners.Length)
        {
            case 1:
                Debug.LogError("THE PATH ONLY HAS ONE POINT. This is probably because you put the car too far away from the ground");
                return _currentPath.corners[0];
            case > 0:
                return _currentPath.corners[_pathIndex];
            default:
                return Vector3.zero;
        }
    }

    private void CheckIfGoToNextPathPoint()
    {
        if (Vector3.Distance(transform.position, GetActivePathPoint()) < _minDistanceUntilNextCorner && 
            (_pathIndex +1) < _currentPath.corners.Length )
        {
            _pathIndex++;
        }
    }

    private void ResetIndexPath()
    {
        _pathIndex = START_CORNER_INDEX;
        _previousPath = _currentPath.corners;
    }
     
    private Vector3 GetShootingDirectionWithError()
    {
        Vector3 shootingDirection = CalcNormalizedTargetDirection();

        //Add shooting error 
        return shootingDirection + new Vector3(UnityEngine.Random.Range(-shootingMarginErrorAngle, shootingMarginErrorAngle),
            UnityEngine.Random.Range(0, shootingMarginErrorAngle), 0);
    }

    public void StopMovement()
    {
        physicsBehaviour.Rb.angularVelocity = Vector3.zero;
        physicsBehaviour.Rb.velocity = Vector3.zero;
        carMovementController.StopAllCarMovement();
    }
    #endregion
}
