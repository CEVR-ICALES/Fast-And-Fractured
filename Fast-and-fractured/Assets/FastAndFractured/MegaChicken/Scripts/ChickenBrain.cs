using UnityEngine;
using Utilities;
using Enums;
using FastAndFractured;
namespace FastAndFractured
{
    public class ChickenBrain : MonoBehaviour
    {
        private bool isIdle = true;
        public bool IsIdle { get => isIdle; }
        private bool isJumping = false;
        public bool IsJumping { get => isJumping; }
        private bool isThrowingEgg = false;
        public bool IsThrowingEgg { get => isThrowingEgg; }
        private float currentIdleTime = 0f;
        public float cooldownTime = 15f;
        public Pooltype pooltypeMegaChickenEgg;
        public GameObject eggSpawnPoint;
        private Animator animator;
        private Collider jumpCollider;

        void Start()
        {
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (isIdle)
            {
                GetRandomAction();
            }
        }

        public void ThrowEgg()
        {
            GameObject egg = ObjectPoolManager.Instance.GivePooledObject(pooltypeMegaChickenEgg);
            egg.transform.position = eggSpawnPoint.transform.position;
            egg.transform.rotation = Quaternion.Euler(0, 0, 0);

            isIdle = true;
            isThrowingEgg = false;
        }
        public void GetRandomAction()
        {
            currentIdleTime += Time.deltaTime;
            if (currentIdleTime >= cooldownTime)
            {
                isIdle = false;
                currentIdleTime = 0f;
                float rand = Random.value;
                if (rand < 0.1f)
                {
                    isThrowingEgg = true;
                }
                else
                {
                    isJumping = true;
                }
            }
        }
        public void Jump()
        {
            animator.SetBool("IsJumping", true);
        }
        public void CheckIfJumpEnded()
        {
            // AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            // if (isJumping && stateInfo.IsName("Jump"))
            // {
            //     // Si la animación está por terminar (por ejemplo, normalizado > 0.95)
            //     if (stateInfo.normalizedTime >= 0.95f)
            //     {
            isJumping = false;
            isIdle = true;
            animator.SetBool("IsJumping", false);
            //     }
            // }
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (isJumping)
            {
                foreach (ContactPoint contact in collision.contacts)
                {
                    if (contact.normal.y > 0.5f)
                    {
                        if (contact.otherCollider.TryGetComponent<StatsController>(out StatsController statsController))
                        {
                            statsController.Dead();
                        }
                    }
                }   
            }
        }
    }
}