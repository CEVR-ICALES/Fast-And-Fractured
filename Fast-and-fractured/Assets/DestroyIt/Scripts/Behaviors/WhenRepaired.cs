using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This is a helper script that listens to a Destructible object's RepairedEvent and runs additional code when the object is repaired.
    /// Put this script on a GameObject that has the Destructible script.
    /// </summary>
    [RequireComponent(typeof(Destructible))]
    public class WhenRepaired : MonoBehaviour
    {
        private Destructible _destObj;

        private void Start()
        {
            // Try to get the Destructible script on the object. If found, attach the OnRepaired event listener to the RepairedEvent.
            _destObj = gameObject.GetComponent<Destructible>();
            if (_destObj != null)
                _destObj.RepairedEvent += OnRepaired;
        }

        private void OnDisable()
        {
            // Unregister the event listener when disabled/destroyed. Very important to prevent memory leaks due to orphaned event listeners!
            if (_destObj == null) return;
            _destObj.RepairedEvent -= OnRepaired;
        }

        /// <summary>When the Destructible object is repaired, the code in this method will run.</summary>
        private void OnRepaired()
        {
            Debug.Log($"{_destObj.name} was repaired {_destObj.LastRepairedAmount} hit points");
        }
    }
}