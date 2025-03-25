using UnityEngine;
using Utilities.Managers.PauseSystem;

public class AnimationPausable : MonoBehaviour, IPausable
{
    [SerializeField] private Animator _animator;
    private float _originalSpeed;
    void Start()
    {
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();

        }
        PauseManager.Instance.RegisterPausable(this);
    }

    void OnDestroy()
    {
        PauseManager.Instance.UnregisterPausable(this);
    }

    public void OnPause()
    {
        if (_animator != null)
        {
            _originalSpeed = _animator.speed;
            _animator.speed = 0f;  
        }
    }

    public void OnResume()
    {
        if (_animator != null)
        {
            _animator.speed = _originalSpeed;  
        }
    }
}