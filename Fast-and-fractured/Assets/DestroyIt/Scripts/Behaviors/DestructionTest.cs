using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This script applies damage to all destructible objects in the scene every time you press the "0" key.
    /// This script is for testing purposes.
    /// </summary>
    [DisallowMultipleComponent]
    public class DestructionTest : MonoBehaviour
    {
        public Destructible objectToDestroy;
        public int damagePerPress = 13; // The amount of damage to apply to all destructible objects per keypress.

        public void Update()
        {
            if (Input.GetKeyUp("0"))
            {
                if (objectToDestroy != null)
                    objectToDestroy.ApplyDamage(damagePerPress);
                else
                {
                    Destructible[] destObjs = FindObjectsOfType<Destructible>();
                    foreach (Destructible destObj in destObjs)
                        destObj.ApplyDamage(damagePerPress);
                }
            }
        }
    }
}