using Enums;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class CarmeUniqueAbility : BaseUniqueAbility
    {
        public GameObject chickenPrefab;
        public Transform uniqueAbilityShootPoint;
        [SerializeField]
        private PhysicsBehaviour physicsBehaviour;

        [Header("Land Point")]
        [SerializeField] private float rayCastDistance;
        [SerializeField] private LayerMask obstacleLayerMask;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private float groundCheckDistance;
        [SerializeField] private float landPointYOffset;
        public bool drawDebugLines = true;


        private Vector3 _aimDirection;
        private Ray _primaryRay;
        private Ray _groundRay;
        private RaycastHit _primaryHit;
        private RaycastHit _groundHit;
        private ITimer _durationTimer;

        [SerializeField] private ShootingHandle shootingHandle;

        private void Start()
        {
            StartCooldown();
        }

        public override bool ActivateAbility() //may be necessary to increase the range of the initial raycast considering teh car speed
        {
            if (base.ActivateAbility())
            {
                _aimDirection = shootingHandle.CurrentShootDirection + physicsBehaviour.Rb.velocity ;
                // remove vertical component while maintaining direction relative to car
                _aimDirection = Vector3.ProjectOnPlane(_aimDirection, Vector3.up).normalized;
                CalculateLandingPoint();
                return true;
            }

            return false;
        }

        private void InitializeAbility(Vector3 landPoint)
        {
            EndAbilityEffects();
            landPoint.y = landPoint.y + landPointYOffset;
            GameObject uniqueAbility = Instantiate(chickenPrefab, uniqueAbilityShootPoint.position, Quaternion.LookRotation(_aimDirection));
            uniqueAbility.GetComponent<McChicken>().InitializeChicken(landPoint, _aimDirection);
            _durationTimer = TimerSystem.Instance.CreateTimer(abilityData.Duration, TimerDirection.INCREASE, () =>
            {
                DestroyUniqueAbility(uniqueAbility);
            });

        }

        private void OnAbilityCantBeInitialized()
        {
            EndAbilityEffects();
            StopCooldown();
        }

        private void DestroyUniqueAbility(GameObject uniqueAbility)
        {
            StopCooldown(); 
            Destroy(uniqueAbility);
        }

        #region Calculate Landing Ppint
        private void CalculateLandingPoint()
        {
            _primaryRay.origin = uniqueAbilityShootPoint.position;
            _primaryRay.direction = _aimDirection;

            bool hitObstacle = Physics.Raycast(_primaryRay, out _primaryHit, rayCastDistance, obstacleLayerMask);

            Vector3 checkPoint = hitObstacle ? _primaryHit.point : _primaryRay.GetPoint(rayCastDistance);

            if (drawDebugLines)
            {
                Debug.DrawLine(uniqueAbilityShootPoint.position, checkPoint, Color.green, 8f);
            }

            CheckDownwardRaycast(checkPoint);

        }

        private void CheckDownwardRaycast(Vector3 origin)
        {
            _groundRay.origin = origin;
            _groundRay.direction = Vector3.down;

            bool hitPoint = Physics.Raycast(_groundRay, out _groundHit, groundCheckDistance, groundLayerMask);

            if (drawDebugLines)
            {
                Debug.DrawLine(origin, _groundHit.point , Color.blue, 8f);
            }

            if (hitPoint)
            {
                InitializeAbility(_groundHit.point);
            } else
            {
                OnAbilityCantBeInitialized();
            }
        }

        #endregion

    }
}

