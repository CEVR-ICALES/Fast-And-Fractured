using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;

public class BreakablePlatform : MonoBehaviour
{
    [SerializeField] private float breakTime = 15f;
    [SerializeField] private int maxBulletHits = 3;
    [SerializeField] private Animator animator;

    private int bulletHits = 0;
    private float accumulatedVehicleTime = 0f;
    private bool isBreaking = false;
    private Dictionary<Collider, float> vehicleEntryTimes = new Dictionary<Collider, float>();

    private void OnTriggerEnter(Collider other)
    {
        if (isBreaking) return;
        if (other.GetComponent<StatsController>())
        {
            // Store entry time
            vehicleEntryTimes[other] = Time.time;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (isBreaking) return;
        if (other.GetComponent<StatsController>() && vehicleEntryTimes.ContainsKey(other))
        {
            // Calculate time spent and accumulate
            float entryTime = vehicleEntryTimes[other];
            float timeSpent = Time.time - entryTime;
            accumulatedVehicleTime += timeSpent;
            vehicleEntryTimes.Remove(other);

            if (accumulatedVehicleTime >= breakTime)
            {
                BreakPlatform();
            }
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isBreaking) return;

        PushBulletBehaviour bullet = collision.gameObject.GetComponent<PushBulletBehaviour>();
        if (bullet)
        {
            bulletHits++;
            if (bulletHits >= maxBulletHits)
            {
                BreakPlatform();
            }
        }
    }

    private void BreakPlatform()
    {
        isBreaking = true;
        // More stuff (anim, particles, etc.)
        gameObject.SetActive(false);
    }
}
