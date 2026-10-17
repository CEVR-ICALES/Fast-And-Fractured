using UnityEngine;

public class PhysicsUpdater : MonoBehaviour
{
    private void FixedUpdate()
    {
        // FishNet's TimeManager switches the simulation mode at runtime; outside Script mode
        // Unity already steps physics and Simulate only logs a warning every step.
        if (Physics.simulationMode == SimulationMode.Script)
            Physics.Simulate(Time.fixedDeltaTime);
    }
}
