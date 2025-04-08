using UnityEngine;
using UnityEngine.Serialization;
using Utilities.Managers.PauseSystem;

public class ParticleSystemPausable : MonoBehaviour, IPausable
{
   [SerializeField] private ParticleSystem[] particleSystems;

    void Start()
    {
        if (particleSystems.Length == 0)
        {
            particleSystems = GetComponentsInChildren<ParticleSystem>();
        }

        PauseManager.Instance.RegisterPausable(this);
    }

    void OnDestroy()
    {
        PauseManager.Instance?.UnregisterPausable(this);
    }

    public void OnPause()
    {
        foreach (var ps in particleSystems)
        {
            ps.Pause();
        }
    }

    public void OnResume()
    {
        foreach (var ps in particleSystems)
        {
            ps.Play();
        }
    }
}