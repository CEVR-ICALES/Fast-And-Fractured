using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Progression
{
    /// <summary>Puts the equipped accessories on any model of a character, each on the child its reward names.</summary>
    public static class AccessoryApplier
    {
        private const string ATTACHED_PREFIX = "[Accessory] ";

        public static void ApplyLocalLoadout(GameObject characterRoot, string characterName)
        {
            if (characterRoot == null || string.IsNullOrEmpty(characterName)) return;
            var catalog = ProgressionRewards.Load();
            foreach (AccessorySlot slot in System.Enum.GetValues(typeof(AccessorySlot)))
            {
                var reward = catalog.Find(PlayerProgression.Equipped(characterName, slot));
                Apply(characterRoot, slot, reward);
            }
        }

        public static void Apply(GameObject characterRoot, AccessorySlot slot, ProgressionReward reward)
        {
            string marker = ATTACHED_PREFIX + slot;
            foreach (var t in new List<Transform>(characterRoot.GetComponentsInChildren<Transform>(true)))
                if (t != null && t.name == marker) Object.Destroy(t.gameObject);
            if (reward == null || reward.prefab == null) return;   // placeholder: nothing to show yet

            Transform parent = FindChild(characterRoot.transform, reward.attachTo) ?? characterRoot.transform;
            var instance = Object.Instantiate(reward.prefab, parent, false);
            instance.name = marker;
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (string.Equals(t.name, name, System.StringComparison.OrdinalIgnoreCase)) return t;
            return null;
        }
    }
}
