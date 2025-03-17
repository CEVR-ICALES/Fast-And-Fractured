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
    [SerializeField] float fleeMagnitude = 5f;
    [SerializeField] float sweepRadius = 20f;
    [SerializeField] float shootingMarginErrorAngle = 2f;
    [SerializeField] LayerMask sweepLayerMask;

    private Vector3 _positionToDrive;
    private GameObject _player;
    private GameObject _targetToShoot;
    public Vector3 PositionToDrive { get => _positionToDrive; set => _positionToDrive = value; }
    public GameObject Player { get => _player; set => _player = value; }
    public GameObject Target { get => _targetToShoot; set => _targetToShoot = value; }

    [SerializeField] NormalShootHandle normalShootHandle;

    private void Start()
    {
        // TODO change this to the correct way of referencing the player
        _player = GameObject.FindGameObjectWithTag("Player");

        //Testing 
        _targetToShoot = _player;
        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        if (!normalShootHandle)
        {
            normalShootHandle = GetComponentInChildren<NormalShootHandle>();
        }
    }

    #region Actions
    #region SearchState

    public void GoToPosition()
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
        Vector3 shootingDirection = CalcNormalizedTargetDirection();

        //Add shooting error 
        shootingDirection += new Vector3(UnityEngine.Random.Range(-shootingMarginErrorAngle, shootingMarginErrorAngle),
            UnityEngine.Random.Range(0, shootingMarginErrorAngle), 0);

        normalShootHandle.CurrentShootDirection = shootingDirection;
        normalShootHandle.NormalShooting();
    }

    public void PushShoot()
    {
        //TODO
        //Shoot pushign bullet to target
        //if target and target is nearby
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
        //Dash
    }
    public bool IsDashAvailable()
    {
        //TODO
        //Is dash off cooldown
        return false;
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
        //TODO
        //Ask if push shoot is ready
        return true;
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
        //TODO
        //Ask if dash is ready
        return true;
    }

    public bool IsDashFinished()
    {
        //TODO
        //Has dash finished
        return false;
    }
    #endregion

    #region Helpers

    private void AssignTarget(GameObject target)
    {
        _targetToShoot = target;
    }

    private Vector3 CalcNormalizedTargetDirection()
    {
        return (_targetToShoot.transform.position - transform.position).normalized;
    }

    #endregion
}
