using UnityEngine;
using Utilities;
using Enums;
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
        private bool isRotating = false;
        public bool IsRotating { get => isRotating; }
        private float currentIdleTime = 0f;
        public float minCooldownTime = 8f;
        public float maxCooldownTime = 15f;
        private float currentCooldownTimeUntilNextAction = 0f;
        public float turnSpeed = 20f;
        private Quaternion targetRotation;
        public Pooltype pooltypeMegaChickenEgg;
        public GameObject eggSpawnPoint;

        void Start()
        {

        }

        void Update()
        {
            if (isIdle)
            {
                GetRandomAction();
            }
        }
        public void RotateChicken()
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
            {
                isRotating = false;
                isIdle = true;
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
            if (currentCooldownTimeUntilNextAction == 0f)
            {
                currentCooldownTimeUntilNextAction = Random.Range(minCooldownTime, maxCooldownTime);
            }
            currentIdleTime += Time.deltaTime;
            if (currentIdleTime >= currentCooldownTimeUntilNextAction)
            {
                isIdle = false;
                currentIdleTime = 0f;
                currentCooldownTimeUntilNextAction = 0f;
                float rand = Random.value;
                if (rand < 0.5f)
                {
                    isThrowingEgg = true;
                }
                else if (rand < 0.8f)
                {
                    Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
                    if (randomDirection != Vector3.zero)
                    {
                        Quaternion lookRotation = Quaternion.LookRotation(randomDirection);
                        targetRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, 0);
                        isRotating = true;
                    }
                }
                else
                {
                    isJumping = true;
                }
            }
        }
        public void Jump()
        {
            //todo depends of the way we want the chicken to jump
            isIdle = true;
            isJumping = false;
        }
    }
}