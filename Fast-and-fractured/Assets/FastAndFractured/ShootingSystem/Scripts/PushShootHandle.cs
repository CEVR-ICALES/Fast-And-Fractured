using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;
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
                // Aumentar la velocidad inicial
                float projectile_Velocity = Mathf.Sqrt((range * Mathf.Abs(Physics.gravity.y* characterStatsController.PushShootGravityMultiplier)) / Mathf.Sin(2 * angle * Mathf.Deg2Rad));

                // Calcular las componentes de la velocidad con el nuevo ángulo
                float Vx = projectile_Velocity * Mathf.Cos(angle * Mathf.Deg2Rad);
                float Vy = projectile_Velocity * Mathf.Sin(angle * Mathf.Deg2Rad);
                Quaternion vectorRotation = Quaternion.FromToRotation(transform.forward, currentShootDirection.z * Vector3.forward);
                // Vector de velocidad final
                transform.rotation = vectorRotation;
                Vector3 velocity = (transform.forward * Vx + transform.up * Vy);
                ShootBullet(velocity, range);
                canShoot = false;
                TimerManager.Instance.StartTimer(characterStatsController.PushCooldown,
                    () => { canShoot = true; },
                    null,
                    "PushShootCooldown of " + characterStatsController.name,
                    false,
                    false
                    );
            }
        }
    }
}
