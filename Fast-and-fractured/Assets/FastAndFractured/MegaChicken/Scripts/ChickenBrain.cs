using UnityEngine;

public class ChickenBrain : MonoBehaviour
{
    private bool isIdle = true;
    public bool IsIdle { get => isIdle; set => isIdle = value; }
    private bool isJumping = false;
    public bool IsJumping { get => isJumping; set => isJumping = value; }
    private bool isThrowingEgg = false;
    public bool IsThrowingEgg { get => isThrowingEgg; set => isThrowingEgg = value; }
    private float currentIdleTime = 0f;
    public float minCooldownTime = 5f;
    public float maxCooldownTime = 10f;
    private float currentCooldownTimeUntilNextAction = 0f;
    private int currentEggsInRow = 0;
    public int minEggsInRow = 3;
    public int maxEggsInRow = 5;
    private int currentMaxEggsInRow = 0;

    [Header("Stats")]
    public float turnSpeed = 5f;
    private Quaternion targetRotation;
    private bool isRotating = false;

    void Start()
    {

    }

    void Update()
    {
        if (isRotating)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * 100 * Time.deltaTime);
            if (Quaternion.Angle(transform.rotation, targetRotation) < 1f)
            {
                isRotating = false;
                isThrowingEgg = false;
                isIdle = true;
                ThrowEgg();
            }
            return;
        }
        if (isIdle)
        {
            if (currentCooldownTimeUntilNextAction == 0f)
            {
                currentCooldownTimeUntilNextAction = Random.Range(minCooldownTime, maxCooldownTime);
            }
            if (currentMaxEggsInRow == 0)
            {
                currentMaxEggsInRow = Random.Range(minEggsInRow, maxEggsInRow);
            }
            currentIdleTime += Time.deltaTime;
            if (currentIdleTime >= currentCooldownTimeUntilNextAction)
            {
                isIdle = false;
                currentIdleTime = 0f;
                currentCooldownTimeUntilNextAction = 0f;
                if (currentEggsInRow < currentMaxEggsInRow)
                {
                    isThrowingEgg = true;
                    currentEggsInRow++;
                }
                else
                {
                    isJumping = true;
                    currentEggsInRow = 0;
                    currentMaxEggsInRow = 0;
                }
            }
        }
    }
    public void StartThrowing()
    {
        Vector3 randomDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f));
        if (randomDirection != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(randomDirection);
            targetRotation = Quaternion.Euler(0, lookRotation.eulerAngles.y, 0);
            isRotating = true;
        }
    }
    private void ThrowEgg()
    {
        isIdle = true;
        isThrowingEgg = false;
    }
}
