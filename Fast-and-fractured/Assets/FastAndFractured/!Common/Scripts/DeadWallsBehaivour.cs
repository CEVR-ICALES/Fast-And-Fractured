using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

public class DeadWallsBehaivour : MonoBehaviour, IKillCharacters, IPausable
{
    [SerializeField]
    private float timeTillKill = 1.5f;
    [SerializeField]
    private int killPriority = 2;
    [SerializeField]
    private float frontAngle = 180;

    private bool _isPaused = false;
    public int KillPriority => killPriority;

    public float KillTime => timeTillKill;

    const float DAMAGE_TO_CHARACTERS = 0;

    private void OnEnable()
    {
        PauseManager.Instance.RegisterPausable(this);
    }

    private void OnDisable()
    {
        PauseManager.Instance?.UnregisterPausable(this);
    }

    private void Start()
    {
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out StatsController statsController))
        {
            if (!other.GetComponent<Rigidbody>().isKinematic)
            {
                StartKillNotify(statsController);
                if (LevelControllerButBetter.Instance.playerReference == statsController.gameObject)
                {
                    IngameEventsManager.Instance.CreateEvent("Events.OutsideMap", 2f);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other) 
    {
        if (other.TryGetComponent(out StatsController statsController))
        {
            if (!other.GetComponent<Rigidbody>().isKinematic && !_isPaused)
            {
                if (IsCharacterInFront(other.transform))
                {
                    CharacterEscapedDead(statsController);
                }
            }
        }
    }

    public bool IsCharacterInFront(Transform target)
    {
        Vector3 directionToTarget = target.position - (transform.position);

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

    public void OnPause()
    {
        _isPaused = true;
    }

    public void OnResume()
    {
        _isPaused = false;
    }
}
