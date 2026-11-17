using UnityEngine;

public class PhysicsUpdater : MonoBehaviour
{
    private void FixedUpdate()
    {
        if (Physics.simulationMode == SimulationMode.Script)
            Physics.Simulate(Time.fixedDeltaTime);
    }
}
