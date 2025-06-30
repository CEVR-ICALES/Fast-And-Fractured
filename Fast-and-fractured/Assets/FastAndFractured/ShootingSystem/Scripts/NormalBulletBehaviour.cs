using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class NormalBulletBehaviour : BulletBehaviour
    {
        public Collider IgnoreCollider { set => _ignoreCollider = value; }
        private Collider _ignoreCollider;
        private bool _callForDestroy = true;
        private const float SPEED_REDUCTION_MULTIPLIER = 0.8f;
        private const float DISABLED_COLLIDER_DURATION = 0.1f;
        private const string LAYER_SHIELD = "Shield";
        public override void InitBulletTrayectory()
        {
            base.InitBulletTrayectory();
            if (_ignoreCollider != null)
            {
                Physics.IgnoreCollision(_ignoreCollider, ownCollider, true);
            }
            _callForDestroy = true;
        }

        protected override void FixedUpdate()
        {
            if ((transform.position - initPosition).magnitude >= range&&_callForDestroy)
            {
                OnBulletEndTrayectory();
                _callForDestroy=false;
            }
        }

        protected override void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer(LAYER_SHIELD))
            {
                if (_ignoreCollider != null && other.transform.IsChildOf(_ignoreCollider.gameObject.transform))
                {
                    return;
                }
                Vector3 contactPoint = other.ClosestPoint(transform.position);
                Vector3 normal = (contactPoint - other.transform.position).normalized;
                Vector3 incomingDirection = rb.linearVelocity.normalized;
                Vector3 reflectedDirection = Vector3.Reflect(incomingDirection, normal);
                float speed = rb.linearVelocity.magnitude;
                rb.linearVelocity = reflectedDirection * speed * SPEED_REDUCTION_MULTIPLIER;
                ownCollider.enabled = false;
                TimerSystem.Instance.CreateTimer(DISABLED_COLLIDER_DURATION, onTimerDecreaseComplete: () =>
                {
                    ownCollider.enabled = true;
                });
            }
            else if (other.TryGetComponent<StatsController>(out var statsController))
            {
                statsController.TakeEndurance(damage, false, _ignoreCollider.gameObject);
                if (_ignoreCollider != null && _ignoreCollider.TryGetComponent<StatsController>(out var ownerStatsController))
                {
                    ownerStatsController.AddDealtDamage(damage);
                }

                OnBulletEndTrayectory();
            }
            else
                OnBulletEndTrayectory();
        }
        protected override void OnBulletEndTrayectory()
        {
            if (_ignoreCollider != null)
            {
                Physics.IgnoreCollision(_ignoreCollider, ownCollider, false);
            }
            base.OnBulletEndTrayectory();

        }
    }
}
