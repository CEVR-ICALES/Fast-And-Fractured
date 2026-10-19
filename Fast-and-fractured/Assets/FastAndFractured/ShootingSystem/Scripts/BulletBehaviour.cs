using Enums;
using FastAndFractured.Utilities;
using System;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public abstract class BulletBehaviour : MonoBehaviour, IPooledObject, IEventSimulable
    {

        [SerializeField] protected Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public Vector3 Velocity { set => velocity = value; }
        protected Vector3 velocity;
        public float Range { set => range = value; }
        protected float range;
        public float Damage { set => damage = value; }
        protected float damage;
        protected Vector3 initPosition;

        public bool InitValues => initValues;
        [SerializeField] private bool initValues = true;
        //References
        protected ICustomRigidbody rb;
        protected Collider ownCollider;
        [SerializeField]
        private GameObject visuals;
        [SerializeField] protected GameObject particles;
        [Tooltip("Time delay to allow particles to show up")][SerializeField] private float delayProyectileEnd = 1.5f;

        public event Action<GameObject> OnDespawnRequested;
         
        public virtual void InitializeValues()
        {
            rb = GetComponent<ICustomRigidbody>();
            ownCollider = GetComponentInParent<Collider>();
           
        }
        public virtual void InitBulletTrayectory()
        {
            ownCollider = GetComponentInParent<Collider>();

            if (particles != null)
            {
                particles.SetActive(false);
            }
            if (rb==null)
            {
                rb = GetComponent<ICustomRigidbody>();
            }
            initPosition = transform.position;
            rb.linearVelocity = velocity;
        }

        protected virtual void OnBulletEndTrayectory()
        {
            if (particles != null)
            {
                rb.linearVelocity = Vector3.zero;
                if (!ownCollider)
                {
                    ownCollider = GetComponentInParent<Collider>();
                }
                ownCollider.enabled = false;
                visuals.SetActive(false);
                rb.constraints = RigidbodyConstraints.FreezePosition;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                transform.rotation = new Quaternion(0, 0, 0, 0);
                particles.SetActive(true);
                TimerSystem.Instance.CreateTimer(delayProyectileEnd, onTimerDecreaseComplete: () =>
                {
                    visuals.SetActive(true);
                    ownCollider.enabled = true;
                    rb.constraints = RigidbodyConstraints.None;
                });
            }
            OnDespawnRequested?.Invoke(gameObject); 
        }

       

        public virtual void OnSimulateStart(object[] args)
        {
            InitializeValues();
         }

        public virtual void OnSimulateTick(float deltaTime)
        {
         }

        public virtual void OnSimulateTriggerEnter(Collider other)
        {
         }

        public virtual void OnSimulateCollisionEnter(Collision collision)
        {
         }
    }

}
