using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Progression
{
    public enum RewardType { Cap, Accessory, Title }

    /// <summary>Where an accessory goes on a character.</summary>
    public enum AccessorySlot { Head, Face, Car }

    [Serializable]
    public sealed class ProgressionReward
    {
        [Tooltip("Stable id saved in the player's profile. Never rename one that shipped.")]
        public string id;
        public string displayName;
        [Min(2)] public int level = 2;
        public RewardType type;
        [Tooltip("Accessories and caps: where they go.")]
        public AccessorySlot slot;
        [Tooltip("Character it belongs to (Pepe, Josefino, MariaAntonia, Carme). Empty = any character.")]
        public string character;
        [Tooltip("Model attached to the character. Empty for the test placeholders: they unlock and equip, but show nothing.")]
        public GameObject prefab;
        [Tooltip("Bone or child the model is attached to (searched by name); empty = the character root.")]
        public string attachTo = "Head";
        public Sprite icon;

        public bool FitsCharacter(string characterName) =>
            string.IsNullOrEmpty(character) || string.Equals(character, characterName, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// What each level unlocks (Resources/ProgressionRewards). The entries shipped now are test
    /// placeholders: the system unlocks and equips them, they have no model yet.
    /// </summary>
    [CreateAssetMenu(menuName = "Fast And Fractured/Progression Rewards", fileName = "ProgressionRewards")]
    public sealed class ProgressionRewards : ScriptableObject
    {
        public const string RESOURCES_PATH = "ProgressionRewards";

        public List<ProgressionReward> rewards = new List<ProgressionReward>();

        private static ProgressionRewards _loaded;

        public static ProgressionRewards Load()
        {
            if (_loaded == null) _loaded = Resources.Load<ProgressionRewards>(RESOURCES_PATH);
            if (_loaded == null) _loaded = CreateInstance<ProgressionRewards>();
            return _loaded;
        }

        public ProgressionReward Find(string id)
        {
            foreach (var reward in rewards)
                if (reward != null && reward.id == id) return reward;
            return null;
        }
    }
}
