using UnityEngine;

namespace DestroyIt
{
    public class Follow : MonoBehaviour
    {
        public Transform objectToFollow;
        public FacingDirection facingDirection = FacingDirection.FollowedObject;
        [HideInInspector]
        public bool isPositionFixed;
        [HideInInspector]
        public Vector3 fixedFromPosition = Vector3.zero;
        [HideInInspector]
        public float fixedDistance;

        void Start()
        {
            if (objectToFollow == null)
            {
                Debug.Log("[DestroyIt-Follow]: No transform was provided. Nothing to follow. Removing script...");
                Destroy(this);
            }
        }

        void LateUpdate()
        {
            if (objectToFollow != null)
            {
                if (isPositionFixed)
                {
                    // get point along line from player to shockwave start
                    Vector3 followPoint = objectToFollow.position.LerpByDistance(fixedFromPosition, fixedDistance);
                    transform.position = followPoint;
                }
                else
                    transform.position = objectToFollow.position;

                switch (facingDirection)
                {
                    case FacingDirection.FollowedObject:
                        transform.LookAt(objectToFollow);
                        break;
                    case FacingDirection.FixedPosition:
                        transform.LookAt(fixedFromPosition);
                        break;
                }
            }
        }

        void OnDrawGizmos()
        {
            if (isPositionFixed && objectToFollow != null)
                Gizmos.DrawLine(fixedFromPosition, objectToFollow.position);
            
            Gizmos.DrawWireSphere(transform.position, .5f);
        }
    }
}