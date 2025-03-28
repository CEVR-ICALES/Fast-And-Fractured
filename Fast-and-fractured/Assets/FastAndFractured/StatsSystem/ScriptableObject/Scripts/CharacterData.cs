using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "CharacterData.asset", menuName = "CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identificators")]
        public int Id;
        public string Name;


        [Header("Health")]
        public float MaxEndurance;
        public float MinEndurance = 0;
        public bool Invulnerable;


        [Header("Movement")]

        public float MaxSpeed;
        public float MaxSpeedDashing;
        [Tooltip("Top speed permitted which vehicle can achieve")] public float MaxSpeedMultiplier;
        public float MinSpeed;
        public float MaxAcceleration;
        public float MinAcceleration;
        public float Acceleration;
        [Tooltip("Slowing force")] public float BrakeTorque;
        [Tooltip("Max angle that the wheels can rotate")] public float Handling;
        [Tooltip("To avoid sudden changes at the handling")] public float HandlingSmoothnes;
        public float AerialRotationSpeed;
        [Tooltip("Threshold to determine how much you have to move the joystick to start drifting")] public float DriftThreshold;
        [Tooltip("Top speed at which the drift will Clamp to determine how effective the drift has to be, higher value means that a higher speed will be nedded for the drift to be really efective")]
        public float DriftingFactorToSpeed;
        public float DriftingSmoothFactor;
        public float DriftForce;
        public float DashTime;

        [Header("Wheels")]
        public float FrontWheelsStrenghtFactor;
        public float RearWheelsStrenghtFactor;

        [Header("Physics")]
        public float Weight;
        public float Traction;
        public float Damping;
        public float BaseForce;
        public float FrontalHitAnlgeThreshold;
        public float EnduranceImportanceWhenColliding;

        [Header("RollPreventions")]

        public float BaseDownwardForce;
        public float TurningForceMultiplier;
        public float SpeedForceMultiplier;

        [Header("DamageAndPushing")]

        public float NormalShootDMG;
        public float MaxNormalShootDMG;
        public float MinNormalShootDMG;
        public float PushShootDMG;
        public float PushShootFORCE;
        public float MaxPushShootDMG;
        public float MinPushShootDMG;
        public float ExplosionRadius;
        public Vector3 ExplosionCenterOffset;

        [Header("ShootMovement")]
        public float NormalShootSpeed;
        [Tooltip("Wait time to shoot next bullet")] public float NormalShootCadenceTime;
        public float NormalShootMaxRange;
        [Tooltip("The angle include from 10� to 89�. 90� will return infinity.")]
        public float PushShootAngle;
        [Tooltip("The range of the variable calculates the distance between the first point and an hipotetic second point at the same Y position. Lower the angel bigger will be the distance.")]
        public float PushShootRange;
        [Tooltip("Bigger the gravity multiplier faster the projectile.")]
        public float PushShootGravityMultiplier;
        public int PushShootBounceNum;
        public float PushShootBounceForce;



        [Header("Cooldowns")]

        public float DashCooldown;
        public float PushShootCooldown;
        public float UniqueAbilityCooldown;
        public float NormalShootOverHeat;
        [Tooltip("When in flipped state, how much time is needed to return to normal state")] public float RecoveryCooldown;
        [Tooltip("Time to go from top speed to max speed")] public float FromTopSpeedToMaxSpeed;
        public float DeadDelay;
    }
}
