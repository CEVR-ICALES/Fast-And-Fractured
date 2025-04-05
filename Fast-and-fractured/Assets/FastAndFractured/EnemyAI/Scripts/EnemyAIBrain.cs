using Enums;
using StateMachine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TextCore.Text;
using Utilities;

namespace FastAndFractured
{
    public class EnemyAIBrain : MonoBehaviour
    {
        //Setted in inspector
        [SerializeField] NavMeshAgent agent;
        NavMeshPath _currentPath;
        Vector3[] _previousPath;
        [SerializeField] float fleeMagnitude = 5f;
        [SerializeField] float sweepRadius = 20f;
        [SerializeField] float shootingMarginErrorAngle = 2f;
        [SerializeField] LayerMask sweepLayerMask;


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
        [SerializeField] LayerMask ignoreLayerMask;

        PathMode pathMode = PathMode.ADVANCED;

        const float MAX_ANGLE_DIRECTION = 90f;
        const float FRONT_ANGLE = 20f;
        const float MAX_INPUT_VALUE = 1f;
        const int HEALTH_WEIGHT_PERCENTAGE = 3;
        const int START_CORNER_INDEX = 1;
        private Vector3 startPosition;
        private Quaternion startRotation;

        [Header("Aggressiveness parameters")]
        [Tooltip("Duration of continuous damage required to reach this value")]
        [SerializeField] private float damageAccumulationDuration = 5f;
        [Range(0, 100)][SerializeField] private float fleeTriggerDamageThresholdPercentage = 40;
        private ITimer damageAccumulationTimer;
        private float _currentAccumulatedDamageTime;
        [Tooltip("The main way to get out of fleestate. It should be lower than the variable below")]
        [Range(0, 100)][SerializeField] private float _recoveryThresholdPercentageForSearch = 50;
        public float RecoveryThresholdPercentageForSearch => _recoveryThresholdPercentageForSearch;
        [Tooltip("How much more health more the AI needs to have over the enemy to start attacking him from flee state")]
        [Range(0, 100)][SerializeField] private float _combatHealthAdvantageThreshold = 60f;


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
        private int _totalDecisionPercentage = 0;
        private int _startingPercentageHealth = 0;
        public Stats StatToChoose => _statToChoose;
        private Stats _statToChoose;


        private void OnEnable()
        {
            if (statsController)
            {
                statsController.onEnduranceDamageTaken.AddListener(OnTakeEnduranceDamage);
                statsController.onEnduranceDamageHealed.AddListener(OnTakeEnduranceHealed);
            }
        }


        private void OnDisable()
        {
            statsController.onEnduranceDamageTaken.RemoveListener(OnTakeEnduranceDamage);
            statsController.onEnduranceDamageHealed.RemoveListener(OnTakeEnduranceHealed);
        }
        private void Awake()
        {
            LevelController.Instance.charactersCustomStart.AddListener(InitializeAIValues);
        }

        private void InitializeAIValues()
        {
            //Testing 
            _targetToShoot = _player;
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
            //physicsBehaviour.Rb = GetComponent<Rigidbody>();
            _currentPath = new NavMeshPath();
            _previousPath = new Vector3[0];
            startPosition = carMovementController.transform.position;
            startRotation = carMovementController.transform.rotation;
            _startingPercentageHealth = decisionPercentageHealth;
            statsController.onEnduranceDamageTaken.AddListener(OnTakeEnduranceDamage);
            statsController.onEnduranceDamageHealed.AddListener(OnTakeEnduranceHealed);
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
            float inputX = -Mathf.Min(angle / MAX_ANGLE_DIRECTION, MAX_INPUT_VALUE);

            //Forward/Backward
            //If angle between 90 and -90 go forward
            //If angle more than 90 or less than -90 go backwards
            float inputY = MAX_INPUT_VALUE;
            if (angle > MAX_ANGLE_DIRECTION || angle < -MAX_ANGLE_DIRECTION)
            {
                inputY = -MAX_INPUT_VALUE;
            }

            Vector2 input = new Vector2(inputX, inputY);


            carMovementController.HandleSteeringInput(input);
        }

        public void UpdateTargetPosition()
        {
            _positionToDrive = _targetToShoot.transform.position;
            _currentTarget = _targetToShoot;
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
            return Vector3.Distance(transform.position, _currentTarget.transform.position) < 2;
        }
        #endregion

        #region SearchState
        public void ChoosePlayer()
        {
            AssignTarget(_player);
            _positionToDrive = _player.transform.position;
        }

        public void ChooseItemFromType()
        {
            do
            {
                CalculateTotalDecisionPercentage();
                //+1 because it's max is exclusive
                int decision = Random.Range(0, _totalDecisionPercentage + 1);

                int percentageMaxSpeed = decisionPercentageHealth + decisionPercentageMaxSpeed;
                int percentageAcceleration = percentageMaxSpeed + decisionPercentageAcceleration;
                int percentageNormalShoot = percentageAcceleration + decisionPercentageNormalShoot;
                int percentagePushShoot = percentageNormalShoot + decisionPercentagePushShoot;
                int percentageCooldown = percentagePushShoot + decisionPercentageCooldown;

                switch (decision)
                {
                    default:
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
                        _statToChoose = Stats.PUSH_DAMAGE;
                        break;
                    case int n when (n <= percentageCooldown):
                        _statToChoose = Stats.COOLDOWN_SPEED;
                        break;
                }
            } while (!InteractableHandler.Instance.CheckIfStatItemExists(_statToChoose));

            GetClosestItemByList(InteractableHandler.Instance.GetOnlyStatBoostItemsStat(_statToChoose));
        }

        public void ChooseNearestItem()
        {
            GetClosestItemByList(InteractableHandler.Instance.GetStatBoostItems());
        }

        public void ChooseNearestCharacter()
        {
            List<GameObject> inGameCharacters = LevelController.Instance.InGameCharacters;
            GameObject nearestTarget = inGameCharacters[0].gameObject != carMovementController.gameObject ? inGameCharacters[0] : inGameCharacters[1];
            var nearestOne = float.MaxValue;
    
            foreach (var character in inGameCharacters)
            {
                float characterDistance = (character.transform.position - carMovementController.transform.position).sqrMagnitude;
                if (characterDistance < nearestOne && character.gameObject != carMovementController.gameObject)
                {
                    nearestOne = characterDistance;
                    nearestTarget = character;
                }
            }
            _targetToShoot = nearestTarget;
            _currentTarget = _targetToShoot;
        }

        public void ChooseNearestDangerZone()
        {
            //TODO
            //Get list of danger zones and choose the nearest one
        }

        public void ChooseNearestSafeZone()
        {
            //TODO
            //Get list of safe zones and choose the nearest one
        }

        public void ChooseNearestHelpfulNpc()
        {
            //TODO
            //Get list of NPCs and choose the nearest one
        }


        #endregion

        #region CombatState
        public void NormalShoot()
        {
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
            pushShootHandle.CurrentShootDirection = GetShootingDirectionWithError();
            pushShootHandle.PushShooting();
        }

        public void UseUniqueAbility()
        {
            //TODO
            //Use unique ability
        }
        #endregion

        #region DashState
        public void Dash()
        {
            //TODO
            carMovementController.HandleDashWithPhysics();
        }

        #endregion

        #region FleeState
        public void RunAwayFromCurrentTarget()
        {
            _positionToDrive = -CalcNormalizedTargetDirection() * fleeMagnitude;
        }

        #endregion
        #endregion

        #region Decisions
        public bool EnemySweep()
        {
            Collider[] colliders = Physics.OverlapSphere(carMovementController.transform.position, sweepRadius, sweepLayerMask);

            return colliders.Length > 0;
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
            return distance > Vector3.Distance(carMovementController.transform.position, _positionToDrive);
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
            var enemyStatsController = _targetToShoot.GetComponent<StatsController>();
            if (enemyStatsController == null) { return false; }


            float healthDifference = statsController.GetEndurancePercentage() - enemyStatsController.GetEndurancePercentage();
            return healthDifference < _combatHealthAdvantageThreshold;
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
            return statsController.GetEndurancePercentage() > _recoveryThresholdPercentageForSearch;
        }

        public bool HasTargetToShoot()
        {
            return LevelController.Instance.InGameCharacters.Contains(TargetToShoot);
        }

        #endregion

        #region Helpers
        private void OnTakeEnduranceDamage(float damageTaken)
        {
            RegisterSuddenly(damageTaken);
            RecalculateDecisionsPercentage();
        }

        private void OnTakeEnduranceHealed(float damageHealed)
        {
            RecalculateDecisionsPercentage();
        }

        private void AssignTarget(GameObject target)
        {
            _targetToShoot = target;
            _positionToDrive = _targetToShoot.transform.position;
        }

        private Vector3 CalcNormalizedTargetDirection()
        {
            return (_targetToShoot.transform.position - carMovementController.transform.position).normalized;
        }

        private float GetAngleDirection(Vector3 axis)
        {
            Vector3 direction;
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
                        Debug.LogWarning("No path to follow" + _currentPath.ToString());
                        if (Physics.Raycast(_positionToDrive, Vector3.down, out var hit, float.MaxValue, ignoreLayerMask))
                        {
                            Debug.DrawRay(_positionToDrive, Vector3.down, Color.magenta);

                            _positionToDrive = hit.point;
                            TryToCalculatePath();
                            CheckIfGoToNextPathPoint();

                        }
                    }
                    ;

                    break;
            }
            direction = (_positionToDrive - carMovementController.transform.position);

            //If it's negative, go left
            //If it's positive, go right
            return Vector3.SignedAngle(direction, carMovementController.transform.forward, axis);
        }

        bool TryToCalculatePath()
        {
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
                    Debug.LogError("THE PATH ONLY HAS ONE POINT. This is probably because you put the car too far away from the ground");

                    return _currentPath.corners[0];
                case > 0:
                    return _currentPath.corners[_pathIndex];
                default:
                    return Vector3.zero;
            }
        }

        private void CheckIfGoToNextPathPoint()
        {
            if (Vector3.Distance(carMovementController.transform.position, GetActivePathPoint()) < _minDistanceUntilNextCorner &&
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

        private Vector3 GetShootingDirectionWithError()
        {
            Vector3 shootingDirection = CalcNormalizedTargetDirection();

            //Add shooting error 
            return shootingDirection + new Vector3(UnityEngine.Random.Range(-shootingMarginErrorAngle, shootingMarginErrorAngle),
                UnityEngine.Random.Range(0, shootingMarginErrorAngle), 0);
        }

        public void StopMovement()
        {
            physicsBehaviour.Rb.angularVelocity = Vector3.zero;
            physicsBehaviour.Rb.velocity = Vector3.zero;
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
            float nearestOne = float.MaxValue;
            List<StatsBoostInteractable> items = list;
            GameObject nearestTarget = items[0].gameObject;
            foreach (StatsBoostInteractable statItem in items)
            {
                float itemDistance = (statItem.transform.position - carMovementController.transform.position).sqrMagnitude;
                if (itemDistance < nearestOne)
                {
                    nearestOne = itemDistance;
                    nearestTarget = statItem.gameObject;
                }
                nearestTarget = statItem.gameObject;
            }

            _targetToGo = nearestTarget;
            _currentTarget = _targetToGo;
        }

        //Is obsolete but can be used in the future
        //#if UNITY_EDITOR
        //        // Save their previous values so we can identify which one changed.
        //        int _checkHealth;
        //        int _checkSpeed;
        //        int _checkAcceleration;
        //        int _checkNormal;
        //        int _checkPush;
        //        int _checkCooldown;

        //        void OnValidate()
        //        {

        //            // Skip this if we haven't cached the values yet.
        //            if (_checkHealth >= 0)
        //            {

        //                // Find which value the user changed, and update the rest from it.
        //                if (_checkHealth != decisionPercentageHealth)
        //                {
        //                    DistributeProportionately(ref decisionPercentageHealth, 
        //                        ref decisionPercentageMaxSpeed, 
        //                        ref decisionPercentageAcceleration, 
        //                        ref decisionPercentageNormalShoot, 
        //                        ref decisionPercentagePushShoot, 
        //                        ref decisionPercentageCooldown);
        //                }
        //                else if (_checkSpeed != decisionPercentageMaxSpeed)
        //                {
        //                    DistributeProportionately(ref decisionPercentageMaxSpeed,
        //                        ref decisionPercentageHealth,
        //                        ref decisionPercentageAcceleration,
        //                        ref decisionPercentageNormalShoot,
        //                        ref decisionPercentagePushShoot,
        //                        ref decisionPercentageCooldown);
        //                }
        //                else if (_checkAcceleration != decisionPercentageAcceleration)
        //                {
        //                    DistributeProportionately(ref decisionPercentageAcceleration,
        //                        ref decisionPercentageHealth,
        //                        ref decisionPercentageMaxSpeed,
        //                        ref decisionPercentageNormalShoot,
        //                        ref decisionPercentagePushShoot,
        //                        ref decisionPercentageCooldown);
        //                }
        //                else if (_checkNormal != decisionPercentageNormalShoot)
        //                {
        //                    DistributeProportionately(ref decisionPercentageNormalShoot,
        //                        ref decisionPercentageHealth,
        //                        ref decisionPercentageMaxSpeed,
        //                        ref decisionPercentageAcceleration,
        //                        ref decisionPercentagePushShoot,
        //                        ref decisionPercentageCooldown);
        //                }
        //                else if (_checkPush != decisionPercentagePushShoot)
        //                {
        //                    DistributeProportionately(ref decisionPercentagePushShoot,
        //                        ref decisionPercentageHealth,
        //                        ref decisionPercentageMaxSpeed,
        //                        ref decisionPercentageAcceleration,
        //                        ref decisionPercentageNormalShoot,
        //                        ref decisionPercentageCooldown);
        //                }
        //                else if (_checkCooldown != decisionPercentageCooldown)
        //                {
        //                    DistributeProportionately(ref decisionPercentageCooldown,
        //                        ref decisionPercentageHealth,
        //                        ref decisionPercentageMaxSpeed,
        //                        ref decisionPercentageAcceleration,
        //                        ref decisionPercentageNormalShoot,
        //                        ref decisionPercentagePushShoot);
        //                }

        //            }

        //            _checkHealth = decisionPercentageHealth;
        //            _checkSpeed = decisionPercentageMaxSpeed;
        //            _checkAcceleration = decisionPercentageAcceleration;
        //            _checkNormal = decisionPercentageNormalShoot;
        //            _checkPush = decisionPercentagePushShoot;
        //            _checkCooldown = decisionPercentageCooldown;
        //        }


        //        void DistributeProportionately(ref int changed, ref int a, ref int b, ref int c, ref int d, ref int e)
        //        {
        //            changed = (int)Mathf.Clamp(changed, 0f, MAX_PERCENTAGE_100);
        //            int total = MAX_PERCENTAGE_100 - changed;

        //            int oldTotal = a + b + c + d + e;
        //            Debug.Log(oldTotal);
        //            if (oldTotal > 0f)
        //            {
        //                float fraction = 1f / oldTotal;
        //                a = Mathf.RoundToInt(total * a * fraction);
        //                b = Mathf.RoundToInt(total * b * fraction);
        //                c = Mathf.RoundToInt(total * c * fraction);
        //                d = Mathf.RoundToInt(total * d * fraction);
        //                e = Mathf.RoundToInt(total * e * fraction);
        //            }
        //            else
        //            {
        //                a = b = c = d = e = total / 5;
        //            }

        //            // Assign any rounding error to the last one, arbitrarily.
        //            // (Better rounding rules exist, so take this as an example only)
        //            //a += total - a - b - c - d - e;
        //        }
        //#endif

        #endregion
    }
}

