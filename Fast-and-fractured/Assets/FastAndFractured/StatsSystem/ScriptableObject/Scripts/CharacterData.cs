using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
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
        public bool Dead;


        [Header("Movement")]

        public float MaxSpeed;
        public float MaxSpeedDashing;
        [Tooltip("Top speed permitted which vehicle can achieve")]  public float MaxSpeedMultiplier;
        public float MinSpeed;
        public float MaxAcceleration;
        public float MinAcceleration;
        public float Acceleration;
        [Tooltip("Slowing force")] public float BrakeTorque;
        [Tooltip("Max angle that the wheels can rotate")] public float Handling;
        [Tooltip("To avoid sudden changes at the handling")] public float HandlingSmoothnes;
        public float AerialRotationSpeed;

        // enum STEERING_MODE
        // enum BRAKE

        //Maybe
        public float DashDistance;


        [Header("Physics")]
        public float Weight;
        public float Traction;
        public float Damping;

        [Header("RollPreventions")]

        public float BaseDownwardForce;
        public float TurningForceMultiplier;
        public float SpeedForceMultiplier;

        [Header("DamageAndPushing")]

        public float NormalShootDMG;
        public float PushShootDMG;
        public float PushShootFORCE;
        public float MaxNormalShootDMG;
        public float MinNormalShootDMG;
        public float MaxPushShootDMG;
        public float MinPushShootDMG;

        [Header("ShootMovement")]
        public float NormalShootSpeed;
        [Tooltip("Wait time to shoot next bullet")] public float NormalShootRateOfFire;
        public float NormalShootMaxRange;
        public float PushShootSpeed;
        public float PushShootAngle;
        public float PushShootRange;


        [Header("Cooldowns")]
        
        public float DashCooldown;
        public float PushShootCooldown;
        public float UniqueAbilityCooldown;
        public float NormalShootOverHeat;
        [Tooltip("When in flipped state, how much time is needed to return to normal state")] public float RecoveryCooldown;
        [Tooltip("Time to go from top speed to max speed")] public float FromTopSpeedToMaxSpeed;
    }
}
