using UnityEngine;
using Utilities.Managers.PauseSystem;

public class AnimationPausable : MonoBehaviour, IPausable
{
    [SerializeField] private Animator animator;
    private float _originalSpeed;
    void Start()
    {
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();

        }
        PauseManager.TryRegisterPausable(this);
    }

    void OnDestroy()
    {
        if (PauseManager.HasInstance)
        {
            PauseManager.TryUnregisterPausable(this);
        }
    }

    public void OnPause()
    {
        if (animator != null)
        {
            _originalSpeed = animator.speed;
            animator.speed = 0f;  
        }
    }

    public void OnResume()
    {
        if (animator != null)
        {
            animator.speed = _originalSpeed;  
        }
    }
}
