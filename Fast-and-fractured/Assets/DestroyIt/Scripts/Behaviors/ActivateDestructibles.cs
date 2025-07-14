using UnityEngine;
// ReSharper disable IdentifierTypo

namespace DestroyIt
{
    public class ActivateDestructibles : MonoBehaviour
    {
        private DestructionManager _destructionManager;
        
        private void Start()
        {
            _destructionManager = DestructionManager.Instance;
            if (_destructionManager == null)
            {
                Debug.LogError("DestructionManager could not be found or created in the scene. Removing script.");
                Destroy(this);
            }
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // Ignore Player objects
            if (other.gameObject.CompareTag("Player")) return;
            
            Destructible destructible = other.gameObject.GetComponentInParent<Destructible>();
            if (destructible == null) return;

            if (destructible.isTerrainTree && _destructionManager.destructibleTreesStayDeactivated) return;

            if (!destructible.enabled)
                destructible.enabled = true;
        }

        private void OnTriggerExit(Collider other)
        {
            // Ignore Player objects
            if (other.gameObject.CompareTag("Player")) return;
            
            Destructible destructible = other.gameObject.GetComponentInParent<Destructible>();
            if (destructible == null) return;

            if (destructible.enabled && !destructible.isTerrainTree && _destructionManager.autoDeactivateDestructibles) // deactivate non-terrain trees
                destructible.shouldDeactivate = true;
            else if (destructible.enabled && destructible.isTerrainTree && _destructionManager.autoDeactivateDestructibleTerrainObjects) // deactivate terrain trees
                destructible.shouldDeactivate = true;
        }
    }
}

