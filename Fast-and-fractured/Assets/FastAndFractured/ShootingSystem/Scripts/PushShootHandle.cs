// RUTA: Assets/FastAndFractured/ShootingSystem/Scripts/PushShootHandle.cs

using System;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class PushShootHandle : ShootingHandle, ITimeSpeedModifiable
    {
        public UnityEvent<float, float> onCooldownUpdate;

        public bool ShootingMine => _shootingMine;
        private bool _shootingMine = false;

        public bool IsPushShooting => _isPushShooting;
        private bool _isPushShooting = false;

        public Transform PushShootPoint => shootPoint;

        private ITimer _pushShootCooldown;

        public void PushShooting()
        {
            if (!canShoot) return;

            canShoot = false;
            _isPushShooting = true;
            _shootingMine = false;

            float range = characterStatsController.PushShootRange;
            float angle = characterStatsController.PushShootAngle;

            CalculateInitialVelocityForParabolicMovement(range, angle, out float vx, out float vy);

            Vector3 currentShootDirectionWithoutY = new Vector3(currentShootDirection.x, 0, currentShootDirection.z).normalized;
            float directionAngle = Vector3.SignedAngle(transform.forward, currentShootDirectionWithoutY, Vector3.up);
            Quaternion rotation = Quaternion.AngleAxis(directionAngle, Vector3.up);
            Vector3 velocityVectorX = transform.forward * vx;

            Vector3 rotatedVector = (rotation * velocityVectorX) + (transform.up * vy);

            ShootBullet(rotatedVector, range);

            _pushShootCooldown = TimerSystem.Instance.CreateTimer(
                characterStatsController.PushCooldown,
                onTimerDecreaseComplete: () => {
                    canShoot = true;
                    _isPushShooting = false;
                },
                onTimerDecreaseUpdate: OnPushShootCooldownDecrease
            );
            ModifySpeedOfExistingTimer(characterStatsController.CooldownSpeed);
        }

        public void MineShoot()
        {
            if (!canShoot) return;

            canShoot = false;
            _shootingMine = true;
            _isPushShooting = false;

            float range = characterStatsController.MineShootRange;
            float angle = characterStatsController.MineShootAngle;

            CalculateInitialVelocityForParabolicMovement(range, angle, out float vx, out float vy);

            Vector3 velocityVectorX = -transform.forward * vx;
            Vector3 velocity = velocityVectorX + transform.up * vy;

            ShootBullet(velocity, range);

            _pushShootCooldown = TimerSystem.Instance.CreateTimer(
                characterStatsController.PushCooldown,
                onTimerDecreaseComplete: () => {
                    canShoot = true;
                    _shootingMine = false;
                },
                onTimerDecreaseUpdate: OnPushShootCooldownDecrease
            );
            ModifySpeedOfExistingTimer(characterStatsController.CooldownSpeed);
        }

        public Vector3 GetCurrentParabolicMovementOfPushShoot(out float gravityMultiplier)
        {
            gravityMultiplier = characterStatsController.PushShootGravityMultiplier;
            float range = characterStatsController.PushShootRange;
            float angle = characterStatsController.PushShootAngle;

            CalculateInitialVelocityForParabolicMovement(range, angle, out float vx, out float vy);

            Vector3 currentShootDirectionWithoutY = new Vector3(currentShootDirection.x, 0, currentShootDirection.z).normalized;
            float directionAngle = Vector3.SignedAngle(transform.forward, currentShootDirectionWithoutY, Vector3.up);
            Quaternion rotation = Quaternion.AngleAxis(directionAngle, Vector3.up);
            Vector3 velocityVectorX = transform.forward * vx;

            return (rotation * velocityVectorX) + transform.up * vy + physicsBehaviour.Rb.linearVelocity;
        }

        private void CalculateInitialVelocityForParabolicMovement(float range, float angle, out float vx, out float vy)
        {
            float gravity = Mathf.Abs(Physics.gravity.y * characterStatsController.PushShootGravityMultiplier);
            float sin2Angle = Mathf.Sin(2 * angle * Mathf.Deg2Rad);

            if (Mathf.Abs(sin2Angle) < 0.0001f)
            {
                vx = 0;
                vy = 0;
                Debug.LogWarning("Cannot calculate parabolic trajectory for the given angle resulting in Sin(2*angle) near zero.");
                return;
            }

            float projectile_Velocity = Mathf.Sqrt((range * gravity) / sin2Angle);
            vx = projectile_Velocity * Mathf.Cos(angle * Mathf.Deg2Rad);
            vy = projectile_Velocity * Mathf.Sin(angle * Mathf.Deg2Rad);
        }

        private void OnPushShootCooldownDecrease(float currentvalue)
        {
            onCooldownUpdate?.Invoke(currentvalue, characterStatsController.PushCooldown);
        }

        public void ModifySpeedOfExistingTimer(float newTimerSpeed)
        {
            if (_pushShootCooldown != null && TimerSystem.Instance.HasTimer(_pushShootCooldown))
            {
                TimerSystem.Instance.ModifyTimer(_pushShootCooldown, speedMultiplier: newTimerSpeed);
            }
        }
    }
}