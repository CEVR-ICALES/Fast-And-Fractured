using UnityEngine;
using UnityEngine.Serialization;
using Utilities.Managers.PauseSystem;

public class PhysicsPausable : MonoBehaviour, IPausable
{
    [SerializeField] private Rigidbody rigidbodyToPause;
    private Vector3 _originalVelocity;
    private Vector3 _originalAngularVelocity;
    bool _wasKinematic;
    void Start()
    {
        if (!rigidbodyToPause)
        {
            rigidbodyToPause = GetComponentInChildren<Rigidbody>();
        }

        PauseManager.Instance.RegisterPausable(this);
    }

    void OnDestroy()
    {
        PauseManager.Instance?.UnregisterPausable(this);
    }

    public void OnPause()
    {
        if (rigidbodyToPause == null){ return;}
        _originalVelocity = rigidbodyToPause.velocity;
        _originalAngularVelocity = rigidbodyToPause.angularVelocity;
        rigidbodyToPause.velocity = Vector3.zero;
        rigidbodyToPause.angularVelocity = Vector3.zero;
        _wasKinematic = rigidbodyToPause.isKinematic;
        rigidbodyToPause.isKinematic = true;
    }

    public void OnResume()
    {
        if (rigidbodyToPause == null){ return;}
        rigidbodyToPause.isKinematic = _wasKinematic; 
        rigidbodyToPause.velocity = _originalVelocity;
        rigidbodyToPause.angularVelocity = _originalAngularVelocity;

    }
}