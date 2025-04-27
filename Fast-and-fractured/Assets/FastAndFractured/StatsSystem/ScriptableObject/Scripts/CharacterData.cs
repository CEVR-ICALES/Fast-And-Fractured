using FastAndFractured;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Utilities;

namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "CharacterData.asset", menuName = "CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identificators")]
        public string CharacterName;
 
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
        public float NormalShootAngle = 180f;
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

        [Header("AI")]
        [SerializeField] private AIParameters aiParameters;
        public AIParameters AIParameters => aiParameters;

        [Header("FinalAnimations")]
        public List<GameObjectStringPair> WinObjects;
        public List<GameObjectStringPair> LoseObjects;
    }

}
[Serializable]
public class AIParameters
{
    [Tooltip("Distance to retreat from the current target when executing in flee state.")]
    [SerializeField] private float fleeDistance = 5f;
    [Tooltip("Radius of the sweep that the AI uses to search for possible enemies")]
    [SerializeField] private float sweepRadius = 20f;
    [Tooltip("The shooting error that AI has on normal shoot")]
    [SerializeField] private float shootingMarginErrorAngle = 2f;
    
    [Header("Aggressiveness parameters")]
    [Tooltip("Duration of continuous damage required to reach this value")]
    [SerializeField] private float damageAccumulationDuration = 5f;
    [Range(0, 100)][SerializeField] private float fleeTriggerDamageThresholdPercentage = 40;
    [Tooltip("The main way to get out of fleestate. It should be lower than the variable below")]
    [Range(0, 100)][SerializeField] private float _recoveryThresholdPercentageForSearch = 50;
    [Tooltip("How much more health more the AI needs to have over the enemy to start attacking him from flee state")]
    [Range(0, 100)][SerializeField] private float _combatHealthAdvantageThreshold = 60f;
    [Tooltip("Percentage threshold used to determine if a car has dealt enough damage relative to the endurance value.")]
    [Range(10, 100)][SerializeField] private int damageThresholdPercentage = 60;


    [Header("Item Type Priority")]
    [Header("Range of weight on how likely it's going to choose that item type.\n" +
        "--> 10 is base and the minimum.\n" +
        "--> 25 to 30 if multiple priorities.\n" +
        "--> 50 if one normal priority is needed.\n" +
        "--> 150 for hyperfixation in that stat.")]
    [Range(10, 150)][SerializeField] private int decisionPercentageHealth = 10;
    [Range(10, 150)][SerializeField] private int decisionPercentageMaxSpeed = 10;
    [Range(10, 150)][SerializeField] private int decisionPercentageAcceleration = 10;
    [Range(10, 150)][SerializeField] private int decisionPercentageNormalShoot = 50;
    [Range(10, 150)][SerializeField] private int decisionPercentagePushShoot = 10;
    [Range(10, 150)][SerializeField] private int decisionPercentageCooldown = 10;
    public float FleeDistance { get => fleeDistance; set => fleeDistance = value; }
    public float SweepRadius { get => sweepRadius; set => sweepRadius = value; }
    public float ShootingMarginErrorAngle { get => shootingMarginErrorAngle; set => shootingMarginErrorAngle = value; }
    public float DamageAccumulationDuration { get => damageAccumulationDuration; set => damageAccumulationDuration = value; }
    public float FleeTriggerDamageThresholdPercentage { get => fleeTriggerDamageThresholdPercentage; set => fleeTriggerDamageThresholdPercentage = value; }
    public float RecoveryThresholdPercentageForSearch { get => _recoveryThresholdPercentageForSearch; set => _recoveryThresholdPercentageForSearch = value; }
    public float CombatHealthAdvantageThreshold { get => _combatHealthAdvantageThreshold; set => _combatHealthAdvantageThreshold = value; }
    public int DamageThresholdPercentage { get => damageThresholdPercentage; set => damageThresholdPercentage = value; }
    public int DecisionPercentageHealth { get => decisionPercentageHealth; set => decisionPercentageHealth = value; }
    public int DecisionPercentageMaxSpeed { get => decisionPercentageMaxSpeed; set => decisionPercentageMaxSpeed = value; }
    public int DecisionPercentageAcceleration { get => decisionPercentageAcceleration; set => decisionPercentageAcceleration = value; }
    public int DecisionPercentageNormalShoot { get => decisionPercentageNormalShoot; set => decisionPercentageNormalShoot = value; }
    public int DecisionPercentagePushShoot { get => decisionPercentagePushShoot; set => decisionPercentagePushShoot = value; }
    public int DecisionPercentageCooldown { get => decisionPercentageCooldown; set => decisionPercentageCooldown = value; }
}
[Serializable]
public class GameObjectStringPair
{
    public GameObject GameObject;
    public string StringValue;
}