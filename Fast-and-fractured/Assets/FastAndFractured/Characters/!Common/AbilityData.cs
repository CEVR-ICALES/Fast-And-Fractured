using FMODUnity;
using UnityEngine;

namespace Game
{
    [CreateAssetMenu(fileName = "NewAbilityData", menuName = "Abilities/Ability Data")]
    public class AbilityData : ScriptableObject
    {
        [Header("Base Ability Settings")] 
        [SerializeField] private string abilityName = "Unnamed Ability";

        [SerializeField] [TextArea] private string abilityDescription = "Ability Description";
        [SerializeField] private float duration = 5f;
        [SerializeField] private EventReference fMODEventPath;
        [SerializeField] private GameObject uniqueUIPrefab;
        [SerializeField] private GameObject particleEffectPrefab;
        [SerializeField] private string uniqueSentence = "Toma pa tus gastos!";
        [Header("Cooldown Settings")]  
        [SerializeField] private float cooldownDuration = 2f;
        public string AbilityName => abilityName;
        public string AbilityDescription => abilityDescription;
        public float Duration => duration;
        public EventReference FMODEventPath => fMODEventPath;
        public GameObject UniqueUIPrefab => uniqueUIPrefab;
        public GameObject ParticleEffectPrefab => particleEffectPrefab;
        public string UniqueSentence => uniqueSentence;
        
        public float CooldownDuration => cooldownDuration;
    }
}