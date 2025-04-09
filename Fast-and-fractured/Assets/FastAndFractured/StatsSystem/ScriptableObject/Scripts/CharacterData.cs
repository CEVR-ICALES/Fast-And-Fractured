using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "CharacterData.asset", menuName = "CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identificators")]
        public string CharacterName;
        public bool AI;

        [Header("Prefabs")]
        public GameObject CarDefaultPrefab;
        public List<GameObject> CarWithSkinsPrefabs;
       
        [Header("Health")]
        public float MaxEndurance = 500;
        public float MinEndurance = 0;
        public bool Invulnerable = false;


        [Header("Movement")]

        public float MaxSpeed = 100;
        public float MaxSpeedDashing = 140;
        [Tooltip("Top speed permitted which vehicle can achieve")] public float MaxSpeedMultiplier = 1;
        public float MinSpeed = 0;
        public float MaxAcceleration = 1500;
        public float MinAcceleration = 1000;
        public float Acceleration = 3500;
        [Tooltip("Slowing force")] public float BrakeTorque = 12000;
        [Tooltip("Max angle that the wheels can rotate")] public float Handling = 35;
        [Tooltip("To avoid sudden changes at the handling")] public float HandlingSmoothnes = 6;
        //delete ?
        public float AerialRotationSpeed;
        [Tooltip("Threshold to determine how much you have to move the joystick to start drifting")] public float DriftThreshold = 0.1f;
        [Tooltip("Top speed at which the drift will Clamp to determine how effective the drift has to be, higher value means that a higher speed will be nedded for the drift to be really efective")]
        public float DriftingFactorToSpeed = 80f;
        public float DriftingSmoothFactor = 0.1f;
        public float DriftForce = 30f;
        public float DashTime = 2f;

        [Header("Wheels")]
        public float FrontWheelsStrenghtFactor = 0.8f;
        public float RearWheelsStrenghtFactor = 0.2f;

        [Header("Physics")]
        public float Weight = 1200;
        public float BaseForce = 300000f;
        public float FrontalHitAnlgeThreshold = 35f;
        public float EnduranceImportanceWhenColliding = 0.5f;

        [Header("RollPreventions")]

        public float BaseDownwardForce = 500f;
        public float TurningForceMultiplier = 2.5f;
        public float SpeedForceMultiplier = 0.6f;

        [Header("DamageAndPushing")]

        public float NormalShootDMG = 10f;
        public float MaxNormalShootDMG = 25f;
        public float MinNormalShootDMG = 5f;
        public float PushShootFORCE = 400000;
        public float ExplosionRadius = 7f;
        public Vector3 ExplosionCenterOffset = Vector3.zero;

        [Header("ShootMovement")]
        public float NormalShootSpeed = 100f;
        [Tooltip("Wait time to shoot next bullet")] public float NormalShootCadenceTime = 0.3f;
        public float NormalShootMaxRange = 150f;
        [Tooltip("The angle include from 10� to 89�. 90� will return infinity.")]
        public float PushShootAngle = 40f;
        [Tooltip("The range of the variable calculates the distance between the first point and an hipotetic second point at the same Y position. Lower the angel bigger will be the distance.")]
        public float PushShootRange = 15f;
        [Tooltip("Bigger the gravity multiplier faster the projectile.")]
        public float PushShootGravityMultiplier = 30f;
        public int PushShootBounceNum = 3;
        public float PushShootBounceForce = 60f;
        public float MineShootAngle = 40;
        public float MineShootRange = 5;



        [Header("Cooldowns")]

        public float DashCooldown = 2f;
        public float PushShootCooldown = 3f;
        public float MineExplosionTime = 5f;
        public float UniqueAbilityCooldown = 7f;
        public float NormalShootOverHeat = 5;
        public float CooldownSpeed = 1f;
        public float DeadDelay;
    }
}
