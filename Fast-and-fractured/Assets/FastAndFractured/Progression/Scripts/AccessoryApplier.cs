using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured.Progression
{
    /// <summary>
    /// Puts the equipped accessories on a model of a character - any model of it (the car in a
    /// match, the character in the menu's bar or selector). For each slot: the equipped reward's
    /// prefab, attached to the child named by the reward (e.g. "Head"), replacing what that slot
    /// had. The test rewards have no prefab, so for now they equip without showing anything.
    /// </summary>
    public static class AccessoryApplier
    {
        private const string ATTACHED_PREFIX = "[Accessory] ";

        /// <summary>Applies the LOCAL player's loadout for <paramref name="characterName"/>.</summary>
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
