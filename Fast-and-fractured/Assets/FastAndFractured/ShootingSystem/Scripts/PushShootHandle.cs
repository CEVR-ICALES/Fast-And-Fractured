using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Utilities;

namespace FastAndFractured {
    public class PushShootHandle : ShootingHandle
    {
        protected override void CustomStart()
        {
            base.CustomStart();
        }

        protected override void SetBulletStats(BulletBehaviour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
            PushBulletBehaviour pushBulletBehaviour = (PushBulletBehaviour)bulletBehaivour; 
            pushBulletBehaviour.PushForce = characterStatsController.PushShootForce;
            pushBulletBehaviour.ExplosionRadius = characterStatsController.ExplosionRadius;
            pushBulletBehaviour.ExplosionCenterOffset = characterStatsController.ExplosionCenterOffset;
            pushBulletBehaviour.CustomGravity = Physics.gravity * characterStatsController.PushShootGravityMultiplier;
            pushBulletBehaviour.BouncingNum = characterStatsController.PushShootBounceNum;
            pushBulletBehaviour.BouncingStrenght = characterStatsController.PushShootBounceForce;
        }

        public void PushShooting()
        {
            if (canShoot)
            {
                float range = characterStatsController.PushShootRange;
                float angle = characterStatsController.PushShootAngle;

                //Calcular la velocidad necesaria para llegar al rango especifico con angulo especifico. 
                float projectile_Velocity = Mathf.Sqrt((range * Mathf.Abs(Physics.gravity.y* characterStatsController.PushShootGravityMultiplier)) / Mathf.Sin(2 * angle * Mathf.Deg2Rad));

                //Calcular la velocidad en ambos ejes. 

                float Vx = projectile_Velocity * Mathf.Cos(angle * Mathf.Deg2Rad);
                float Vy = projectile_Velocity * Mathf.Sin(angle * Mathf.Deg2Rad);

                //Rotar el vectorX forward hacia el angulo del currentdirection 

                Vector3 currentShootDirectionWithoutY = new Vector3(currentShootDirection.x, 0, currentShootDirection.z);
                float directionAngle = Vector3.SignedAngle(transform.forward, currentShootDirectionWithoutY, Vector3.up);
                Quaternion rotation = Quaternion.AngleAxis(directionAngle, Vector3.up);

                Vector3 velocityVectorX = transform.forward * Vx;
                
                //Finalmente, se calcula el vector rotado en X y se suma el vector Y.
                Vector3 rotatedVector = (rotation * velocityVectorX) + transform.up * Vy;
                ShootBullet(rotatedVector, range);
                canShoot = false;
                TimerSystem.Instance.CreateTimer(characterStatsController.PushCooldown, onTimerDecreaseComplete:
                    () => { canShoot = true; }
                    );
            }
        }
    }
}
