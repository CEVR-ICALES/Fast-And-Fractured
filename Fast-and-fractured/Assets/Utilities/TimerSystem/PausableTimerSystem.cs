using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

public class PausableTimerSystem : MonoBehaviour, IPausable
{
    [SerializeField] TimerSystem timerSystem;

    private void Start()
    {
        if (timerSystem == null)
        {
            timerSystem = GetComponent<TimerSystem>();
        }
        PauseManager.Instance.RegisterPausable(this);

    }

    private void OnDestroy()
    {
        PauseManager.Instance?.UnregisterPausable(this);
    }
    public void OnPause()
    {
        timerSystem.OnPause();
    }

    public void OnResume()
    {
        timerSystem.OnResume();
    }


}
