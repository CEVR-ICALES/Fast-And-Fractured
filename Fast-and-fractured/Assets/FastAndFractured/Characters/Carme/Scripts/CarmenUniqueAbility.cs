using UnityEngine;
using static UnityEngine.UI.Image;

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
            //_aimDirection = 
            CalculateLandingPoint();

            // this will be called once the landiong point is calculated
            //GameObject uniqueAbility = Instantiate(chickenPrefab, uniqueAbilityShootPoint.position, Quaternion.identity);
            //_aimDirection = GetComponent<ShootingHandle>().CurrentShootDirection;
            //uniqueAbility.GetComponent<McChicken>().InitializeChicken(_aimDirection);

        }

        private void CalculateLandingPoint()
        {
            _primaryRay.origin = uniqueAbilityShootPoint.position;
            _primaryRay.direction = _aimDirection;

            bool hitObstacle = Physics.Raycast(_primaryRay, out _primaryHit, rayCastDistance, obstacleLayerMask);

            Vector3 checkPoint = hitObstacle ? _primaryHit.point : _primaryRay.GetPoint(rayCastDistance);

            if (drawDebugLines)
            {
                Debug.DrawLine(uniqueAbilityShootPoint.position, checkPoint, hitObstacle ? Color.blue : Color.yellow, 2f);
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
                Debug.DrawLine(origin, _groundHit.point , hitPoint ? Color.blue : Color.yellow, 2f);
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

