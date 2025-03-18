using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public class NormalBulletBehaivour : BulletBehaivour
    {
        public Collider[] IgnoreColliders { set => _ignoreColliders = value; }
        private Collider[] _ignoreColliders;
        private Collider ownCollider;
        private bool _ignoreColliderDone = false;

        protected override void Start()
        {
            ownCollider = GetComponent<Collider>();
        }
        public override void InitBulletTrayectory()
        {
            base.InitBulletTrayectory();
            if (!_ignoreColliderDone)
            {
                foreach (var collider in _ignoreColliders)
                {
                    Physics.IgnoreCollision(ownCollider, collider, true);
                }
                _ignoreColliderDone = true;
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
            //Provisional till layer decision
                if (other.TryGetComponent<StatsController>(out var statsController))
                {
                    statsController.TakeEndurance(damage, false);
                    ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
                }
        }
    }
}
