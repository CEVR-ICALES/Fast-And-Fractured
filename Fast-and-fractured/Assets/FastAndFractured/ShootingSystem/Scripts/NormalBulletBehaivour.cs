using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class NormalBulletBehaivour : BulletBehaivour
    {
        private void Start()
        {
        }
        protected override void FixedUpdate()
        {
            if ((transform.position - initPosition).magnitude >= range)
            {
                OnBulletEndTrayectory();
            }
        }

        protected override void OnTriggerEnter(Collider other)
        {
            //Provisional till layer decision
            if (other.TryGetComponent<ShootEnemy>(out var shootEnemy))
            {
                if (other.TryGetComponent<StatsController>(out var statsController))
                {
                    statsController.TakeEndurance(damage, false);
                    ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
                }
            }
        }
    }
}
