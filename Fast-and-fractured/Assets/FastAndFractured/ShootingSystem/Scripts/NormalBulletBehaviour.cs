using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class NormalBulletBehaviour : BulletBehaviour
    {
        public Collider IgnoreCollider { set => _ignoreCollider = value; }
        private Collider _ignoreCollider;
        private bool _callForDestroy = true;
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
            if (other.TryGetComponent<StatsController>(out var statsController))
            {
                statsController.TakeEndurance(damage, false,ownCollider.gameObject);
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
