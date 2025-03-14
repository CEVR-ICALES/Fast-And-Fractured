using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game {
    public class PushShootHandle : ShootingHandle
    {
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {

        }

        protected override void CustomStart()
        {
            base.CustomStart();
        }

        protected override void SetBulletStats(BulletBehaivour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
        }

        public void PushShooting()
        {
            if (canShoot)
            {
                float range = characterStatsController.PushShootRange;
                // Calculate the velocity needed to throw the object to the target at specified angle.
                float angle = characterStatsController.PushShootAngle;
                float projectile_Velocity = range / (Mathf.Sin(2 * angle * Mathf.Deg2Rad) / -Physics.gravity.y);
                // Extract the X  Y componenent of the velocity
                float Vx = Mathf.Sqrt(projectile_Velocity) * Mathf.Cos(angle * Mathf.Deg2Rad);
                float Vy = Mathf.Sqrt(projectile_Velocity) * Mathf.Sin(angle * Mathf.Deg2Rad);
                Vector3 velocity = transform.forward * Vx + transform.up * Vy;
                ShootBullet(velocity, range);
                canShoot = false;
                TimerManager.Instance.StartTimer(characterStatsController.PushCooldown,
                    () => { canShoot = true; },
                    null,
                    "PushShootCooldown of " + characterStatsController.name,
                    false,
                    false
                    ) ;
            }
        }
    }
}
