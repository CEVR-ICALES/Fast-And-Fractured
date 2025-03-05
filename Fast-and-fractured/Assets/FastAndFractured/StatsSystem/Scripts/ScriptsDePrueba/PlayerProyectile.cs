using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class PlayerProyectile : MonoBehaviour
{
    [SerializeField] private float damage = 10f;
    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent<ShootEnemy>(out var target))
        {
            if(target.TryGetComponent<StatsController>(out var targetHP))
            {
                targetHP.TakeDamage(damage,false);
                Destroy(gameObject);
            }
        }
    }
}
