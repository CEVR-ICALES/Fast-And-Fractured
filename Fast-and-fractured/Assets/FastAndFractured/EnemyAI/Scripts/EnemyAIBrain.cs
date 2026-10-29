using Enums;
using FastAndFractured.Utilities;
using NRandom;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Utilities;

namespace FastAndFractured
{
    public class EnemyAIBrain : MonoBehaviour
    {
        //Setted in inspector
        [SerializeField] NavMeshAgent agent;
        NavMeshPath _currentPath;
        Vector3[] _previousPath;
        [Tooltip("Flee state: how far (m) the AI drives away from its current target.")]
        [SerializeField] float fleeDistance = 5f;
        [Tooltip("Radius (m) in which the AI notices other cars and switches from searching items to fighting.")]
        [SerializeField] float sweepRadius = 20f;
        [Tooltip("Aim error added to every shot (normal and push). Each AI scales it by its personality and by the target distance, but never past this value. 0 = perfect aim; 0.25 = up to ~14 deg off.")]
        [SerializeField] float shootingMarginErrorAngle = 0.02f;
        [Tooltip("Layers the AI looks through when searching for cars.")]
        [SerializeField] LayerMask sweepLayerMask;

        private Vector3 _currentPosition;
        private Vector3 _positionToDrive;
        //Reference is obtained via LevelController
        private GameObject _player;
        //Target to shoot
        private GameObject _targetToShoot;
        //Target to go to item/npc/zones
        private GameObject _targetToGo;
        private GameObject _currentTarget;
        //path index starts at 1 because 0 is their actual position
        private int _pathIndex = START_CORNER_INDEX;

        public Vector3 PositionToDrive { get => _positionToDrive; set => _positionToDrive = value; }
        public GameObject Player { get => _player; set => _player = value; }
        public GameObject TargetToShoot { get => _targetToShoot; set => _targetToShoot = value; }
        public GameObject TargetToGo { get => _targetToGo; set => _targetToGo = value; }

        [SerializeField] NormalShootHandle normalShootHandle;
        [SerializeField] PushShootHandle pushShootHandle;
        [SerializeField] CarMovementController carMovementController;
        [SerializeField] PhysicsBehaviour physicsBehaviour;
        [SerializeField] StatsController statsController;
        [SerializeField] BaseUniqueAbility uniqueAbility;
        [SerializeField] ApplyForceByState applyForceByState;
        [SerializeField] TurretRotationMovement turretRotationMovement;
        [Tooltip("Ground layers the AI raycasts against to keep its flee point on the ground.")]
        [SerializeField] LayerMask ignoreLayerMask;

        PathMode pathMode = PathMode.ADVANCED;

        const float ANGLE_90 = 90f;
        const float MAX_INPUT_VALUE = 1f;
        const int MAX_ITEM_TYPE_ATTEMPTS = 20;

        const float MIN_LEAD_BULLET_SPEED = 1f;
        const float SEED_POSITION_SCALE = 10f;
        const int SEED_PRIME_X = 73856093;
        const int SEED_PRIME_Z = 19349663;

        // Personality, seeded from the spawn point so every peer rolls the same.
        private float _aimErrorScale = 1f;
        private float _reactionTime = 0.3f;
        private float _cornerCaution = 1f;
        private float _targetAcquiredAt;
        const int START_CORNER_INDEX = 1;
        private const int PERCENTAGE_VALUE = 100;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float speedMultiplier=1;

        [Header("Aggressiveness parameters (each character's AIParameters override these)")]
        [Tooltip("Seconds without being hit before the damage counted for fleeing resets to 0.")]
        [SerializeField] private float damageAccumulationDuration = 5f;
        [Tooltip("Damage points taken within the window above that make the AI flee. Raw damage, not a percentage: with 1200 endurance, 40 means fleeing after losing ~3%.")]
        [Range(0, 100)][SerializeField] private float fleeTriggerDamageThresholdPercentage = 40;
        private ITimer damageAccumulationTimer;
        private float _currentAccumulatedDamageTime;
        [Tooltip("Endurance % the AI needs to leave the flee state and go back to searching. Keep it below 100.")]
        [Range(0, 100)][SerializeField] private float recoveryThresholdPercentageForSearch = 50;
        public float RecoveryThresholdPercentageForSearch => recoveryThresholdPercentageForSearch;
        [Tooltip("Flee state: the AI goes back to fighting when its endurance % minus the target's is below this value.")]
        [Range(0, 100)][SerializeField] private float combatHealthAdvantageThreshold = 60f;
        [Tooltip("Damage dealt by another car, as a % of the AI's current endurance, that makes it switch target to that car.")]
        [Range(10, 100)][SerializeField] private int damageThresholdPercentage = 60;


        [Header("Item Type Priority")]
        [Tooltip("Weight for health pickups.")]
        [Range(10, 150)][SerializeField] private int decisionPercentageHealth = 10;
        [Tooltip("Weight for max speed pickups.")]
        [Range(10, 150)][SerializeField] private int decisionPercentageMaxSpeed = 10;
        [Tooltip("Weight for acceleration pickups.")]
        [Range(10, 150)][SerializeField] private int decisionPercentageAcceleration = 10;
        [Tooltip("Weight for normal shot damage pickups.")]
        [Range(10, 150)][SerializeField] private int decisionPercentageNormalShoot = 50;
        [Tooltip("Weight for push shot pickups.")]
        [Range(10, 150)][SerializeField] private int decisionPercentagePushShoot = 10;
        [Tooltip("Weight for cooldown pickups.")]
        [Range(10, 150)][SerializeField] private int decisionPercentageCooldown = 10;

        [Tooltip("Extra distance (m) to the storm at which the AI already counts as inside it and runs away. Negative = it waits until it is that deep inside.")]
        [Range(-300, 600)][SerializeField] private int marginToFleeFromSandstorm = 0;

        [Header("Driving")]
        [Tooltip("Angle (deg) to the next path point from which the AI starts easing off the throttle (divided by its corner caution).")]
        [SerializeField] private float cornerSlowdownStartAngle = 35f;
        [Tooltip("Throttle (0-1) the AI keeps in the tightest turns (90 deg).")]
        [SerializeField] private float minCornerThrottle = 0.35f;
        [Tooltip("Distance (m) to a path corner at which the AI aims for the next one.")]
        [SerializeField] private float minDistanceUntilNextCorner = 3f;
        [Tooltip("Distance (m) at which the AI counts an item or zone it drives to as reached.")]
        [SerializeField] private float arrivedDistance = 35f;
        [Tooltip("Multiplier of the flipped-car reset timer when the car lies on the ground (lower = it recovers sooner).")]
        [SerializeField] private float flipTimerFactorOnGround = 0.75f;

        [Header("Aiming")]
        [Tooltip("Max seconds ahead the AI leads a moving target (aims where it will be).")]
        [SerializeField] private float maxLeadSeconds = 1.2f;
        [Tooltip("Distance (m) at which the aim error doubles; it never goes past Shooting Margin Error Angle.")]
        [SerializeField] private float aimErrorDistance = 30f;
        [Tooltip("A target within +/- this angle (deg) of the car's heading counts as in front (IsInFront decision).")]
        [SerializeField] private float frontAngle = 20f;

        [Header("Personality (rolled per AI from its spawn point)")]
        [Tooltip("Best aim an AI can roll: multiplier of the aim error (lower = sharper).")]
        [SerializeField] private float minAimErrorScale = 0.6f;
        [Tooltip("Worst aim an AI can roll: multiplier of the aim error.")]
        [SerializeField] private float maxAimErrorScale = 1.8f;
        [Tooltip("Fastest reaction an AI can roll: seconds before it shoots a newly chosen target.")]
        [SerializeField] private float minReactionTime = 0.15f;
        [Tooltip("Slowest reaction an AI can roll.")]
        [SerializeField] private float maxReactionTime = 0.5f;
        [Tooltip("Boldest driver an AI can roll: lower brakes later into corners.")]
        [SerializeField] private float minCornerCaution = 0.7f;
        [Tooltip("Most careful driver an AI can roll.")]
        [SerializeField] private float maxCornerCaution = 1.3f;
        [Tooltip("Random +/- added to the flee and fight-back thresholds, so some AIs are bolder and others flee sooner.")]
        [SerializeField] private float thresholdJitter = 10f;
        [Tooltip("Lowest value the jittered thresholds can take.")]
        [SerializeField] private float minThreshold = 5f;
        [Tooltip("Highest value the jittered thresholds can take.")]
        [SerializeField] private float maxThreshold = 95f;

        [Header("Target choice (lowest score wins: distance in m minus bonuses)")]
        [Tooltip("Seconds the AI sticks to its target before choosing again (stops it switching every frame).")]
        [SerializeField] private float targetCommitSeconds = 3f;
        [Tooltip("Metres of preference for human players over AIs.")]
        [SerializeField] private float scoreHumanBonus = 10f;
        [Tooltip("Metres of preference for a car that damaged this AI recently.")]
        [SerializeField] private float scoreDamagedMeBonus = 15f;
        [Tooltip("Metres of preference for a nearly dead car (scaled by how much endurance it lost).")]
        [SerializeField] private float scoreLowHealthBonus = 20f;

        [Header("Items, storm and end of match")]
        [Tooltip("Fleeing: the AI only goes for the nearest item or safe zone when its target is more than this angle (deg) off its heading.")]
        [SerializeField] private float awayFromTargetAngle = 30f;
        [Tooltip("Added to the Health item weight per endurance % the AI currently has.")]
        [SerializeField] private int healthWeightPerEndurance = 3;
        [Tooltip("How much missing endurance widens the storm margin (0.5 = up to +50% when nearly dead).")]
        [SerializeField] private float sandstormEnduranceFactor = 0.5f;
        [Tooltip("With this many cars or fewer left, the AI plays the end-game behaviour.")]
        [SerializeField] private int endGameCharacters = 3;

        private int _totalDecisionPercentage = 0;
        private int _startingPercentageHealth = 0;
        public Stats StatToChoose => _statToChoose;
        private Stats _statToChoose;

        private IAGroundState groundState = IAGroundState.NONE;
        private ITimer _flipForceTimer;
        private AiInputProvider _aiInputProvider;
        //Training Values
        public bool PlayerNear { get => _playerNear; set => _playerNear = value; }
        private bool _playerNear = false;

        public bool DoTrainingAction { get => _doTrainingAction; set => _doTrainingAction = value;}
        private bool _doTrainingAction;



        private void OnEnable()
        {
            if (statsController)
            {
                statsController.onEnduranceDamageTaken.AddListener(OnTakeEnduranceDamage);
                statsController.onEnduranceDamageHealed.AddListener(OnTakeEnduranceHealed);
            }
            _aiInputProvider = GetComponentInParent<AiInputProvider>();

        }


        private void OnDisable()
        {
            if (statsController)
            { 
                statsController.onEnduranceDamageTaken.RemoveListener(OnTakeEnduranceDamage);
                statsController.onEnduranceDamageHealed.RemoveListener(OnTakeEnduranceHealed);
            }
        }
        private void Awake()
        {
            LevelController.Instance.charactersCustomStart.AddListener(InitializeAIValues);
        }

        private void Update()
        {
            if (carMovementController != null)
            {
                _currentPosition = carMovementController.transform.position;
                if (!carMovementController.IsGrounded())
                {
                    AirForces();
                }
                else
                {
                    GroundForces();
                    if (!carMovementController.IsFlipped)
                    {
                        if (carMovementController.IsInFlipCase() || physicsBehaviour.IsTouchingGround)
                        {
                            carMovementController.StartIsFlippedTimer(physicsBehaviour.IsTouchingGround ? flipTimerFactorOnGround : 1);
                        }
                        else
                        {
                            carMovementController.StopFlippedTimer();
                        }
                    }
                    if (carMovementController.IsFlipped)
                    {
                        if (_flipForceTimer == null)
                        {
                            FlipStateForce();
                            _flipForceTimer = TimerSystem.Instance.CreateTimer(0.5f, onTimerDecreaseComplete: () =>
                            {
                                _flipForceTimer = null;
                            });
                        }
                        groundState = IAGroundState.FLIP_SATE;
                        if (!carMovementController.IsInFlipCase())
                        {
                            carMovementController.IsFlipped = false;
                            groundState = IAGroundState.GROUND;
                        }
                    }
                }
            }
        }

        private void GroundForces()
        {
            if ((groundState == IAGroundState.AIR || groundState == IAGroundState.NONE) && groundState != IAGroundState.FLIP_SATE)
            {
                applyForceByState.ToggleAirFriction(false);
                applyForceByState.ToggleCustomGravity(false);
                applyForceByState.ToggleRollPrevention(true, 1);//By default, IA is always moving
                groundState = IAGroundState.GROUND;
            }
        }

        private void AirForces()
        {
            if (groundState == IAGroundState.GROUND || groundState == IAGroundState.NONE)
            {
                applyForceByState.ToggleAirFriction(true);
                applyForceByState.ToggleCustomGravity(true);
                applyForceByState.ToggleRollPrevention(false, 0);
                groundState = IAGroundState.AIR;
            }
        }

        private void FlipStateForce()
        {
            applyForceByState.ApplyFlipStateForce(physicsBehaviour.TouchingGroundNormal, physicsBehaviour.TouchingGroundPoint);
            applyForceByState.ToggleRollPrevention(false, 1);
        }

        private void InitializeAIValues()
        {
            if (!agent)
            {
                agent = GetComponentInChildren<NavMeshAgent>();
            }

            if (!normalShootHandle)
            {
                normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            }
            if (!pushShootHandle)
            {
                pushShootHandle = GetComponentInChildren<PushShootHandle>();
            }
            if (!carMovementController)
            {
                carMovementController = GetComponentInChildren<CarMovementController>();
                gameObject.name = gameObject.name.Replace("(Clone)", "") + " - " + carMovementController.gameObject.name;
            }
            if (!physicsBehaviour)
            {
                physicsBehaviour = GetComponentInChildren<PhysicsBehaviour>();
            }
            if (!statsController)
            {
                statsController = GetComponentInChildren<StatsController>();
            }

            if (!uniqueAbility)
            {
                uniqueAbility = GetComponentInChildren<BaseUniqueAbility>();
            }

            if (!applyForceByState)
            {
                applyForceByState = GetComponentInChildren<ApplyForceByState>();
            }

            if (!turretRotationMovement)
            {
                turretRotationMovement = GetComponentInChildren<TurretRotationMovement>();
            }

            _currentPath = new NavMeshPath();
            _previousPath = new Vector3[0];
            startPosition = carMovementController.transform.position;
            startRotation = carMovementController.transform.rotation;
            _startingPercentageHealth = decisionPercentageHealth;
            
            // Subscribes here if statsController was still null in OnEnable; RemoveListener avoids doubles.
            if (statsController)
            {
                statsController.onEnduranceDamageTaken.RemoveListener(OnTakeEnduranceDamage);
                statsController.onEnduranceDamageTaken.AddListener(OnTakeEnduranceDamage);
                
                statsController.onEnduranceDamageHealed.RemoveListener(OnTakeEnduranceHealed);
                statsController.onEnduranceDamageHealed.AddListener(OnTakeEnduranceHealed);
            }
            
            _currentPosition = carMovementController.transform.position;
            _player = LevelController.Instance.LocalPlayer;
            RollPersonality();
        }

        private void RollPersonality()
        {
            int seed = unchecked(Mathf.RoundToInt(startPosition.x * SEED_POSITION_SCALE) * SEED_PRIME_X ^ Mathf.RoundToInt(startPosition.z * SEED_POSITION_SCALE) * SEED_PRIME_Z);
            var rng = new System.Random(seed);
            float Roll(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

            _aimErrorScale = Roll(minAimErrorScale, maxAimErrorScale);
            _reactionTime = Roll(minReactionTime, maxReactionTime);
            _cornerCaution = Roll(minCornerCaution, maxCornerCaution);
            // Some are bolder, some flee sooner.
            fleeTriggerDamageThresholdPercentage = Mathf.Clamp(fleeTriggerDamageThresholdPercentage + Roll(-thresholdJitter, thresholdJitter), minThreshold, maxThreshold);
            combatHealthAdvantageThreshold = Mathf.Clamp(combatHealthAdvantageThreshold + Roll(-thresholdJitter, thresholdJitter), minThreshold, maxThreshold);
        }
        public void ReturnToStartPosition()
        {
            carMovementController.transform.position = startPosition;
            StopMovement();
        }
        public float GetHealth()
        {
            return statsController.Endurance;
        }
        #region Actions

        #region Common 
        public void GoToPosition()
        {
            float angle = GetAngleDirection(Vector3.up);

            //Left/Right
            //If it's negative, go left
            //If it's positive, go right
            float inputX = -Mathf.Min(angle / ANGLE_90, MAX_INPUT_VALUE);

            //Forward/Backward
            //If angle between 90 and -90 go forward
            //If angle more than 90 or less than -90 go backwards
            float inputY = MAX_INPUT_VALUE;
            if (angle > ANGLE_90 || angle < -ANGLE_90)
            {
                inputY = -MAX_INPUT_VALUE;
            }
            else
            {
                float slowFrom = cornerSlowdownStartAngle / _cornerCaution;
                inputY = Mathf.Lerp(MAX_INPUT_VALUE, minCornerThrottle, Mathf.InverseLerp(slowFrom, ANGLE_90, Mathf.Abs(angle)));
            }

            Vector2 input = new Vector2(inputX, inputY);
            _aiInputProvider.DesiredMoveInput = input * speedMultiplier;
            carMovementController.ProcessMovementInput();
        }


        public void UpdateTargetPosition()
        {
            if (_currentTarget == null)
            {
                return;
            }
            _positionToDrive = _currentTarget.transform.position;
        }

        public void RegisterSuddenly(float damageTaken)
        {
            _currentAccumulatedDamageTime += damageTaken;
            if (damageAccumulationTimer == null)
            {
                damageAccumulationTimer = TimerSystem.Instance.CreateTimer(damageAccumulationDuration, TimerDirection.INCREASE, onTimerIncreaseComplete: () =>
                {
                    damageAccumulationTimer = null;
                    _currentAccumulatedDamageTime = 0;
                });
            }
            else
            {
                TimerSystem.Instance.ModifyTimer(damageAccumulationTimer, newCurrentTime: 0);
            }
        }

        public bool HasReachedTargetToGoPosition()
        {
            if (_currentTarget == null)
            {
                return true;
            }
            float distance = Vector3.Distance(_currentPosition, _currentTarget.transform.position);
            return distance < arrivedDistance;
        }
        #endregion

        #region SearchState
        public void ChoosePlayer()
        {
            // Online, the best human target rather than the host's car (LevelController.LocalPlayer).
            GameObject target = ChooseBestTarget(humansOnly: true);
            if (target == null) target = _player;
            if (target != null)
            {
                ChangeTargetToShoot(target);
            }
        }

        public void ChooseItemFromType()
        {
            // With no stat item left on the map the loop below would never end.
            var allItems = InteractableHandler.Instance.GetStatBoostItems();
            if (allItems == null || allItems.Count == 0) return;
            int attempts = 0;
            do
            {
                if (++attempts > MAX_ITEM_TYPE_ATTEMPTS)
                {
                    ChooseNearestItem();
                    return;
                }
                CalculateTotalDecisionPercentage();
                //+1 because it's max is exclusive
                int decision = DeterministicRandom.Instance.NextInt(0, _totalDecisionPercentage + 1);
                int percentageMaxSpeed = decisionPercentageHealth + decisionPercentageMaxSpeed;
                int percentageAcceleration = percentageMaxSpeed + decisionPercentageAcceleration;
                int percentageNormalShoot = percentageAcceleration + decisionPercentageNormalShoot;
                int percentagePushShoot = percentageNormalShoot + decisionPercentagePushShoot;
                int percentageCooldown = percentagePushShoot + decisionPercentageCooldown;

                switch (decision)
                {
                    case int n when (n <= decisionPercentageHealth):
                        _statToChoose = Stats.ENDURANCE;
                        break;
                    case int n when (n <= percentageMaxSpeed):
                        _statToChoose = Stats.MAX_SPEED;
                        break;
                    case int n when (n <= percentageAcceleration):
                        _statToChoose = Stats.ACCELERATION;
                        break;
                    case int n when (n <= percentageNormalShoot):
                        _statToChoose = Stats.NORMAL_DAMAGE;
                        break;
                    case int n when (n <= percentagePushShoot):
                        _statToChoose = Stats.PUSH_FORCE;
                        break;
                    case int n when (n <= percentageCooldown):
                        _statToChoose = Stats.COOLDOWN_SPEED;
                        break;
                    default:
                        ChooseNearestItem();
                        break;
                }
            } while (!InteractableHandler.Instance.CheckIfStatItemExists(_statToChoose));

            GetClosestItemByList(InteractableHandler.Instance.GetOnlyStatBoostItemsStat(_statToChoose));
        }

        public void ChooseNearestItem()
        {
            GetClosestItemByList(InteractableHandler.Instance.GetStatBoostItems());
        }

        public void ChooseNearestItemAwayFromTarget()
        {
            float angle = GetAngleDirection(Vector3.up);
            float nearestOne = float.MaxValue;
            List<StatsBoostInteractable> items = InteractableHandler.Instance.GetStatBoostItems();
            items = ListWithGameElementNotInsideSandstorm(items);
            GameObject nearestTarget = items[0].gameObject;
            foreach (StatsBoostInteractable statItem in items)
            {
                float itemDistance = (statItem.transform.position - _currentPosition).sqrMagnitude;
                if (itemDistance < nearestOne && (angle < -awayFromTargetAngle || angle > awayFromTargetAngle))
                {
                    nearestOne = itemDistance;
                    nearestTarget = statItem.gameObject;
                }
            }

            ChangeTargetToGo(nearestTarget);
        }



        public void ChooseNearestCharacter()
        {
            GameObject target = ChooseBestTarget(humansOnly: false);
            if (target == null) target = CalcNearestCharacter();
            ChangeTargetToShoot(target);
        }

        /// <summary>
        /// Lowest score wins: distance minus bonuses for humans, for whoever hurt this AI recently and for low health.
        /// The current target is kept for targetCommitSeconds while alive.
        /// </summary>
        private GameObject ChooseBestTarget(bool humansOnly)
        {
            // LevelController picks the first target before this AI has initialized.
            if (carMovementController == null || !LevelController.HasInstance) return null;
            if (_targetToShoot != null && IsAliveCharacter(_targetToShoot) &&
                Time.time - _targetAcquiredAt < targetCommitSeconds &&
                (!humansOnly || !IsAICharacter(_targetToShoot)))
                return _targetToShoot;

            List<GameObject> characters = ListWithGameElementNotInsideSandstorm(LevelController.Instance.InGameCharacters);
            GameObject best = null;
            float bestScore = float.MaxValue;
            foreach (GameObject character in characters)
            {
                if (!character || character == carMovementController.gameObject || !IsAliveCharacter(character)) continue;
                bool ai = IsAICharacter(character);
                if (humansOnly && ai) continue;

                float score = Vector3.Distance(character.transform.position, _currentPosition);
                if (!ai) score -= scoreHumanBonus;
                if (_carsThatDamagedAI.ContainsKey(character)) score -= scoreDamagedMeBonus;
                var stats = character.GetComponentInChildren<StatsController>();
                if (stats != null) score -= (1f - stats.GetEndurancePercentage() / PERCENTAGE_VALUE) * scoreLowHealthBonus;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = character;
                }
            }
            return best;
        }

        private static bool IsAICharacter(GameObject character) =>
            character.GetComponentInParent<EnemyAIBrain>() != null || character.GetComponentInChildren<EnemyAIBrain>() != null;

        private static bool IsAliveCharacter(GameObject character)
        {
            if (!character || !character.activeInHierarchy) return false;
            var stats = character.GetComponentInChildren<StatsController>();
            return stats == null || stats.Endurance > 0f;
        }


        public GameObject CalcNearestCharacter()
        {
            List<GameObject> inGameCharacters = LevelController.Instance.InGameCharacters;
            inGameCharacters = ListWithGameElementNotInsideSandstorm(inGameCharacters);
            if (inGameCharacters.Count > 1)
            {
                GameObject nearestTarget = inGameCharacters[0].gameObject != carMovementController.gameObject ? inGameCharacters[0] : inGameCharacters[1];
                float nearestOne = float.MaxValue;

                foreach (GameObject character in inGameCharacters)
                {
                    if (!character) continue;
                    float characterDistance = (character.transform.position - _currentPosition).sqrMagnitude;
                    if (characterDistance < nearestOne && character.gameObject != carMovementController.gameObject)
                    {
                        nearestOne = characterDistance;
                        nearestTarget = character;
                    }
                }
                return nearestTarget;
            }
            return null;
        }

        public void ChooseCharacterThatIsNotPlayer()
        {
            GameObject characterThatIsNotPlayer = ReturnCharacterThatIsNotPlayer();
            ChangeTargetToShoot(characterThatIsNotPlayer);
        }

        private GameObject ReturnCharacterThatIsNotPlayer()
        {
            List<GameObject> inGameCharacters = LevelController.Instance.InGameCharacters;
            foreach (GameObject character in inGameCharacters)
            {
                if(character!=_player)
                    return character;
            }
            return _player;
        }
        public void ChooseNearestDangerZone()
        {
            //TODO
            //Get list of danger zones and choose the nearest one
        }

        public void ChooseNearestSafeZone()
        {
            GameObject nearestTarget = CalcNearestSafeZone();
            ChangeTargetToGo(nearestTarget);
        }

        public void ChooseNearestSafeZoneAwayFromTarget()
        {
            float angle = GetAngleDirection(Vector3.up);
            float nearestOne = float.MaxValue;
            List<GameObject> safeZonesOutsideSandstorm = LevelController.Instance.SafeZonesOutsideSandstorm();
            GameObject nearestSafeZone = safeZonesOutsideSandstorm[0];
            foreach (GameObject safeZone in safeZonesOutsideSandstorm)
            {
                float safeZoneDistance = (safeZone.transform.position - _currentPosition).sqrMagnitude;
                if (safeZoneDistance < nearestOne && (angle < -awayFromTargetAngle || angle > awayFromTargetAngle))
                {
                    nearestOne = safeZoneDistance;
                    nearestSafeZone = safeZone.gameObject;
                }
            }
            ChangeTargetToGo(nearestSafeZone);
        }

        public GameObject CalcNearestSafeZone()
        {
            List<GameObject> safeZonesOutsideSandstorm = LevelController.Instance.SafeZonesOutsideSandstorm();
            GameObject nearestSafeZone = safeZonesOutsideSandstorm[0];
            float nearestOne = float.MaxValue;

            foreach (GameObject safeZone in safeZonesOutsideSandstorm)
            {
                if (!safeZone) continue;
                float characterDistance = (safeZone.transform.position - _currentPosition).sqrMagnitude;
                if (characterDistance < nearestOne && safeZone.gameObject != carMovementController.gameObject)
                {
                    nearestOne = characterDistance;
                    nearestSafeZone = safeZone;
                }
            }
            return nearestSafeZone;
        }



        #endregion

        #region CombatState
        public void NormalShoot()
        {
            if (!TargetToShoot) return;
            if (Time.time - _targetAcquiredAt < _reactionTime) return;   // still reacting to the new target

            normalShootHandle.CurrentShootDirection = GetShootingDirectionWithError();
            normalShootHandle.NormalShooting();
        }
        public void StopDelayDecreaseOverheat()
        {
            normalShootHandle.StopDelayDecreaseOverheat();
        }
        public void DecreaseOverheatTime()
        {
            normalShootHandle.DecreaseOverheatTime();
        }
        public void PushShoot()
        {
            if (!_targetToShoot) return;
            if (Time.time - _targetAcquiredAt < _reactionTime) return;
            pushShootHandle.CurrentShootDirection = GetShootingDirectionWithError();
            pushShootHandle.PushShooting();
        }

        public void UseUniqueAbility()
        {
            if (uniqueAbility == null) return;
            uniqueAbility.ActivateAbility();
        }

        public void ChangeShootingTargetToTheOneThatMadeMoreDamage()
        {
            var listOfCarsThatMadeLotsOfDamage = _carsThatDamagedAI
                .Where(x => (x.Value.damageMade / statsController.Endurance * PERCENTAGE_VALUE) > damageThresholdPercentage)
                .ToList();
            if (listOfCarsThatMadeLotsOfDamage.Count > 1)
            {
                ChangeTargetToShoot(listOfCarsThatMadeLotsOfDamage[0].Key);
            }

        }
        #endregion

        #region DashState
        public void Dash()
        {
            carMovementController.HandleDashWithPhysics();
        }

        #endregion


        #region FleeState
        public void RunAwayFromCurrentTarget()
        {
            if (!_currentTarget) return;
            _positionToDrive = -CalcNormalizedTargetDirection() * fleeDistance;
        }

        #endregion
        #endregion

        #region Decisions
        public bool EnemySweep()
        {

            GameObject nearestCharacter = CalcNearestCharacter();
            if (nearestCharacter != null)
            {
                return Vector3.Distance(_currentPosition, nearestCharacter.transform.position) < sweepRadius;
            }
            return false;
        }

        public bool IsPushShootReady()
        {
            return pushShootHandle.CanShoot;
        }

        public bool IsShootOverheated()
        {
            return normalShootHandle.IsOverHeat;
        }
        public bool IsUniqueAbilityReady()
        {
            return uniqueAbility.IsOnCooldown;
        }
        public bool IsUniqueAbilityFinished()
        {
            return !uniqueAbility.IsAbilityActive;
        }

        public bool IsDashReady()
        {
            return carMovementController.CanDash;
        }

        public bool IsDashFinished()
        {
            return !carMovementController.IsDashing;
        }

        public bool IsInValidRange(float distance)
        {
            return distance > Vector3.Distance(_currentPosition, _positionToDrive);
        }

        public bool IsInFront()
        {
            float angle = GetAngleDirection(Vector3.up);
            return (angle > -frontAngle && angle < frontAngle);
        }

        public bool WantsToChangeToCombatState()
        {
            if (!_targetToShoot)
            {
                return false;
            }
            StatsController enemyStatsController = _targetToShoot.GetComponent<StatsController>();
            if (enemyStatsController == null) { return false; }


            float healthDifference = statsController.GetEndurancePercentage() - enemyStatsController.GetEndurancePercentage();
            return healthDifference < combatHealthAdvantageThreshold;
        }
        public bool WantsToChangeToFleeState()
        {
            bool condition = _currentAccumulatedDamageTime >= fleeTriggerDamageThresholdPercentage;
            if (condition)
            {
                return condition;
            }
            //TODO if in the future we need more extra conditions is posible to add them here
            return condition;
        }

        public bool WantsToChangeFromFleeToSearchState()
        {
            return statsController.GetEndurancePercentage() > recoveryThresholdPercentageForSearch;
        }

        public bool HasTargetToShoot()
        {
            return LevelController.Instance.InGameCharacters.Contains(TargetToShoot);
        }

        public bool HasSomeoneThatIsNotTheTargetMadeEnoughDamage()
        {

            var listOfCarsThatMadeLotsOfDamage = _carsThatDamagedAI
             .Where(x => (x.Value.damageMade / statsController.Endurance * PERCENTAGE_VALUE) > damageThresholdPercentage)
             .ToList();
            return listOfCarsThatMadeLotsOfDamage != null && listOfCarsThatMadeLotsOfDamage.Count > 0;
        }
        //State shootToWhoMadeMoreDamageState
        #region TraningState
        public bool CanPerformeTrainingAction()
        {
            return _doTrainingAction;
        }
        public void DesactivateTrainingAction()
        {
            _doTrainingAction = false;
        }
        #endregion
        #endregion


        #region Helpers

        private Dictionary<GameObject, CarDamagedMe> _carsThatDamagedAI = new();

        [Tooltip("Seconds the AI remembers who damaged it (for switching target to them).")]
        [SerializeField] private float forgetDuration = 5f;
        private void OnTakeEnduranceDamage(float damageTaken, GameObject whoIsMakingDamage)
        {
            if (!whoIsMakingDamage.GetComponentInParent<CarMovementController>())
            {
                return;
            }
            if (whoIsMakingDamage != _targetToShoot)
            {
                if (!_carsThatDamagedAI.TryAdd(whoIsMakingDamage, new CarDamagedMe()
                {
                    damageMade = damageTaken,
                    timeThatHasPassed = Time.time,
                    timerUntilRemove = TimerSystem.Instance.CreateTimer(forgetDuration, TimerDirection.INCREASE, onTimerIncreaseComplete:
                            () =>
                            {
                                _carsThatDamagedAI.Remove(whoIsMakingDamage);
                            })
                }))
                {
                    _carsThatDamagedAI[whoIsMakingDamage].timeThatHasPassed = Time.time;
                    TimerSystem.Instance.ModifyTimer(_carsThatDamagedAI[whoIsMakingDamage].timerUntilRemove, newCurrentTime: 0);
                }
                _carsThatDamagedAI[whoIsMakingDamage].damageMade += damageTaken;



            }
            RegisterSuddenly(damageTaken);
            RecalculateDecisionsPercentage();
        }

        private void OnTakeEnduranceHealed(float damageHealed)
        {
            RecalculateDecisionsPercentage();
        }



        private Vector3 CalcNormalizedTargetDirection()
        {
            return (_currentTarget.transform.position - _currentPosition).normalized;
        }

        private Vector3 CalcNormalizedShootingDirection()
        {
            return (_currentTarget.transform.position - normalShootHandle.ShootPoint.position).normalized;
        }

        private float GetAngleDirection(Vector3 axis)
        {
            Vector3 direction;
            if (_currentTarget)
            {
                switch (pathMode)
                {
                    default:
                    case PathMode.SIMPLE:
                        direction = CalcNormalizedTargetDirection();
                        break;
                    case PathMode.ADVANCED:
                        if (TryToCalculatePath())
                        {
                            CheckIfGoToNextPathPoint();
                        }
                        else
                        {
                            //Debug.LogWarning("No path to follow" + _currentPath.ToString());
                            if (Physics.Raycast(_positionToDrive, Vector3.down, out RaycastHit hit, 100f, ignoreLayerMask))
                            {
                                Debug.DrawRay(_positionToDrive, Vector3.down, Color.magenta);

                                _positionToDrive = hit.point;
                                TryToCalculatePath();
                                CheckIfGoToNextPathPoint();
                            }
                        }
                        break;
                }
            }
            direction = (_positionToDrive - _currentPosition);

            //If it's negative, go left
            //If it's positive, go right
            return Vector3.SignedAngle(direction, carMovementController.transform.forward, axis);
        }

        private float _emergencyRepositioningValue = 200f;
        bool TryToCalculatePath()
        {
            if (!carMovementController.IsGrounded())
            {
                return false;
            }

            if (!agent.isOnNavMesh)
            {
                //_positionToDrive = _positionToDrive;
                //Debug.LogWarning("No navmesh so trying to go to position to drive manually", this.gameObject);
                //return true;

                if (NavMesh.SamplePosition(_currentPosition, out NavMeshHit hit, _emergencyRepositioningValue, NavMesh.AllAreas))
                {
                    _positionToDrive = hit.position;
                    Debug.LogWarning("Emergency repositioning", this.gameObject);
                    return true;
                }
            }
            if (agent.CalculatePath(_positionToDrive, _currentPath))
            {
                if (_previousPath.Length != _currentPath.corners.Length)
                {
                    ResetIndexPath();
                }
                if (_currentPath.corners.Length == 1) return false;

                for (int i = 0; i < _currentPath.corners.Length - 1; i++)
                    Debug.DrawLine(_currentPath.corners[i], _currentPath.corners[i + 1], Color.yellow);

                _positionToDrive = GetActivePathPoint();
                CheckIfGoToNextPathPoint();
                return true;
            }
            else
            {
                //Debug.LogError("No path to follow" + _currentPath.ToString());
                return false;
            }
            ;
        }
        private Vector3 GetActivePathPoint()
        {
            switch (_currentPath.corners.Length)
            {
                case 1:
                    Debug.LogError("THE PATH ONLY HAS ONE POINT. This is probably because you put the car too far away from the ground", this.gameObject);

                    return _currentPath.corners[0];
                case > 0:
                    return _currentPath.corners[_pathIndex];
                default:
                    return Vector3.zero;
            }
        }

        private void CheckIfGoToNextPathPoint()
        {
            if (Vector3.Distance(_currentPosition, GetActivePathPoint()) < minDistanceUntilNextCorner &&
                (_pathIndex + 1) < _currentPath.corners.Length)
            {
                _pathIndex++;
            }
        }

        private void ResetIndexPath()
        {
            _pathIndex = START_CORNER_INDEX;
            _previousPath = _currentPath.corners;
        }

        public Vector3 GetShootingDirectionWithError()
        {
            Vector3 shootFrom = normalShootHandle.ShootPoint.position;
            Vector3 aimPoint = _currentTarget.transform.position;
            float distance = Vector3.Distance(shootFrom, aimPoint);
            ICustomRigidbody targetBody = _currentTarget.GetComponentInParent<ICustomRigidbody>();
            if (targetBody == null) targetBody = _currentTarget.GetComponentInChildren<ICustomRigidbody>();
            float bulletSpeed = statsController.NormalShootSpeed;
            if (targetBody != null && bulletSpeed > MIN_LEAD_BULLET_SPEED)
                aimPoint += targetBody.linearVelocity * Mathf.Min(distance / bulletSpeed, maxLeadSeconds);

            Vector3 shootingDirection = (aimPoint - shootFrom).normalized;
            if (turretRotationMovement != null)
            {
                turretRotationMovement.TargetDirection = shootingDirection;
            }
            float error = shootingMarginErrorAngle * Mathf.Min(_aimErrorScale * (1f + distance / aimErrorDistance), 1f);
            return (shootingDirection + new Vector3(UnityEngine.Random.Range(-error, error),
                UnityEngine.Random.Range(0, error), UnityEngine.Random.Range(-error, error))).normalized;
        }

        public void StopMovement()
        {
            physicsBehaviour.Rb.angularVelocity = Vector3.zero;
            physicsBehaviour.Rb.linearVelocity = Vector3.zero;
            carMovementController.StopAllCarMovement();
        }

        private void CalculateTotalDecisionPercentage()
        {
            _totalDecisionPercentage += decisionPercentageHealth +
                decisionPercentageMaxSpeed +
                decisionPercentageAcceleration +
                decisionPercentageNormalShoot +
                decisionPercentagePushShoot +
                decisionPercentageCooldown;
        }

        private void RecalculateDecisionsPercentage()
        {
            decisionPercentageHealth = Mathf.RoundToInt(_startingPercentageHealth + statsController.GetEndurancePercentage() * healthWeightPerEndurance);
        }

        private void GetClosestItemByList(List<StatsBoostInteractable> list)
        {
            // No item of that type on the map right now (all picked up): keep the current target.
            if (list == null || list.Count == 0) return;
            float nearestOne = float.MaxValue;
            List<StatsBoostInteractable> items = list;
            GameObject nearestTarget = items[0].gameObject;
            items = ListWithGameElementNotInsideSandstorm(items);
            foreach (StatsBoostInteractable statItem in items)
            {
                float itemDistance = (statItem.transform.position - _currentPosition).sqrMagnitude;
                if (itemDistance < nearestOne)
                {
                    nearestOne = itemDistance;
                    nearestTarget = statItem.gameObject;
                }
            }

            ChangeTargetToGo(nearestTarget);
        }

        private List<T> ListWithGameElementNotInsideSandstorm<T>(List<T> gameElementListIfInsideSandstorm) where T : MonoBehaviour
        {
            List<T> gameElementsNotInsideSandstorm = new List<T>();
            foreach (T gameElement in gameElementListIfInsideSandstorm)
            {
                if (!LevelController.Instance.IsInsideSandstorm(gameElement.gameObject))
                {
                    gameElementsNotInsideSandstorm.Add(gameElement);
                }
            }
            return gameElementsNotInsideSandstorm.Count > 0 ? gameElementsNotInsideSandstorm : gameElementListIfInsideSandstorm;
        }

        private List<GameObject> ListWithGameElementNotInsideSandstorm(List<GameObject> gameElementListIfInsideSandstorm)
        {
            List<GameObject> gameElementsNotInsideSandstorm = new List<GameObject>();
            foreach (GameObject gameElement in gameElementListIfInsideSandstorm)
            {
                if (!LevelController.Instance.IsInsideSandstorm(gameElement))
                {
                    gameElementsNotInsideSandstorm.Add(gameElement);
                }
            }
            return gameElementsNotInsideSandstorm.Count > 0 ? gameElementsNotInsideSandstorm : gameElementListIfInsideSandstorm;
        }

        public bool IsIAInsideSandstorm()
        {
            float enduranceFactor = (1 - (statsController.Endurance / statsController.MaxEndurance)) * sandstormEnduranceFactor;
            float marginWithEnduranceToFleeTheSandstorm = marginToFleeFromSandstorm + marginToFleeFromSandstorm * enduranceFactor;
            return LevelController.Instance.IsInsideSandstorm(carMovementController.gameObject, marginToFleeFromSandstorm);
        }

        public bool AreAllInteractablesInsideSandstorm()
        {
            return LevelController.Instance.AreAllThisGameElementsInsideSandstorm(GameElement.INTERACTABLE);
        }

        public bool AreAllSafeZonesInsideSandstorm()
        {
            return LevelController.Instance.AreAllThisGameElementsInsideSandstorm(GameElement.SAFE_ZONES);
        }

        public bool IsOnEndGame()
        {
            return endGameCharacters >= LevelController.Instance.InGameCharacters.Count;
        }

        public void InstallAIParameters(AIParameters aIParameters)
        {
            fleeDistance = aIParameters.FleeDistance;
            sweepRadius = aIParameters.SweepRadius;
            shootingMarginErrorAngle = aIParameters.ShootingMarginErrorAngle;
            damageAccumulationDuration = aIParameters.DamageAccumulationDuration;
            fleeTriggerDamageThresholdPercentage = aIParameters.FleeTriggerDamageThresholdPercentage;
            recoveryThresholdPercentageForSearch = aIParameters.RecoveryThresholdPercentageForSearch;
            combatHealthAdvantageThreshold = aIParameters.CombatHealthAdvantageThreshold;
            damageThresholdPercentage = aIParameters.DamageThresholdPercentage;
            decisionPercentageHealth = aIParameters.DecisionPercentageHealth;
            decisionPercentageMaxSpeed = aIParameters.DecisionPercentageMaxSpeed;
            decisionPercentageAcceleration = aIParameters.DecisionPercentageAcceleration;
            decisionPercentageNormalShoot = aIParameters.DecisionPercentageNormalShoot;
            decisionPercentagePushShoot = aIParameters.DecisionPercentagePushShoot;
            decisionPercentageCooldown = aIParameters.DecisionPercentageCooldown;
            marginToFleeFromSandstorm = aIParameters.MarginToFleeFromSands;
        }

        /// <summary>
        /// Makes the IA go to another location that ISN'T A CHARACTER
        /// such as items, locations etc.
        /// </summary>
        private void ChangeTargetToGo(GameObject target)
        {
            _targetToGo = target;
            UpdateCurrentTarget(_targetToGo);
        }
        /// <summary>
        /// Makes the IA go to another CHARACTER
        /// </summary>
        private void ChangeTargetToShoot(GameObject target)
        {
            if (target != _targetToShoot) _targetAcquiredAt = Time.time;
            _targetToShoot = target;
            UpdateCurrentTarget(_targetToShoot);
        }
        /// <summary>
        /// Helper to change targets
        /// DO NOT USE IN OTHER METHODS
        /// </summary>
        /// 
        private void UpdateCurrentTarget(GameObject newTarget)
        {
            _currentTarget = newTarget;
            if (_currentTarget.TryGetComponent(out Target target))
            {
                _positionToDrive = target.TargetBehind.position;
            }
            else
            {
                _positionToDrive = _currentTarget.transform.position;
            }
        } 

        public void ModifySpeedMultiplier(float multiplier)
        { 
            speedMultiplier = Mathf.Clamp01(multiplier);

        }

        #endregion
    }

}

public class CarDamagedMe
{
    public float damageMade;
    public float timeThatHasPassed;
    public ITimer timerUntilRemove;
}

