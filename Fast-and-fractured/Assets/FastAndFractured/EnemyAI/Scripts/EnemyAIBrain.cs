using Enums;
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
        [Tooltip("Distance to retreat from the current target when executing in flee state.")]
        [SerializeField] float fleeDistance = 5f;
        [Tooltip("Radius of the sweep that the AI uses to search for possible enemies")]
        [SerializeField] float sweepRadius = 20f;
        [Tooltip("The shooting error that AI has on normal shoot")]
        [SerializeField] float shootingMarginErrorAngle = 0.02f;
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
        private float _minDistanceUntilNextCorner = 3f;

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
        [SerializeField] LayerMask ignoreLayerMask;

        PathMode pathMode = PathMode.ADVANCED;

        const float ANGLE_90 = 90f;
        const float ANGLE_30 = 30f;
        const float FRONT_ANGLE = 20f;
        const float MAX_INPUT_VALUE = 1f;
        const float SWEEP_FREQUENCY = 0.5f;
        const int MAX_ITEM_TYPE_ATTEMPTS = 20;

        // Driving: ease off the throttle into tight turns instead of taking every corner flat out.
        const float CORNER_SLOWDOWN_START_ANGLE = 35f;
        const float MIN_CORNER_THROTTLE = 0.35f;
        // Aiming: lead moving targets, and miss more the farther they are.
        const float MAX_LEAD_SECONDS = 1.2f;
        const float AIM_ERROR_DISTANCE = 30f;
        // Targeting: stick with a target a while instead of switching every frame.
        const float TARGET_COMMIT_SECONDS = 3f;
        const float SCORE_HUMAN_BONUS = 10f;
        const float SCORE_DAMAGED_ME_BONUS = 15f;
        const float SCORE_LOW_HEALTH_BONUS = 20f;

        // Personality, stable per AI and identical on every peer (seeded from its spawn point,
        // no shared random stream consumed): how well it aims, how fast it reacts, how
        // carefully it corners.
        private float _aimErrorScale = 1f;
        private float _reactionTime = 0.3f;
        private float _cornerCaution = 1f;
        private float _targetAcquiredAt;
        const float DISTANCE_MARGIN_ERROR = 35f;
        const int HEALTH_WEIGHT_PERCENTAGE = 3;
        const int START_CORNER_INDEX = 1;
        private const int PERCENTAGE_VALUE = 100;
        private Vector3 startPosition;
        private Quaternion startRotation;
        private float speedMultiplier=1;

        [Header("Aggressiveness parameters")]
        [Tooltip("Duration of continuous damage required to reach this value")]
        [SerializeField] private float damageAccumulationDuration = 5f;
        [Range(0, 100)][SerializeField] private float fleeTriggerDamageThresholdPercentage = 40;
        private ITimer damageAccumulationTimer;
        private float _currentAccumulatedDamageTime;
        [Tooltip("The main way to get out of fleestate. It should be lower than the variable below")]
        [Range(0, 100)][SerializeField] private float recoveryThresholdPercentageForSearch = 50;
        public float RecoveryThresholdPercentageForSearch => recoveryThresholdPercentageForSearch;
        [Tooltip("How much more health more the AI needs to have over the enemy to start attacking him from flee state")]
        [Range(0, 100)][SerializeField] private float combatHealthAdvantageThreshold = 60f;
        [Tooltip("Percentage threshold used to determine if a car has dealt enough damage relative to the endurance value.")]
        [Range(10, 100)][SerializeField] private int damageThresholdPercentage = 60;


        [Header("Item Type Priority")]
        [Tooltip("Range of weight on how likely it's going to choose that item type.\n" +
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

        [Range(-300, 600)][SerializeField] private int marginToFleeFromSandstorm = 0;
        private const float ENDURANCE_FACTOR_TO_FLEE_THE_SANDSTORM = 0.5f;
        private const int NUM_CHARACTERS_TO_CONSIDER_END_GAME = 3;
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

        private const float DECRESE_TIME_FACTOR_IF_TOUCHING_GROUND = 0.75f;


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
                            carMovementController.StartIsFlippedTimer(physicsBehaviour.IsTouchingGround ? DECRESE_TIME_FACTOR_IF_TOUCHING_GROUND : 1);
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
            
            // Subscriptions are handled in OnEnable/OnDisable. 
            // If statsController was null during OnEnable, we subscribe here.
            // If it was already subscribed, RemoveListener ensures we don't double-subscribe.
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
            int seed = unchecked(Mathf.RoundToInt(startPosition.x * 10f) * 73856093 ^ Mathf.RoundToInt(startPosition.z * 10f) * 19349663);
            var rng = new System.Random(seed);
            float Roll(float min, float max) => Mathf.Lerp(min, max, (float)rng.NextDouble());

            _aimErrorScale = Roll(0.6f, 1.8f);
            _reactionTime = Roll(0.15f, 0.5f);
            _cornerCaution = Roll(0.7f, 1.3f);
            // Some are bolder, some flee sooner.
            fleeTriggerDamageThresholdPercentage = Mathf.Clamp(fleeTriggerDamageThresholdPercentage + Roll(-10f, 10f), 5f, 95f);
            combatHealthAdvantageThreshold = Mathf.Clamp(combatHealthAdvantageThreshold + Roll(-10f, 10f), 5f, 95f);
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
                // Lift off into a tight turn, like a driver would, rather than flat out always.
                float slowFrom = CORNER_SLOWDOWN_START_ANGLE / _cornerCaution;
                inputY = Mathf.Lerp(MAX_INPUT_VALUE, MIN_CORNER_THROTTLE, Mathf.InverseLerp(slowFrom, ANGLE_90, Mathf.Abs(angle)));
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
            return distance < DISTANCE_MARGIN_ERROR;
        }
        #endregion

        #region SearchState
        public void ChoosePlayer()
        {
            // Online every AI used to hunt the same car - the host's, LevelController's
            // LocalPlayer. Go for the best human target instead (in singleplayer the only human
            // is the player, so nothing changes there).
            GameObject target = ChooseBestTarget(humansOnly: true);
            if (target == null) target = _player;
            if (target != null)
            {
                ChangeTargetToShoot(target);
            }
        }

        public void ChooseItemFromType()
        {
            // With no stat item left on the map the loop below could never find one and never
            // end (the empty-list exception further down used to be what broke out of it).
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
                if (itemDistance < nearestOne && (angle < -ANGLE_30 || angle > ANGLE_30))
                {
                    nearestOne = itemDistance;
                    nearestTarget = statItem.gameObject;
                }
            }

            ChangeTargetToGo(nearestTarget);
        }



        public void ChooseNearestCharacter()
        {
            // Not strictly the nearest any more: nearby, weakened, or the one that hurt me -
            // and keep the current target for a while unless it is gone.
            GameObject target = ChooseBestTarget(humansOnly: false);
            if (target == null) target = CalcNearestCharacter();
            ChangeTargetToShoot(target);
        }

        /// <summary>
        /// Lowest score wins: distance in metres, minus bonuses for humans, for whoever damaged
        /// this AI recently and for low health. Keeps the current target while it is alive and
        /// was picked less than TARGET_COMMIT_SECONDS ago.
        /// </summary>
        private GameObject ChooseBestTarget(bool humansOnly)
        {
            // LevelController picks the first target before this AI has initialized.
            if (carMovementController == null || !LevelController.HasInstance) return null;
            if (_targetToShoot != null && IsAliveCharacter(_targetToShoot) &&
                Time.time - _targetAcquiredAt < TARGET_COMMIT_SECONDS &&
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
                if (!ai) score -= SCORE_HUMAN_BONUS;
                if (_carsThatDamagedAI.ContainsKey(character)) score -= SCORE_DAMAGED_ME_BONUS;
                var stats = character.GetComponentInChildren<StatsController>();
                if (stats != null) score -= (1f - stats.GetEndurancePercentage() / PERCENTAGE_VALUE) * SCORE_LOW_HEALTH_BONUS;

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
                if (safeZoneDistance < nearestOne && (angle < -ANGLE_30 || angle > ANGLE_30))
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
            return (angle > -FRONT_ANGLE && angle < FRONT_ANGLE);
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
            if (Vector3.Distance(_currentPosition, GetActivePathPoint()) < _minDistanceUntilNextCorner &&
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
            // Aim where the target will be when the bullet arrives, not where it is now.
            Vector3 shootFrom = normalShootHandle.ShootPoint.position;
            Vector3 aimPoint = _currentTarget.transform.position;
            float distance = Vector3.Distance(shootFrom, aimPoint);
            Rigidbody targetBody = _currentTarget.GetComponentInParent<Rigidbody>();
            if (targetBody == null) targetBody = _currentTarget.GetComponentInChildren<Rigidbody>();
            float bulletSpeed = statsController.NormalShootSpeed;
            if (targetBody != null && bulletSpeed > 1f)
                aimPoint += targetBody.linearVelocity * Mathf.Min(distance / bulletSpeed, MAX_LEAD_SECONDS);

            Vector3 shootingDirection = (aimPoint - shootFrom).normalized;
            if (turretRotationMovement != null)
            {
                turretRotationMovement.TargetDirection = shootingDirection;
            }
            //Add shooting error: per-AI skill, and wider the farther the target is
            float error = shootingMarginErrorAngle * _aimErrorScale * (1f + distance / AIM_ERROR_DISTANCE);
            return shootingDirection + new Vector3(UnityEngine.Random.Range(-error, error),
                UnityEngine.Random.Range(0, error), UnityEngine.Random.Range(-error, error));
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
            decisionPercentageHealth = Mathf.RoundToInt(_startingPercentageHealth + statsController.GetEndurancePercentage() * HEALTH_WEIGHT_PERCENTAGE);
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
            float enduranceFactor = (1 - (statsController.Endurance / statsController.MaxEndurance)) * ENDURANCE_FACTOR_TO_FLEE_THE_SANDSTORM;
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
            return NUM_CHARACTERS_TO_CONSIDER_END_GAME >= LevelController.Instance.InGameCharacters.Count;
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

