using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class NormalBulletBehaviour : BulletBehaviour
    {
        public Collider IgnoreCollider { set => _ignoreCollider = value; }
        private Collider _ignoreCollider;
        public override void InitBulletTrayectory()
        {
            base.InitBulletTrayectory();
            if (_ignoreCollider != null)
            {
                if (!Physics.GetIgnoreCollision(_ignoreCollider, ownCollider))
                {
                    Physics.IgnoreCollision(_ignoreCollider, ownCollider);
                }
            }
        }

        protected override void FixedUpdate()
        {
            if ((transform.position - initPosition).magnitude >= range)
            {
                OnBulletEndTrayectory();
            }
        }

        protected override void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<StatsController>(out var statsController))
            {
                statsController.TakeEndurance(damage, false);
                OnBulletEndTrayectory();
            }
            else
                OnBulletEndTrayectory();
        }
    }
}
