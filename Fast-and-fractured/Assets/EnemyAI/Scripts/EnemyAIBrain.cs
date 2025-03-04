using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class EnemyAIBrain : MonoBehaviour
{
    //Setted in inspector
    [SerializeField] NavMeshAgent agent;

    private Vector3 _positionToDrive;
    public Vector3 PositionToDrive { get => _positionToDrive; set => _positionToDrive = value; }

    public void GoToPosition()
    {
        agent.SetDestination(_positionToDrive);
    }
}
