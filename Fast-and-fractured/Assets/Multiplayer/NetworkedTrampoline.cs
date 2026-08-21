using FishNet.Object;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Server-only ParabolicImpulse + RPC-broadcast release animation.</summary>
    public class NetworkedTrampoline : ServerAuthoritativeWrapper<ParabolicImpulse>
    {
        [SerializeField] private TrampolineAnimations _animations;
        [SerializeField] private float _serverCooldown = 0.5f;

        private float _nextServerImpulseAcceptedTime;

        protected override void Awake()
        {
            base.Awake();

            if (_animations == null)
                _animations = GetComponentInChildren<TrampolineAnimations>(true);
            if (_animations == null && transform.parent != null)
                _animations = transform.parent.GetComponentInChildren<TrampolineAnimations>(true);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!base.IsServerInitialized) return;
            if (Time.time < _nextServerImpulseAcceptedTime) return;

            bool isImpulsable = other.GetComponent<PhysicsBehaviour>() != null
                                || other.GetComponent<ICanBeImpulseByTrampoline>() != null;
            if (!isImpulsable) return;

            _nextServerImpulseAcceptedTime = Time.time + _serverCooldown;
            RpcPlayReleaseAnimation(_serverCooldown);
        }

        [ObserversRpc(BufferLast = false)]
        private void RpcPlayReleaseAnimation(float countdown)
        {
            if (_animations == null || _animations.releaseAnimation == null) return;
            _animations.releaseAnimation.Invoke(countdown);
        }
    }
}
