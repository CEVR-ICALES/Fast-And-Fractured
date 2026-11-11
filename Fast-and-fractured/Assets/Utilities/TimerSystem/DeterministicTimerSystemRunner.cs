using FishNet.Object;
using UnityEngine;
using Utilities;

public class DeterministicTimerSystemRunner : NetworkBehaviour
{
    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        if (!base.IsServerInitialized && !base.Owner.IsLocalClient)
        {
            enabled = false;
            return;
        }

        base.TimeManager.OnTick += TimeManager_OnTick;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        if (base.TimeManager != null)
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
        }
    }

    private void TimeManager_OnTick()
    {
        TimerSystem.Instance.Tick((float)base.TimeManager.TickDelta);
    }
}

