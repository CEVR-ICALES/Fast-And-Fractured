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
       [SerializeField] private ParticleSystem _explosionFlash;
       [SerializeField] private SphereCollider _explosionCollider;
        [SerializeField] private float _explosionRadius;
        protected override void FixedUpdate()
        {
            if (_useCustomGravity)
            {
                rb.velocity += _customGravity * Time.fixedDeltaTime;
            }
        }

        protected override void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<StatsController>(out var statsController))
            {
                float oCarWeight = statsController.Weight;
                float oCarEnduranceFactor = statsController.Endurance;
                float force = _pushForce * (1 - oCarEnduranceFactor) * (oCarWeight / 20);
                //other.GetComponent<Rigidbody>().AddForce();
            }
        }

        protected override void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer != characterLayers)
            {
                BouncingHandle(collision);
            }
            else
                Explosion();
        }

        private void Explosion()
        {
            //OnBulletEndTrayectory(() => {
            //    _explosionCollider.radius += _explosionRadius * _explosionFlash.sizeOverLifetime.xMultiplier;
            //});
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
            _currentBounceStrenght = _bounceStrenght;
            _currentBouncingNum = 0;
            _useCustomGravity = true;
        }

        protected override void OnBulletEndTrayectory(Action<float> action)
        {
            _useCustomGravity = false;
            base.OnBulletEndTrayectory(action);
        }

        // Start is called before the first frame update
        void Start()
        {
            initialPosition = transform.position;
        }

        // Update is called once per frame
        void Update()
        {

        }
    }
}
