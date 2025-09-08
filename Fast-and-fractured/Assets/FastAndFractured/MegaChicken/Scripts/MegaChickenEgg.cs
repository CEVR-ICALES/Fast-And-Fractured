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
        private bool _initValues = true;
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => _initValues;
        private NavMeshAgent _agent;
        public Transform visualModel;
        private Vector3 _lastPosition;
        private float _radius;
        public Collider ownCollider;
        public SphereCollider pushZoneCollider;
        private Rigidbody _ownRigidbody;
        public GameObject explosionParticles;
        private bool _isPaused = false;
        private float _timeEnabled = 0f;
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
            if (_agent == null)
            {
                _ownRigidbody = GetComponent<Rigidbody>();
                _agent = GetComponent<NavMeshAgent>();
                _agent.updateRotation = false;
                SphereCollider col = GetComponent<SphereCollider>();
                if (col != null)
                {
                    float maxScale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
                    _radius = col.radius * maxScale;
                }
            }
            visualModel.gameObject.SetActive(true);
            _agent.speed = 0f;
            ownCollider.enabled = false;
            _ownRigidbody.isKinematic = true;
            pushZoneCollider.enabled = false;
            explosionParticles.SetActive(false);
            _timeEnabled = 0f;
            GetRandomPoint();
            TimerSystem.Instance.CreateTimer(TIME_UNTIL_REACTIVATE_FISICS, onTimerDecreaseComplete: () =>
            {
                ownCollider.enabled = true;
                _ownRigidbody.isKinematic = false;
                pushZoneCollider.enabled = true;
                pushZoneCollider.radius = NORMAL_RADIUS;
                _agent.speed = AGENT_SPEED;
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
                return;
            }
            if (!_agent.pathPending && _agent.remainingDistance < 1f || _timeEnabled >= maxTimeEnabled)
            {
                Explosion();
            }
            if (visualModel != null && _agent.enabled && _agent.velocity.sqrMagnitude > 0.01f)
            {
                _timeEnabled += Time.deltaTime;
                Vector3 movement = transform.position - _lastPosition;
                _lastPosition = transform.position;

                if (movement.magnitude > 0.0001f)
                {
                    float distance = movement.magnitude;
                    float rotation = (distance / _radius) * Mathf.Rad2Deg;

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
                _agent.SetDestination(hit.position);
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
            _agent.speed = 0f;
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
            _agent.speed = 0;
        }

        public void OnResume()
        {
            _isPaused = false;
            _agent.speed = AGENT_SPEED;
        }
    }
}