using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class NormalBulletBehaivour : BulletBehaivour
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
                OnBulletEndTrayectory(null);
            }
        }

        protected override void OnTriggerEnter(Collider other)
        {
            //Provisional till layer decision
                if (other.TryGetComponent<StatsController>(out var statsController))
                {
                    statsController.TakeEndurance(damage, false);
                    OnBulletEndTrayectory(null);
                }
        }
    }
}
