using System;
using UnityEngine;

namespace DestroyIt
{
    public static class Check
    {
        public static bool IsDefaultParticleAssigned()
        {
            if (DestructionManager.Instance == null) return false;

            if (DestructionManager.Instance.defaultParticle == null)
            {
                Debug.LogError("DestructionManager: Default Particle is not assigned. You should assign a default particle effect for simple destructible objects.");
                return false;
            }
            return true;
        }

        public static bool LayerExists(string layerName, bool logMessage)
        {
            if (DestructionManager.Instance == null) return false;

            int layer = LayerMask.NameToLayer(layerName);
            if (layer == -1)
            {
                if (logMessage)
                    Debug.LogWarning(String.Format("[DestroyIt Core] Layer \"{0}\" does not exist. Please add a layer named \"{0}\" to your project.", layerName));
                return false;
            }
            return true;
        }
    }
}