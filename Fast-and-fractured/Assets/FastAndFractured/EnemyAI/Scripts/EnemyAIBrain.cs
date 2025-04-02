using Enums;
using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements;
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
        private GameObject _targetToShoot;
        //path index starts at 1 because 0 is their actual position
        private int _pathIndex = START_CORNER_INDEX;
        private float _minDistanceUntilNextCorner = 3f;
        public Vector3 PositionToDrive { get => _positionToDrive; set => _positionToDrive = value; }
        public GameObject Player { get => _player; set => _player = value; }
        public GameObject Target { get => _targetToShoot; set => _targetToShoot = value; }

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
        const int START_CORNER_INDEX = 1;
        Vector3 startPosition;
        Quaternion startRotation;
        [Header("Aggressiveness parameters")]
        [Tooltip("During how much time needs to suffer damage continously until reach this value")][SerializeField] private float suddenlyLostTime = 5f;
        [Range(0, 100)] [SerializeField] private float endurancePercentageSuddenlyLostNeedToChangeToFleeState = 40;
        ITimer suddenlyLostTimer;
        private float _currentSuddenlyLostTimeAmount;
        [Tooltip("The main way to get out of fleestate. It should be lower than the variable below")][Range(0,100)][SerializeField] private float endurancePercentageNeededToChangeFromFleeToSearchState =50;
        [Tooltip("How much more health more the AI needs to have over the enemy to start attacking him from flee state")]
        [Range(0,100)][SerializeField] private float healthDifferenceNeededToChangeFromFleeToCombatState =60f;
        private void OnEnable()
        {
            statsController.onEnduranceDamageTaken.AddListener(OnTakeEnduranceDamage);
        }


        private void OnDisable()
        {
            statsController.onEnduranceDamageTaken.RemoveListener(OnTakeEnduranceDamage);
        }
        private void Awake()
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
            physicsBehaviour.Rb = GetComponent<Rigidbody>();
            _currentPath = new NavMeshPath();
            _previousPath = new Vector3[0];
            startPosition = carMovementController.transform.position;
            startRotation = carMovementController.transform.rotation;
        }
        public void ReturnToStartPosition()
        {
            carMovementController.transform.position = startPosition;
            carMovementController.transform.rotation = startRotation;
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
        public void RegisterSuddenly(float damageTaken)
        {
            _currentSuddenlyLostTimeAmount += damageTaken;
            if (suddentlyLostTimer == null)
            {
                suddentlyLostTimer = TimerSystem.Instance.CreateTimer(suddenlyLostTime, TimerDirection.INCREASE, onTimerIncreaseComplete: () =>
                {
                    suddentlyLostTimer = null;
                    _currentSuddenlyLostTimeAmount = 0;
                });
            }
            else
            {
                TimerSystem.Instance.ModifyTimer(suddentlyLostTimer, newCurrentTime: 0);
            }
        }
        #endregion

        #region SearchState

        [Obsolete("We don't use setDestination anymore")]
        public void GoToPositionDeprecated()
        {
            agent.SetDestination(_positionToDrive);
        }

        public void SearchPlayerPosition()
        {
            AssignTarget(_player);
            _positionToDrive = _player.transform.position;
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
            return healthDifference < healthDifferenceNeededToChangeFromFleeToCombatState;
        }
        public bool WantsToChangeToFleeState()
        {
            bool condition = _currentSuddenlyLostTimeAmount >= enudurancePercentageSuddentlyLostNeedToChangeToFleeState;
            if (condition)
            {
                return condition;
            }
            //TODO if in the future we need more extra conditions is posible to add them here
            return condition;
        }

        public bool WantsToChangeFromFleeToSearchState()
        {
            return statsController.GetEndurancePercentage() > endurancePercentageNeededToChangeFromFleeToSearchState;
        }
        #endregion

        private void OnTakeEnduranceDamage(float damageTaken)
        {
            RegisterSuddently(damageTaken);
        }
        #region Helpers

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
        #endregion
    }
}

