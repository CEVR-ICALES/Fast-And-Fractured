using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game {
    public class PushBulletBehaviour : BulletBehaivour
    {
        [SerializeField] private LayerMask characterLayers;
        public float PushForce {set=> _pushForce = value; }
        private float _pushForce;
        public float ExplosionRadius { set => _explosionRadius = value; }
        private float _explosionRadius;

        public Vector3 ExplosionCenterOffset { set => _explosionCenterOffset = value; }
        private Vector3 _explosionCenterOffset;
        public int BouncingNum { set => _bouncingNum = value; }
        private int _bouncingNum;
        private int _currentBouncingNum;
        public float BouncingStrenght { set => _bounceStrenght = value; }
        private float _bounceStrenght;
        private float _currentBounceStrenght;
        private Vector3 initialPosition;
        private bool _firstTime = true;
        public Vector3 CustomGravity { set => _customGravity = value; }
        private Vector3 _customGravity;
        private bool _useCustomGravity;
        [SerializeField] private ExplosionForce _explosionHitbox;
        protected override void FixedUpdate()
        {
            if (_useCustomGravity)
            {
                rb.velocity += _customGravity * Time.fixedDeltaTime;
            }
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
            _explosionHitbox.ActivateExplosionHitbox(_explosionRadius,_pushForce,_explosionCenterOffset);
            OnBulletEndTrayectory();
        }

        private void BouncingHandle(Collision collision)
        { 
                if (_bouncingNum > _currentBouncingNum)
                {
                    _currentBouncingNum++;
                    _currentBounceStrenght = _bounceStrenght * (1f - (_currentBouncingNum / _bouncingNum));
                    var newDirection = collision.GetContact(0).normal * _currentBounceStrenght;
                    rb.velocity += newDirection;
                    if (_firstTime)
                    {
                        Debug.Log(Vector3.Distance(transform.position, initialPosition));
                        _firstTime = false;
                    }
                }
                if (_currentBouncingNum >= _bouncingNum)
                {
                    rb.velocity = Vector3.zero;
                    Explosion();
                }
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
        public override void InitializeValues()
        {
            base.InitializeValues();
            _explosionHitbox.ExplosionCollider = _explosionHitbox.GetComponent<SphereCollider>();
        }

        protected override void OnBulletEndTrayectory()
        {
            _useCustomGravity = false;
            base.OnBulletEndTrayectory();
        }

        // Start is called before the first frame update
        void Start()
        {
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
