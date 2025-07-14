using UnityEngine;

namespace DestroyIt
{
    public static class ParticleHelper
    {

        private static Material[] GetNewMaterialsForDestroyedParticle(Renderer destMesh, Destructible destructibleObj)
        {
            if (destructibleObj == null) return null;

            Material[] curMats = destMesh.sharedMaterials;
            Material[] newMats = new Material[curMats.Length];

            // For each of the old materials, try to get the destroyed version.
            for (int i = 0; i < curMats.Length; i++)
            {
                Material currentMat = curMats[i];
                if (currentMat == null) continue;

                // First, see if we need to replace the material with one defined on the Destructible script.
                MaterialMapping matMap = destructibleObj.replaceParticleMats.Find(x => x.SourceMaterial == currentMat);
                newMats[i] = matMap == null ? currentMat : matMap.ReplacementMaterial;

                // If we are using Progressive Damage, try to get a destroyed version of the material.
                if (!destructibleObj.UseProgressiveDamage) continue;
                if (destructibleObj.damageLevels == null || destructibleObj.damageLevels.Count == 0)
                    destructibleObj.damageLevels = DestructibleHelper.DefaultDamageLevels();

                //DestructionManager.Instance.SetProgressiveDamageTexture(destMesh, newMats[i], destructibleObj.damageLevels[destructibleObj.damageLevels.Count - 1]);
            }
            return newMats;
        }
    }
}