using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
// ReSharper disable IdentifierTypo

namespace DestroyIt
{
    [Serializable]
    public class DamageLevel
    {
        public float maxHitPoints;
        public float minHitPoints;
        public int healthPercent;
        public bool hasError;
        public int visibleDamageLevel;
    }

    /// <summary>A particle effect that is triggered at a specified damage level.</summary>
    [Serializable]
    public class DamageEffect
    {
        public int TriggeredAt;
        public Vector3 Offset;
        public Vector3 Rotation;
        public Vector3 Scale = Vector3.one;
        [FormerlySerializedAs("Effect")]
        public GameObject Prefab;
        public GameObject GameObject;
        public bool HasStarted;
        public bool HasTagDependency;
        public Tag TagDependency;
        public bool UnparentOnDestroy = true;
        public bool StopEmittingOnDestroy;
        public ParticleSystem[] ParticleSystems { get; set; }
    }

    public class Debris
    {
        public Rigidbody Rigidbody { get; set; }
        public GameObject GameObject { get; set; }
        public bool IsActive
        {
            get {return (Rigidbody != null && GameObject.activeSelf);}
        }

        public void Disable()
        {
            if (Rigidbody != null)
                GameObject.SetActive(false);
        }
    }

    public class ExplosiveDamage : Damage
    {
        public float BlastForce { get; set; }
        public Vector3 Position { get; set; }
        public float Radius { get; set; }
        public float UpwardModifier { get; set; }
        public float DamageAmount { get; set; }
    }

    /// <summary>Contains information about the object that collided with a destructible object.</summary>
    public class ImpactDamage : Damage
    {
        /// <summary>The amount of damage done by the impact force.</summary>
        public float DamageAmount { get; set; }

        /// <summary>A reference to the object that collided into the destructible object.</summary>
        public Rigidbody ImpactObject { get; set; }

        public Vector3 ImpactObjectVelocityFrom { get; set; }

        public Vector3 ImpactObjectVelocityTo
        {
            get { return ImpactObjectVelocityFrom * -1; }
        }

        public float AdditionalForce { get; set; }

        public Vector3 AdditionalForcePosition { get; set; }

        public float AdditionalForceRadius { get; set; }
    }

    /// <summary>Direct damage without any impact or force.</summary>
    public class DirectDamage : Damage
    {
        public float DamageAmount { get; set; }
    }

    public class ActiveParticle
    {
        public GameObject GameObject { get; set; }
        public float InstantiatedTime { get; set; }
        public int ParentId { get; set; }
    }

    [Serializable]
    public class DamageMaterial
    {
        public string name;
        public List<Material> damageMaterials;
    }

    [Serializable]
    public class MaterialMapping
    {
        public Material SourceMaterial;
        public Material ReplacementMaterial;
    }

    [Serializable]
    public class PoolEntry
    {
        public GameObject Prefab;
        public int Count;
        public bool OnlyPooled;
    }

    public class TerrainTree
    {
        public int Index { get; set; }
        public Vector3 Position { get; set; }
        public TreeInstance TreeInstance { get; set; }
    }

    [Serializable]
    public class DestructibleTree
    {
        public int prototypeIndex;
        public GameObject Prefab;
    }

    [Serializable]
    public class TreeReset
    {
        public int prototypeIndex;
        public Vector3 position;
        public DateTime resetTime;
        public Color color;
        public float heightScale;
        public float widthScale;
    }
}