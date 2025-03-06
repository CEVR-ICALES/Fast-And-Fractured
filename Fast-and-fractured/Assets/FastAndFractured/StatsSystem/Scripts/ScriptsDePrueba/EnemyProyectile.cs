using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class EnemyProyectile : MonoBehaviour
{
    public float damage = 10f;
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent<PlayerBehaivour>(out var target))
        {
            if (target.TryGetComponent<StatsController>(out var targetHP))
            {
                targetHP.TakeDamage(damage, false);
            }
        }
    }
}
