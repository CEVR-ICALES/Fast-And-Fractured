using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SocialPlatforms;
using Utilities;

namespace FastAndFractured {
    public class PushShootHandle : ShootingHandle, ITimeSpeedModifiable
    {
        public UnityEvent<float, float> onCooldownUpdate;
        public bool ShootingMine => _shootingMine;
        private bool _shootingMine = false;
        public bool IsPushShooting => _isPushShooting;
        private bool _isPushShooting = false;
        public Transform PushShootPoint=>shootPoint;

        ITimer _pushShootCooldown;
        protected override void SetBulletStats(BulletBehaviour bulletBehaivour)
        {
            base.SetBulletStats(bulletBehaivour);
            PushBulletBehaviour pushBulletBehaviour = (PushBulletBehaviour)bulletBehaivour; 
            pushBulletBehaviour.PushForce = characterStatsController.PushShootForce;
            pushBulletBehaviour.ExplosionRadius = characterStatsController.ExplosionRadius;
            pushBulletBehaviour.ExplosionCenterOffset = characterStatsController.ExplosionCenterOffset;
            pushBulletBehaviour.CustomGravity = Physics.gravity * characterStatsController.PushShootGravityMultiplier;
            if (!_shootingMine)
            {
                pushBulletBehaviour.BouncingNum = characterStatsController.PushShootBounceNum;
                pushBulletBehaviour.BouncingStrenght = characterStatsController.PushShootBounceForce;
                pushBulletBehaviour.TimeToExplode = 0;
            }
            else
            {
                pushBulletBehaviour.TimeToExplode = characterStatsController.MineExplosionTime;
                pushBulletBehaviour.BouncingNum = 0;
                pushBulletBehaviour.BouncingStrenght = 0;
            }
        }

        public void PushShooting()
        {
            if (canShoot)
            {
                canShoot = false;
                _isPushShooting = true;
               float range = characterStatsController.PushShootRange;
               float angle = characterStatsController.PushShootAngle;
                CalculateInitialVelocityForParabolicMovement(range, angle, out float Vx, out float Vy);

                //Rotar el vectorX forward hacia el angulo del currentdirection 
                Vector3 currentShootDirectionWithoutY = new Vector3(currentShootDirection.x, 0, currentShootDirection.z);
                float directionAngle = Vector3.SignedAngle(transform.forward, currentShootDirectionWithoutY, Vector3.up);
                Quaternion rotation = Quaternion.AngleAxis(directionAngle, Vector3.up);
                Vector3 velocityVectorX = transform.forward * Vx;
                
                //Finalmente, se calcula el vector rotado en X y se suma el vector Y.
                Vector3 rotatedVector = (rotation * velocityVectorX) + transform.up * Vy;
                ShootBullet(rotatedVector, range);
                _pushShootCooldown = TimerSystem.Instance.CreateTimer(characterStatsController.PushCooldown, onTimerDecreaseComplete:
                    () => { 
                        canShoot = true;
                        _isPushShooting = false;
                    },
                    onTimerDecreaseUpdate: OnPushShootCooldownDecrease
                    );
                TimerSystem.Instance.ModifyTimer(_pushShootCooldown, speedMultiplier: characterStatsController.CooldownSpeed);

            }
        }

        public void MineShoot()
        {
            if (canShoot)
            {
                canShoot = false;
                _shootingMine = true;
                float range = characterStatsController.MineShootRange;
                float angle = characterStatsController.MineShootAngle;
                CalculateInitialVelocityForParabolicMovement(range,angle,out float Vx, out float Vy);
                Vector3 velocityVectorX = -transform.forward * Vx;
                Vector3 velocity = velocityVectorX + transform.up * Vy;
                ShootBullet(velocity, range);
                _pushShootCooldown = TimerSystem.Instance.CreateTimer(characterStatsController.PushCooldown, onTimerDecreaseComplete:
                    () => { 
                        canShoot = true; 
                        _shootingMine = false; 
                    },
                    onTimerDecreaseUpdate: OnPushShootCooldownDecrease
                    );

                ModifySpeedOfExistingTimer(characterStatsController.CooldownSpeed);
            }
        }

        public Vector3 GetCurrentParabolicMovementOfPushShoot(out float gravityMultiplier)
        {
            gravityMultiplier = characterStatsController.PushShootGravityMultiplier;
            float range = characterStatsController.PushShootRange;
            float angle = characterStatsController.PushShootAngle;
            CalculateInitialVelocityForParabolicMovement(range, angle, out float Vx, out float Vy);

            //Rotar el vectorX forward hacia el angulo del currentdirection 
            Vector3 currentShootDirectionWithoutY = new Vector3(currentShootDirection.x, 0, currentShootDirection.z);
            float directionAngle = Vector3.SignedAngle(transform.forward, currentShootDirectionWithoutY, Vector3.up);
            Quaternion rotation = Quaternion.AngleAxis(directionAngle, Vector3.up);
            Vector3 velocityVectorX = transform.forward * Vx;

            //Finalmente, se calcula el vector rotado en X y se suma el vector Y.
            return (rotation * velocityVectorX) + transform.up * Vy + physicsBehaviour.Rb.velocity;
        }

        private void CalculateInitialVelocityForParabolicMovement(float range, float angle,out float Vx,out float Vy)
        {
            //Calcular la velocidad necesaria para llegar al rango especifico con angulo especifico. 
            float projectile_Velocity = Mathf.Sqrt((range * Mathf.Abs(Physics.gravity.y * characterStatsController.PushShootGravityMultiplier)) / Mathf.Sin(2 * angle * Mathf.Deg2Rad));

            //Calcular la velocidad en ambos ejes. 

             Vx = projectile_Velocity * Mathf.Cos(angle * Mathf.Deg2Rad);
             Vy = projectile_Velocity * Mathf.Sin(angle * Mathf.Deg2Rad);
        }

        private void OnPushShootCooldownDecrease(float currentvalue)
        {
            onCooldownUpdate?.Invoke(currentvalue, characterStatsController.PushCooldown);
        }

        public void ModifySpeedOfExistingTimer(float newTimerSpeed)
        {
            if (_pushShootCooldown != null&&TimerSystem.Instance.HasTimer(_pushShootCooldown))
            {
                TimerSystem.Instance.ModifyTimer(_pushShootCooldown, speedMultiplier: newTimerSpeed);   
            }
        }

    }
}
