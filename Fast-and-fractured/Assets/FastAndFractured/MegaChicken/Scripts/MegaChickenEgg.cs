using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Utilities;
using StateMachine;
using Enums;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class MegaChickenEgg : MonoBehaviour, IPooledObject, IPausable
    {
        private bool initValues = true;
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => initValues;
        private NavMeshAgent agent;
        public Transform visualModel;
        private Vector3 lastPosition;
        private float radius;
        public Collider ownCollider;
        public SphereCollider pushZoneCollider;
        private Rigidbody ownRigidbody;
        public GameObject explosionParticles;
        private bool _isPaused = false;
        private float timeEnabled = 0f;
        public float maxTimeEnabled = 20f;

        [Header("Push related")]
        [SerializeField] private bool isGrounded = true;
        [SerializeField] private float applyForceYOffset = 1f;
        [SerializeField] private ForceMode forceMode = ForceMode.Force;
        [SerializeField, Range(0f, 100f)] private float forceMultiplier = 5f;

        private const int GROUND_LAYER = 3;
        private const float TIME_UNTIL_REACTIVATE_FISICS = 1f;
        private const float EXPLOSION_TIME = 1.5f;
        private const float NORMAL_RADIUS = 0.68f;
        private const float MAX_RADIUS = 1.5f;
        private const float AGENT_SPEED = 40f;
        private const float POINT_RADIUS = 500f;

        public virtual void InitializeValues()
        {

        }

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
            if (agent == null)
            {
                ownRigidbody = GetComponent<Rigidbody>();
                agent = GetComponent<NavMeshAgent>();
                agent.updateRotation = false;
                SphereCollider col = GetComponent<SphereCollider>();
                if (col != null)
                {
                    float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                    radius = col.radius * maxScale;
                }
            }
            visualModel.gameObject.SetActive(true);
            agent.speed = 0f;
            ownCollider.enabled = false;
            ownRigidbody.isKinematic = true;
            pushZoneCollider.enabled = false;
            explosionParticles.SetActive(false);
            timeEnabled = 0f;
            GetRandomPoint();
            TimerSystem.Instance.CreateTimer(TIME_UNTIL_REACTIVATE_FISICS, onTimerDecreaseComplete: () =>
            {
                ownCollider.enabled = true;
                ownRigidbody.isKinematic = false;
                pushZoneCollider.enabled = true;
                pushZoneCollider.radius = NORMAL_RADIUS;
                agent.speed = AGENT_SPEED;
            });
        }
        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
        }

        void Update()
        {
            if (_isPaused)
            {
                agent.speed = 0;
                return;
            }
            if (!agent.pathPending && agent.remainingDistance < 1f || timeEnabled >= maxTimeEnabled)
            {
                Explosion();
            }
            if (visualModel != null && agent.enabled && agent.velocity.sqrMagnitude > 0.01f)
            {
                timeEnabled += Time.deltaTime;
                Vector3 movement = transform.position - lastPosition;
                lastPosition = transform.position;

                if (movement.magnitude > 0.0001f)
                {
                    float distance = movement.magnitude;
                    float rotation = (distance / radius) * Mathf.Rad2Deg;

                    Vector3 ejeRotacion = Vector3.Cross(Vector3.up, movement.normalized);

                    visualModel.Rotate(ejeRotacion, rotation, Space.World);
                }
            }
        }
        private void GetRandomPoint()
        {
            Vector3 randomDirection = Random.insideUnitSphere * POINT_RADIUS;
            randomDirection += transform.position;
            NavMeshHit hit;
            if (NavMesh.SamplePosition(randomDirection, out hit, POINT_RADIUS, 1))
            {
                agent.SetDestination(hit.position);
            }
        }
        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.TryGetComponent(out PhysicsBehaviour otherComponentPhysicsBehaviours))
            {
                PushCharacter(other, otherComponentPhysicsBehaviours);
            }
        }
        private void PushCharacter(Collider other, PhysicsBehaviour otherComponentPhysicsBehaviours)
        {
            if (otherComponentPhysicsBehaviours.CarImpactHandler.CheckForModifiedCarState() == ModifiedCarState.JOSEFINO_INVULNERABLE)
            {
                otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
                return;
            }

            otherComponentPhysicsBehaviours.CancelDash();
            float otherCarEnduranceFactor = otherComponentPhysicsBehaviours.StatsController.Endurance / otherComponentPhysicsBehaviours.StatsController.MaxEndurance;
            float otherCarWeight = otherComponentPhysicsBehaviours.StatsController.Weight;
            float otherCarEnduranceImportance = otherComponentPhysicsBehaviours.StatsController.EnduranceImportanceWhenColliding;
            float forceToApply;

            Vector3 otherPosition = other.transform.position;

            Vector3 contactPoint = pushZoneCollider.ClosestPoint(otherPosition);

            Vector3 vectorCenterToContactPoint = contactPoint - transform.position;

            Vector3 direction = vectorCenterToContactPoint.normalized;

            direction = isGrounded ? Vector3.ProjectOnPlane(direction, Vector3.up) : direction;

            float distanceToCenter = vectorCenterToContactPoint.magnitude;

            forceToApply = -forceMultiplier * otherComponentPhysicsBehaviours.CalculateForceToApplyToOtherCar(otherCarEnduranceFactor, otherCarWeight, otherCarEnduranceImportance);

            if (!otherComponentPhysicsBehaviours.HasBeenPushed)
            {
                otherComponentPhysicsBehaviours.ApplyForce(direction, contactPoint, forceToApply * (1 - distanceToCenter / pushZoneCollider.radius), forceMode);
                otherComponentPhysicsBehaviours.CarImpactHandler.OnHasBeenPushed(otherComponentPhysicsBehaviours);
            }
        }
        private void Explosion()
        {
            pushZoneCollider.radius = MAX_RADIUS;
            agent.speed = 0f;
            visualModel.gameObject.SetActive(false);
            explosionParticles.SetActive(true);
            TimerSystem.Instance.CreateTimer(EXPLOSION_TIME, onTimerDecreaseComplete: () =>
            {
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
            });
        }
        public void OnPause()
        {
            _isPaused = true;
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }
}