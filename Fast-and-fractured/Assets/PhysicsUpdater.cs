using UnityEngine;

public class PhysicsUpdater : MonoBehaviour
{
    private void FixedUpdate()
    {
        Physics.Simulate(Time.fixedDeltaTime);
    }
}
