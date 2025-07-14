using UnityEngine;
using System.Collections;

namespace DestroyIt
{
    /// <summary>
    /// This script provides extra features to the BulbFlash particle effect.
    /// It removes the "Powered" tag from its parent, and removes the PoweredTag
    /// script which manages the parent's powered state.
    /// </summary>
    public class BulbFlash : MonoBehaviour
    {
        void OnEnable()
        {
            // start a coroutine because we want to wait until the particle effect has had a
            // chance to be a child under something.
            StartCoroutine(RemovePower());
        }

        IEnumerator RemovePower()
        {
            // wait one frame
            yield return 0;

            Transform parent = this.transform.parent;
            if (parent != null)
            {
                parent.gameObject.RemoveTag(Tag.Powered);
                parent.gameObject.RemoveComponent<PoweredTag>();
            }
            StopCoroutine("RemovePower");
        }
    }
}