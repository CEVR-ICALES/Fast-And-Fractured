using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
using Utilities;

public class BreakablePlatform : MonoBehaviour
{
    [SerializeField] private float breakTime = 15f;
    [Range(0.1f, 0.7f)]
    [SerializeField] private float semiBreakTimePercentage = 0.5f;
    [SerializeField] private float bulletsSubtractTime = 0.2f;
    [SerializeField] private float explosionSubtractTime = 0.5f;
    private MeshCollider _meshCollider;
    private MeshRenderer[] _platformFaces;
    private int _platformIndex = 0;
    [SerializeField]
    private Transform SplinesParent;
    private int currentCharacters = 0;
    private ITimer breakTimeTimer = null;
    private ITimer semiBreakTimeTimer = null;

    private const int BREAKABLE_PlATFORM_FASES = 3;

    private int bulletHits = 0;
    private bool isBreaking = false;

    private void Start()
    {
        _platformFaces = new MeshRenderer[BREAKABLE_PlATFORM_FASES];
        for(int i = 0; i < BREAKABLE_PlATFORM_FASES; ++i)
        {
            if (SplinesParent.GetChild(i) != null) 
            {
                _platformFaces[i] = SplinesParent.GetChild(i).GetComponent<MeshRenderer>();
                if (i != 0 && _platformFaces[i].enabled)
                {
                    _platformFaces[i].enabled = false;
                }
            }
        }
        _meshCollider = _platformFaces[0].GetComponent<MeshCollider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBreaking) return;
        if (other.GetComponent<StatsController>())
        {
            // Store entry time
            if (breakTimeTimer == null)
            {
                    breakTimeTimer = TimerSystem.Instance.CreateTimer(breakTime, onTimerDecreaseComplete: () =>
                    {
                        BreakPlatform();
                    });
                    semiBreakTimeTimer = TimerSystem.Instance.CreateTimer(breakTime * semiBreakTimePercentage, onTimerDecreaseComplete: () =>
                    {
                        SemiBreakPlatform();
                    });
            }
            else
            {
                if (breakTimeTimer.GetData().IsPaused)
                {
                    breakTimeTimer.ResumeTimer();
                    semiBreakTimeTimer.ResumeTimer();
                }
            }
            currentCharacters++;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isBreaking) return;
        if (other.GetComponent<StatsController>())
        {   
            currentCharacters--;
            if(currentCharacters == 0)
            {
                breakTimeTimer.PauseTimer();
                semiBreakTimeTimer.PauseTimer();
            }
        }
    }

    //private void OnCollisionEnter(Collision collision)
    //{
    //    if (isBreaking) return;

    //    PushBulletBehaviour bullet = collision.gameObject.GetComponent<PushBulletBehaviour>();
    //    if (bullet)
    //    {
    //        bulletHits++;
    //        if (bulletHits >= bulletsSubtractTime)
    //        {
    //            BreakPlatform();
    //        }
    //    }
    //}

    private void BreakPlatform()
    {
        isBreaking = true;
        _platformFaces[_platformIndex - 1].enabled = false;
        _platformFaces[_platformIndex].enabled = true;
        _meshCollider.enabled = false;
    }

    private void SemiBreakPlatform()
    {
        _platformIndex++;
        _platformFaces[_platformIndex-1].enabled = false;
        _platformFaces[_platformIndex].enabled = true;
    }
}
