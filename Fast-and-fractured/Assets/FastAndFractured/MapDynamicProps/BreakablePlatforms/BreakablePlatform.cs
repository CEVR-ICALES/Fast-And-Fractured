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
    [SerializeField] private MeshCollider meshCollider;
    [SerializeField] private MeshRenderer platform;
    [SerializeField] private MeshRenderer semiBreakePlatform;
    [SerializeField] private MeshRenderer breakePlatform;
    private int currentCharacters = 0;
    private ITimer breakTimeTimer = null;
    private ITimer semiBreakTimeTimer = null;


    private int bulletHits = 0;
    private bool isBreaking = false;

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
        semiBreakePlatform.enabled = false;
        meshCollider.enabled = false;
        breakePlatform.enabled = true;
    }

    private void SemiBreakPlatform()
    {
        platform.enabled = false;
        semiBreakePlatform.enabled = true;
    }
}
