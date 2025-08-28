using UnityEngine;
using Utilities;
using Enums;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class ChickenBrain : MonoBehaviour, IPausable
    {
        private bool isIdle = true;
        public bool IsIdle { get => isIdle; }
        private bool isJumping = false;
        public bool IsJumping { get => isJumping; }
        private bool isLayingEgg = false;
        public bool IsLayingEgg { get => isLayingEgg; }
        private float currentIdleTime = 0f;
        public float cooldownTime = 15f;
        [SerializeField, Range(0f, 1f)] private float chanceOfEgg = 0.6f;
        public Pooltype pooltypeMegaChickenEgg;
        public GameObject eggSpawnPoint;
        public Animator Animator { get => animator; }
        private Animator animator;
        private bool _isPaused = false;
        private bool hasEggBeenLayed = false;
        private const float ANIMATION_TIME_UNTIL_CHANGE_TO_IDLE = 0.95f;
        private const float ANIMATION_TIME_UNTIL_LAYING_EGG = 0.5f;
        private const string JUMP_ANIMATION_NAME = "Jump";
        private const string EGG_ANIMATION_NAME = "LayEgg";
        private const string BOOL_JUMPING_NAME = "IsJumping";
        private const string BOOL_EGG_NAME = "IsLayingEgg";

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }
        void Start()
        {
            animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (_isPaused)
            {
                animator.speed = 0;
                return;
            }
            else
            {
                animator.speed = 1;
            }
            if (isIdle)
            {
                GetRandomAction();
            }
        }

        
        public void GetRandomAction()
        {
            currentIdleTime += Time.deltaTime;
            if (currentIdleTime >= cooldownTime)
            {
                isIdle = false;
                currentIdleTime = 0f;
                float rand = Random.value;
                if (rand < chanceOfEgg)
                {
                    isLayingEgg = true;
                    hasEggBeenLayed = false;
                }
                else
                {
                    isJumping = true;
                }
            }
        }
        public void ThrowEgg()
        {
            animator.SetBool(BOOL_EGG_NAME, true);
        }
        public void CheckIfLayingEggEnded()
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (isLayingEgg && stateInfo.IsName(EGG_ANIMATION_NAME))
            {
                if (stateInfo.normalizedTime >= ANIMATION_TIME_UNTIL_LAYING_EGG && !hasEggBeenLayed)
                {
                    hasEggBeenLayed = true;
                    GameObject egg = ObjectPoolManager.Instance.GivePooledObject(pooltypeMegaChickenEgg);
                    egg.transform.position = eggSpawnPoint.transform.position;
                    egg.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                if (stateInfo.normalizedTime >= ANIMATION_TIME_UNTIL_CHANGE_TO_IDLE)
                {
                    isLayingEgg = false;
                    isIdle = true;
                    animator.SetBool(BOOL_EGG_NAME, false);
                }
            }
        }
        public void Jump()
        {
            animator.SetBool(BOOL_JUMPING_NAME, true);
        }
        public void CheckIfJumpEnded()
        {
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (isJumping && stateInfo.IsName(JUMP_ANIMATION_NAME))
            {
                if (stateInfo.normalizedTime >= ANIMATION_TIME_UNTIL_CHANGE_TO_IDLE)
                {
                    isJumping = false;
                    isIdle = true;
                    animator.SetBool(BOOL_JUMPING_NAME, false);
                }
            }
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