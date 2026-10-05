using System.Collections.Generic;
using UnityEngine;
using Utilities;

public class GolfBallSpawnHandle : MonoBehaviour
{
    private GolfBallBehaviour[] _golfBallBehaviours;

    [SerializeField]
    [Range(10f,500f)]
    private float YSpawnPositionOffset = 10f;

    [SerializeField]
    private float waitTillRelocate = 10f;

    private Dictionary<GolfBallBehaviour,ITimer> onCurrentOutOfBounds;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _golfBallBehaviours = GetComponentsInChildren<GolfBallBehaviour>();
        foreach(GolfBallBehaviour golfBallBehaviour in _golfBallBehaviours)
        {
            golfBallBehaviour.onOutOfBounds.AddListener(SetBallPostion);
            golfBallBehaviour.onCancelOutOfBounds.AddListener(StopRelocateTimer);
        }
        onCurrentOutOfBounds = new Dictionary<GolfBallBehaviour,ITimer>();
    }

    private void SetBallPostion(Vector3 initialPosition,GolfBallBehaviour golfBallBehaviour)
    {
        golfBallBehaviour.Rb.linearVelocity = Vector3.zero;
        Vector3 initialPosWithYOffset =  initialPosition + Vector3.up * YSpawnPositionOffset;
       ITimer relocateTimer = TimerSystem.Instance.CreateTimer(waitTillRelocate, onTimerDecreaseComplete: () =>
        {
            golfBallBehaviour.transform.position = initialPosWithYOffset;
        });
        onCurrentOutOfBounds.Add(golfBallBehaviour,relocateTimer);
    }

    private void StopRelocateTimer(GolfBallBehaviour golfBallBehaviour)
    {
        onCurrentOutOfBounds[golfBallBehaviour].StopTimer();
    }
}
