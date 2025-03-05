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
        public float PushShootDMG;
        public float PushShootFORCE;
        public float MaxNormalShootDMG;
        public float MinNormalShootDMG;
        public float MaxPushShootDMG;
        public float MinPushShootDMG;


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
