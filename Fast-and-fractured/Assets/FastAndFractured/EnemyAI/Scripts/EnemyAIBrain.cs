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
    [SerializeField] LayerMask sweepLayerMask;

    private Vector3 _positionToDrive;
    private GameObject _player;
    private GameObject _targetToShoot;
    public Vector3 PositionToDrive { get => _positionToDrive; set => _positionToDrive = value; }
    public GameObject Player { get => _player; set => _player = value; }
    public GameObject Target { get => _targetToShoot; set => _targetToShoot = value; }



    private void Start()
    {
        _player = GameObject.FindGameObjectWithTag("Player");
        if (!agent)
        {
            agent = GetComponent<NavMeshAgent>();
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
        //TODO
        //Shoot normal bullet to target
        //if target and target is nearby and shoot is not overheated
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
       Vector3 targetDirection = (transform.position - _targetToShoot.transform.position).normalized;

        _positionToDrive = targetDirection * fleeMagnitude;
    }
    public bool IsDashFinished()
    {
        //TODO
        //Has dash finished
        return false;
    }
    #endregion
    #endregion

    #region Decisions
    public bool EnemySweep()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, sweepRadius, sweepLayerMask);
     
        return colliders.Length > 0;
    }

    public bool IsPushShootOnCooldown()
    {
        //TODO
        //Ask if push shoot is on cooldown
        return false;
    }

    public bool IsShootOverheated()
    {
        //TODO
        //Ask if shoot is overheated
        return false;
    }
    public bool IsUniqueAbilityFinished()
    {
        //TODO
        //Ask if unique ability is finished
        return true;
    }
    #endregion

    #region Helpers

    private void AssignTarget(GameObject target)
    {
        _targetToShoot = target;
    }

   
    #endregion
}
