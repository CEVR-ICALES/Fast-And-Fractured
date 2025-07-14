using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Put this script on a GameObject that has the Destructible script.
    /// When this object is damaged, it will also apply that damage to all of its parent Destructible objects.
    /// </summary>
    [RequireComponent(typeof(Destructible))]
    public class WhenDamagedDamageParents : MonoBehaviour
    {
        private Destructible _destObj;

        private void Start()
        {
            // Try to get the Destructible script on the object. If found, attach the OnDamaged event listener to the DamagedEvent.
            _destObj = gameObject.GetComponent<Destructible>();
            if (_destObj != null)
                _destObj.DamagedEvent += OnDamaged;
        }

        private void OnDisable()
        {
            // Unregister the event listener when disabled/destroyed. Very important to prevent memory leaks due to orphaned event listeners!
            if (_destObj == null) return;
            _destObj.DamagedEvent -= OnDamaged;
        }

        /// <summary>When the Destructible object is damaged, the code in this method will run.</summary>
        private void OnDamaged()
        {
            // Get a reference to all the Destructible parents of this object
            Destructible[] destructibleParents = gameObject.GetComponentsInParent<Destructible>();

            // Loop through the destructible parent objects and apply damage
            for (int i = 0; i < destructibleParents.Length; i++)
            {
                if (destructibleParents[i] == _destObj) continue; // Ignore the current destructible object
                destructibleParents[i].ApplyDamage(_destObj.LastDamagedAmount); // Apply the damage to only parent objects
            }
        }
    }
}