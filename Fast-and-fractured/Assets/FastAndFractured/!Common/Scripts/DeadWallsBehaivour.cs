using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class DeadWallsBehaivour : MonoBehaviour, IKillCharacters
{
    [SerializeField]
    private float timeTillKill = 1.5f;
    [SerializeField]
    private int killPriority = 2;
    [SerializeField]
    private float frontAngle = 180;
    private Dictionary<StatsController,ITimer> _charactersDontExitTheCollider;
    public int KillPriority => killPriority;

    public float KillTime => timeTillKill;

    const float DAMAGE_TO_CHARACTERS = 0;


    private void Start()
    {
        _charactersDontExitTheCollider = new Dictionary<StatsController,ITimer>();
    }
    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out StatsController controller))
        {
            StartKillNotify(controller);
            IngameEventsManager.Instance.CreateEvent("¡¡¡¡¡¡¡Vuelve a la zona de juego rápido!!!!!",2f);
        }
    }

    private void OnTriggerExit(Collider other) {
        if (other.TryGetComponent(out StatsController controller))
        {
            if (IsCharacterInFront(other.transform))
            {
                CharacterEscapedDead(controller);
            }
        }
    }

    public bool IsCharacterInFront(Transform target)
    {
        Vector3 directionToTarget = target.position - (transform.position + transform.lossyScale);

        directionToTarget.Normalize();
        float dotProduct = Vector3.Dot(transform.forward, directionToTarget);
        float angleToTarget = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
        return angleToTarget < (frontAngle / 2f);
    }

    public void StartKillNotify(StatsController statsController)
    {
        statsController.GetKilledNotify(this, false,DAMAGE_TO_CHARACTERS);
    }
    public void CharacterEscapedDead(StatsController statsController)
    {
        statsController.GetKilledNotify(this, true,DAMAGE_TO_CHARACTERS);
    }

    public GameObject GetKillerGameObject()
    {
        return gameObject;
    }
}
