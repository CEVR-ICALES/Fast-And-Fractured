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
        public Pooltype PoolType;

        [Header("Game Object")]

        public GameObject Prefab;
        //public GameObject Instance;

        [Header("Health")]
        public float MaxEndurance;
        public float MinEndurance = 0;
        public bool Dead;


        [Header("Movement")]

        public float MaxSpeed;
        public float MaxSpeedDashing;
        public float MaxSpeedMultiplier;
        public float MinSpeed;
        public float MaxAcceleration;
        public float MinAcceleration;
        public float Acceleration;
        public float BrakeTorque;
        public float Handling;
        public float HandlingSmoothnes;
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
        public float NormalShootCadenceTime;
        public float NormalShootRange;
        public float PushShootSpeed;
        [Tooltip("The angle include from 10º to 89º. 90º will return infinity.")]
        public float PushShootAngle;
     [Tooltip("The range of the variable calculates the distance between the first point and an hipotetic second point at the same Y position. Lower the angel bigger will be the distance.")] 
        public float PushShootRange;
        [Tooltip("Bigger the gravity multiplier faster the projectile.")]
        public float PushShootGravityMultiplier;
        public int PushShootBounceNum;
        public float PushShootBounceForce;



        [Header("COOLDOWNS")]
        
        public float DashCooldown;
        public float PushShootCooldown;
        public float UniqueAbilityCooldown;
        public float NormalShootOverHeat;
        public float RecoveryCooldown;
        public float CooldownSpeed;
        public float MinCooldownSpeed;
        public float MaxCooldownSpeed;
    }
}
