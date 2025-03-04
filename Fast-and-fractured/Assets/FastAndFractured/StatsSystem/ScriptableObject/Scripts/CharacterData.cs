using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "CharacterData.asset", menuName = "CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identificators")]
        public int Id;
        public string Name;

        [Header("Game Object")]

        public GameObject Prefab;
        public GameObject Instance;

        [Header("Health")]
        public float MaxResits;
        public bool Dead;
        public delegate void Died();
        public Died OnDied; 

        [Header("Movement")]

        public float MaxSpeed;
        public float MinSpeed;
        public float Acceleration;
        public float BrakeTorque;
        public float Handling;
        //Maybe
        public float DashDistance;


        [Header("Physics")]
        public float Weight;
        public float Traction;
        public float Damping;

        [Header("DamageAndPushing")]

        public float NormalShootDMG;
        public float PushShootDMG;
        public float PushShootFORCE;

        [Header("COOLDOWNS")]

        public float DashCooldown;
        public float PushShootCooldown;
        public float UniqueAbilityCooldown;
        public float NormalShootOverHeat;

    }
}
