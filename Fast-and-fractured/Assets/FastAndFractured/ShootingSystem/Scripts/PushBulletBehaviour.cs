using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utilities;
using Enums;

namespace FastAndFractured {
    public class PushBulletBehaviour : BulletBehaviour
    {
        //Explosion Values
        public float PushForce {set=> _pushForce = value; }
        private float _pushForce;
        public float ExplosionRadius { set => _explosionRadius = value; }
        private float _explosionRadius;

        public Vector3 ExplosionCenterOffset { set => _explosionCenterOffset = value; }
        private Vector3 _explosionCenterOffset;
        [SerializeField] private ExplosionForce _explosionHitbox;
        public float TimeToExplode {set => _timeToExplode = value; }
        private float _timeToExplode = 0;

        //Movement Values

        public int BouncingNum { set => _bouncingNum = value; }
        private int _bouncingNum = 0;
        private int _currentBouncingNum;
        public float BouncingStrenght { set => _bounceStrenght = value; }
        private float _bounceStrenght = 0;
        private float _currentBounceStrenght;
        private Vector3 initialPosition;
        private bool _firstTime = true;
        public Vector3 CustomGravity { set => _customGravity = value; }
        private Vector3 _customGravity;
        private bool _useCustomGravity;

        [SerializeField] private LayerMask characterLayers;

        protected override void FixedUpdate()
        {
            if (_useCustomGravity)
            {
                rb.linearVelocity += _customGravity * Time.fixedDeltaTime;
            }
        }

        public override void InitializeValues()
        {
            base.InitializeValues();
            _explosionHitbox.ExplosionCollider = _explosionHitbox.GetComponent<SphereCollider>();
        }

        public override void InitBulletTrayectory()
        {
            base.InitBulletTrayectory();
            initialPosition = transform.position;
            _currentBounceStrenght = _bounceStrenght;
            _currentBouncingNum = 0;
            _useCustomGravity = true;
            _explosionHitbox.DesactivateExplostionHitbox();
        }


        protected override void OnCollisionEnter(Collision collision)
        {
            if (!((characterLayers & 1 << collision.gameObject.layer) == 1 << collision.gameObject.layer))
            {
                BouncingHandle(collision);
            }
            else
            {
                Explosion();
            }
        }

        private void Explosion()
        {
            _explosionHitbox.ActivateExplosionHitbox(_explosionRadius, _pushForce, _explosionCenterOffset);
            OnBulletEndTrayectory();
        }

        private void BouncingHandle(Collision collision)
        {
            if (_bouncingNum > _currentBouncingNum)
            {
                _currentBouncingNum++;
                _currentBounceStrenght = _bounceStrenght * (1f - (_currentBouncingNum / _bouncingNum));
                var newDirection = collision.GetContact(0).normal * _currentBounceStrenght;
                rb.linearVelocity += newDirection;
                if (_firstTime)
                {
                    Debug.Log(Vector3.Distance(transform.position, initialPosition));
                    _firstTime = false;
                }
            }
            if (_currentBouncingNum >= _bouncingNum)
            {
                rb.linearVelocity = Vector3.zero;
                rb.constraints = RigidbodyConstraints.FreezePosition;
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                if (_timeToExplode > 0)
                {
                    TimerSystem.Instance.CreateTimer(_timeToExplode, TimerDirection.INCREASE, onTimerIncreaseComplete: () =>
                    {
                        Explosion();
                    });
                }
                else
                {
                    Explosion();
                }
            }
        }

        protected override void OnBulletEndTrayectory()
        {
            _useCustomGravity = false;
            base.OnBulletEndTrayectory();
        }
    }
}
