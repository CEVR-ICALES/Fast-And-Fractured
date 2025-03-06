using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAIBrain : MonoBehaviour
{
    //Setted in inspector
    [SerializeField] NavMeshAgent agent;
    [SerializeField] float fleeMagnitude = 5;

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

    public void GoToPosition()
    {
        agent.SetDestination(_positionToDrive);
    }

    public void SearchPlayerPosition()
    {
        AssignTarget(_player);
        _positionToDrive = _player.transform.position;
    }

    public void NormalShoot()
    {
        //TODO
        //Shoot normal bullet to target
    }

    public void PushShoot()
    {
        //TODO
        //Shoot pushign bullet to target
    }

    public void Dash()
    {
        //TODO
        //Dash
    }

    public void UseUniqueAbility()
    {
        //TODO
        //Use unique ability
    }
    public bool IsDashAvailable()
    {
        return false;
    }

    public void RunAwayFromCurrentTarget()
    {
       Vector3 targetDirection = (transform.position - _targetToShoot.transform.position).normalized;

        _positionToDrive = targetDirection * fleeMagnitude;
    }


    private void AssignTarget(GameObject target)
    {
        _targetToShoot = target;
    }

    internal bool IsDashFinished()
    {
        return false;
    }
}
