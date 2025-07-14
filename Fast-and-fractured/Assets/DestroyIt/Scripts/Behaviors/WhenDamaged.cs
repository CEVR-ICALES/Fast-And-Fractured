using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This is a helper script that listens to a Destructible object's DamagedEvent and runs additional code when the object is damaged.
    /// Put this script on a GameObject that has the Destructible script.
    /// </summary>
    [RequireComponent(typeof(Destructible))]
    public class WhenDamaged : MonoBehaviour
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
            Debug.Log($"{_destObj.name} was damaged for {_destObj.LastDamagedAmount} hit points");
        }
    }
}