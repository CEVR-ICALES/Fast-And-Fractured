using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SuckingBuilding : MonoBehaviour
    {
        public float detectionRadius = 10f;
        public float coneAngle = 45f; 
        public float pullForce = 30f;
        public bool mode = true;

        void FixedUpdate()
        {
            if (mode)
                DetectAndPullCharacters2();
            else
                DetectAndPullCharacters();
        }
        //this one doesnt have camera problems, but the sucking force doesnt work as intended, it doesnt feel like you ar being suck
        void DetectAndPullCharacters()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius);
            foreach (Collider collider in colliders)
            {
                Rigidbody rb = collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 directionToTarget = (collider.transform.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, directionToTarget);

                    if (angle < coneAngle / 2)
                    {
                        Vector3 pullDirection = (transform.position - collider.transform.position).normalized;
                        rb.AddForce(pullDirection * pullForce, ForceMode.Acceleration); 
                    }
                }
            }
        }
        //this on feels more good, the force is strong enough to pull the character, but allows you to fight against it, but the camera shakes because of the two forces fighting
        void DetectAndPullCharacters2()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius);
            foreach (Collider collider in colliders)
            {
                Rigidbody rb = collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 directionToTarget = (collider.transform.position - transform.position).normalized;
                    float angle = Vector3.Angle(transform.forward, directionToTarget);

                    if (angle < coneAngle / 2)
                    {
                        Vector3 pullDirection = (transform.position - collider.transform.position).normalized;
                        Vector3 newPosition = rb.position + pullDirection * pullForce * Time.fixedDeltaTime;
                        rb.MovePosition(newPosition);
                    }
                }
            }
        }
                    
        void OnDrawGizmos()
        {
            // Dibuja el rango de detección como una esfera
            Gizmos.color = new Color(0, 1, 0, 0.3f); // Verde semitransparente
            Gizmos.DrawSphere(transform.position, detectionRadius);

            // Dibuja el cono de absorción
            Gizmos.color = Color.yellow;
            Vector3 forward = transform.forward * detectionRadius;
            Quaternion leftRayRotation = Quaternion.Euler(0, -coneAngle / 2, 0);
            Quaternion rightRayRotation = Quaternion.Euler(0, coneAngle / 2, 0);

            Vector3 leftRayDirection = leftRayRotation * forward;
            Vector3 rightRayDirection = rightRayRotation * forward;

            Gizmos.DrawRay(transform.position, leftRayDirection);
            Gizmos.DrawRay(transform.position, rightRayDirection);
        }
    }
}
