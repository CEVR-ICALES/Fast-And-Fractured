using System;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    /// <summary>
    /// Recolours a win/lose cutscene for the player's skin: every renderer swaps the skin's listed materials.
    /// Skin 0 (or a skin with no entry) keeps the prefab's own materials.
    /// </summary>
    public class FinalAnimationSkins : MonoBehaviour
    {
        [Serializable]
        public class MaterialSwap
        {
            public Material original;
            public Material replacement;
        }

        [Serializable]
        public class SkinMaterials
        {
            public int skin;
            public List<MaterialSwap> swaps = new List<MaterialSwap>();
        }

        [SerializeField] private List<SkinMaterials> skins = new List<SkinMaterials>();

        public List<SkinMaterials> Skins => skins;

        public void Apply(int skin)
        {
            SkinMaterials entry = skins.Find(s => s.skin == skin);
            if (entry == null || entry.swaps.Count == 0) return;

            var replacements = new Dictionary<Material, Material>();
            foreach (MaterialSwap swap in entry.swaps)
                if (swap.original != null && swap.replacement != null)
                    replacements[swap.original] = swap.replacement;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && replacements.TryGetValue(materials[i], out Material replacement))
                    {
                        materials[i] = replacement;
                        changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = materials;
            }
        }
    }
}
