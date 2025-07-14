using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    public static class DestructibleHelper
    {
        /// <summary>When a Destructible Object is DESTROYED, this script will attempt to find and transfer the appropriate damaged materials over to the new prefab.</summary>
        public static void TransferMaterials(Destructible oldObj, GameObject newObj)
        {
            if (oldObj == null) return;

            Renderer[] oldMeshes = oldObj.GetComponentsInChildren<Renderer>();
            Renderer[] newMeshes = newObj.GetComponentsInChildren<Renderer>();

            // If either object has no meshes, then there's nothing to transfer, so exit.
            if (oldMeshes.Length == 0 || newMeshes.Length == 0) return;

            //TODO: Should this be removed? It has been commented out for a while.
            // If there are no specified materials to replace, then exit, because we will just use the materials already assigned on the destroyed prefab.
	        //if (oldObj.replaceMaterials == null || oldObj.replaceMaterials.Count == 0) return;

            // Get new materials for each destroyed mesh
            for (int i = 0; i < newMeshes.Length; i++)
            {
                if (newMeshes[i] is MeshRenderer || newMeshes[i] is SkinnedMeshRenderer)
                    newMeshes[i].materials = GetNewMaterialsForDestroyedMesh(newMeshes[i], oldObj);
            }
        }

        /// <summary>
        /// For SpeedTree trees, locks the Hue Variation by setting the override property _HueVariationPos.
        /// This way, destroyed trees won't change color as they fall and roll around.
        /// Note that _HueVariationPos is a custom property added by the DestroyItSpeedTree custom shader.
        /// </summary>
        public static void LockHueVariation(this GameObject go)
        {
            if (go == null) return;

            Renderer[] meshes = go.GetComponentsInChildren<Renderer>();
            if (meshes.Length == 0) return;
            for (int i = 0; i < meshes.Length; i++)
            {
                for (int j = 0; j < meshes[i].materials.Length; j++)
                {
                    Material mat = meshes[i].materials[j];
                    if (mat.HasProperty("_HueVariationPos"))
                        mat.SetVector("_HueVariationPos", go.transform.position);
                }
            }
        }

        private static Material[] GetNewMaterialsForDestroyedMesh(Renderer destMesh, Destructible destructibleObj)
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
                MaterialMapping matMap = destructibleObj.replaceMaterials.Find(x => x.SourceMaterial == currentMat);
                newMats[i] = matMap == null ? currentMat : matMap.ReplacementMaterial;

                // If we are using Progressive Damage, try to get a destroyed version of the material.
                if (!destructibleObj.UseProgressiveDamage) continue;
                if (destructibleObj.damageLevels == null || destructibleObj.damageLevels.Count == 0)
                    destructibleObj.damageLevels = DefaultDamageLevels();

                DestructionManager.Instance.SetProgressiveDamageTexture(destMesh, newMats[i], destructibleObj.damageLevels[destructibleObj.damageLevels.Count - 1]);
            }
            return newMats;
        }

        /// <summary>Reapply force to the impact object (if any) so it punches through the destroyed object.</summary>
        public static void ReapplyImpactForce(ImpactDamage info, float velocityReduction)
        {
            if (info.ImpactObject == null || info.ImpactObject.isKinematic) return;
            
            info.ImpactObject.velocity = Vector3.zero; //zero out the velocity
            info.ImpactObject.AddForce(info.ImpactObjectVelocityTo * velocityReduction, ForceMode.Impulse);
        }

        public static List<DamageLevel> DefaultDamageLevels()
        {
            // Initialize with default damage levels if null.
            return new List<DamageLevel>
            {
                new DamageLevel{healthPercent = 100, visibleDamageLevel = 0},
                new DamageLevel{healthPercent = 80, visibleDamageLevel = 2},
                new DamageLevel{healthPercent = 60, visibleDamageLevel = 4},
                new DamageLevel{healthPercent = 40, visibleDamageLevel = 6},
                new DamageLevel{healthPercent = 20, visibleDamageLevel = 8}
            };
        }

        /// <summary>Removes colliders from the object and lets it fall through the terrain.</summary>
        public static void SinkAndDestroy(Destructible destObj)
        {
            // First, turn Kinematic off for all rigidbodies under this object.
            Rigidbody[] rbodies = destObj.GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rbody in rbodies)
            {
                rbody.isKinematic = false;
                rbody.WakeUp();
            }
            // Next, strip off all colliders so it falls through the terrain.
            Collider[] colliders = destObj.GetComponentsInChildren<Collider>();
            foreach (Collider coll in colliders)
                coll.enabled = false;
            // Attach the DestroyAfter script to the object so it will get removed from the game.
            DestroyAfter destAfter = destObj.gameObject.AddComponent<DestroyAfter>();
            destAfter.seconds = 5f;
        }
    }
}