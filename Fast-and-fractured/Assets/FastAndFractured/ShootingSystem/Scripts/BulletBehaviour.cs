using Enums;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public abstract class BulletBehaviour : MonoBehaviour, IPooledObject
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
        protected Rigidbody rb;
        protected Collider ownCollider;
        [SerializeField]
        private GameObject visuals;
        [SerializeField] protected GameObject particles;
        [Tooltip("Time delay to allow particles to show up")][SerializeField] private float delayProyectileEnd = 1.5f;

        // Update is called once per frame
        protected abstract void FixedUpdate();
        public virtual void InitializeValues()
        {
            rb = GetComponent<Rigidbody>();
            ownCollider = GetComponent<Collider>();
           
        }
        public virtual void InitBulletTrayectory()
        {
            if (particles != null)
            {
                particles.SetActive(false);
            }
            initPosition = transform.position;
            rb.velocity = velocity;
        }

        protected virtual void OnBulletEndTrayectory()
        {
            if (particles != null)
            {
                rb.velocity = Vector3.zero;
                ownCollider.enabled = false;
                visuals.SetActive(false);
                rb.constraints = RigidbodyConstraints.FreezePosition;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                transform.rotation = new Quaternion(0, 0, 0, 0);
                particles.SetActive(true);
                TimerSystem.Instance.CreateTimer(delayProyectileEnd, onTimerDecreaseComplete: () =>
                {
                    visuals.SetActive(true);
                    ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
                    ownCollider.enabled = true;
                    rb.constraints = RigidbodyConstraints.None;
                });
            }
            else
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
        }

        protected virtual void OnTriggerEnter(Collider other)
        {

        }

        protected virtual void OnCollisionEnter(Collision collision)
        {

        }


    }

}
