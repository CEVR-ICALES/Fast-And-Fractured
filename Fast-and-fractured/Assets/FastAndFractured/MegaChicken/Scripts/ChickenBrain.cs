using UnityEngine;
using Utilities;
using Enums;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class ChickenBrain : MonoBehaviour, IPausable
    {
        private bool _isIdle = true;
        public bool IsIdle { get => _isIdle; }
        private bool _isJumping = false;
        public bool IsJumping { get => _isJumping; }
        private bool _isLayingEgg = false;
        public bool IsLayingEgg { get => _isLayingEgg; }
        private float _currentIdleTime = 0f;
        public float cooldownTime = 15f;
        [SerializeField, Range(0f, 1f)] private float chanceOfEgg = 0.6f;
        public Pooltype pooltypeMegaChickenEgg;
        public GameObject eggSpawnPoint;
        public Animator Animator { get => _animator; }
        private Animator _animator;
        private bool _isPaused = false;
        private bool _hasEggBeenLayed = false;
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
            _animator = GetComponent<Animator>();
        }

        void Update()
        {
            if (_isPaused)
            {
                return;
            }
            if (_isIdle)
            {
                GetRandomAction();
            }
        }

        
        public void GetRandomAction()
        {
            _currentIdleTime += Time.deltaTime;
            if (_currentIdleTime >= cooldownTime)
            {
                _isIdle = false;
                _currentIdleTime = 0f;
                float rand = Random.value;
                if (rand < chanceOfEgg)
                {
                    _isLayingEgg = true;
                    _hasEggBeenLayed = false;
                }
                else
                {
                    _isJumping = true;
                }
            }
        }
        public void ThrowEgg()
        {
            _animator.SetBool(BOOL_EGG_NAME, true);
        }
        public void CheckIfLayingEggEnded()
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (_isLayingEgg && stateInfo.IsName(EGG_ANIMATION_NAME))
            {
                if (stateInfo.normalizedTime >= ANIMATION_TIME_UNTIL_LAYING_EGG && !_hasEggBeenLayed)
                {
                    _hasEggBeenLayed = true;
                    GameObject egg = ObjectPoolManager.Instance.GivePooledObject(pooltypeMegaChickenEgg);
                    egg.transform.position = eggSpawnPoint.transform.position;
                    egg.transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                if (stateInfo.normalizedTime >= ANIMATION_TIME_UNTIL_CHANGE_TO_IDLE)
                {
                    _isLayingEgg = false;
                    _isIdle = true;
                    _animator.SetBool(BOOL_EGG_NAME, false);
                }
            }
        }
        public void Jump()
        {
            _animator.SetBool(BOOL_JUMPING_NAME, true);
        }
        public void CheckIfJumpEnded()
        {
            AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            if (_isJumping && stateInfo.IsName(JUMP_ANIMATION_NAME))
            {
                if (stateInfo.normalizedTime >= ANIMATION_TIME_UNTIL_CHANGE_TO_IDLE)
                {
                    _isJumping = false;
                    _isIdle = true;
                    _animator.SetBool(BOOL_JUMPING_NAME, false);
                }
            }
        }
        public void OnPause()
        {
            _isPaused = true;
            _animator.speed = 0;
        }

        public void OnResume()
        {
            _isPaused = false;
            _animator.speed = 1;
        }
    }
}