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
        public GameObject Instance;

        [Header("Health")]
        public float MaxResits;
        public float MinResists = 0;
        public bool Dead;


        [Header("Movement")]

        public float MaxSpeed;
        public float MaxSpeedDashing;
        public float MaxSpeedAscend;
        public float MaxSpeedDescend;
        public float MaxSpeedMultiplier;
        public float MinSpeed;
        public float MaxAcceleration;
        public float Acceleration;
        public float MinAcceleration;
        public float BrakeTorque;
        public float Handling;
        //Maybe
        public float DashDistance;


        [Header("Physics")]
        public float Weight;
        public float Traction;
        public float Damping;

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
