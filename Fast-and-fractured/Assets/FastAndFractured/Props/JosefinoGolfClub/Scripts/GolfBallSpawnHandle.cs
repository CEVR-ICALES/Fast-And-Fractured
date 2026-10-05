using UnityEngine;

public class GolfBallSpawnHandle : MonoBehaviour
{
    private GolfBallBehaviour[] _golfBallBehaviours;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _golfBallBehaviours = GetComponentsInChildren<GolfBallBehaviour>();
        foreach(GolfBallBehaviour golfBallBehaviour in _golfBallBehaviours)
        {
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
