using Enums; 
using UnityEngine;

public struct BulletSpawnParameters
    {
        public Pooltype Pooltype;
        public Vector3 Velocity;
        public float Range;
        public float Damage;
        public Transform ShootPoint;
        public Collider IgnoredCollider;
        
         public float PushForce;
        public float ExplosionRadius;
        public Vector3 ExplosionCenterOffset;
        public Vector3 CustomGravity;
        public int BouncingNum;
        public float BouncingStrenght;
        public float TimeToExplode;
        public bool IsMine;

        // Car that fired (dev, 2026-10): explosions credit it as the last one who pushed the victim.
        // Local only - never sent over the network.
        public GameObject Author;
    }
    
