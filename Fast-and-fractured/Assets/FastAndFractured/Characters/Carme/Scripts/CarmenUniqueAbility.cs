using UnityEngine;

namespace FastAndFractured
{
    public class CarmenUniqueAbility : BaseUniqueAbility
    {
        public GameObject chickenPrefab;
        public Transform uniqueAbilityShootPoint;

        [Header("Land Point")]
        [SerializeField] private float rayCastDistance;
        [SerializeField] private LayerMask obstacleLayerMask;
        [SerializeField] private LayerMask groundLayerMask;
        [SerializeField] private float groundCheckDistance;
        public bool drawDebugLines = true;


        private Vector3 _aimDirection;
        private Ray _primaryRay;
        private Ray _groundRay;
        private RaycastHit _primaryHit;
        private RaycastHit _groundHit;

        public override void ActivateAbility()
        {
            base.ActivateAbility();
            Vector3 carRight = transform.right;
            _aimDirection = GetComponent<ShootingHandle>().CurrentShootDirection; 
            // Remove vertical component while maintaining direction relative to car
            //_aimDirection = Vector3.ProjectOnPlane(_aimDirection, Vector3.up).normalized;
            //_aimDirection = Vector3.ProjectOnPlane(_aimDirection, carRight).normalized;
            Debug.Log(_aimDirection);
            CalculateLandingPoint();

        }

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
            }
        }

        private void InitializeAbility(Vector3 landPoint)
        {
            Debug.Log("HITTT");
            GameObject uniqueAbility = Instantiate(chickenPrefab, landPoint, Quaternion.LookRotation(_aimDirection));
        }


        
    }
}

