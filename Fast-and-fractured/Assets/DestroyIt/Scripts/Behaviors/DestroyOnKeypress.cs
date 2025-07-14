using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace DestroyIt
{
    /// <summary>
    /// Put this script on a game object in your scene, and it will destroy Destructible object when you press the associated key.
    /// </summary>
    public class DestroyOnKeypress : MonoBehaviour
    {
        public float force = 500f;
        public float radius = 10f;
        public float upwardModifier = -1;
        public ObjectToDestroy[] objectsToDestroy;

        void Update()
        {
            // Look for keys that may have been pressed.
            for (int i = 0; i < objectsToDestroy.Length; i++)
            {
                if (string.IsNullOrEmpty(objectsToDestroy[i].key)) continue;

                // Get the keycode for each keypress we want to look for
                KeyCode key = (KeyCode)Enum.Parse(typeof(KeyCode), objectsToDestroy[i].key.ToUpper());

                // If the keypress matches the keycode configured, try to destroy the destructible objects assigned to it
                if (Input.GetKeyUp(key))
                {
                    // For each destructible object assigned to this keycode...
                    Destructible[] destObjs = objectsToDestroy[i].destructibles;
                    for (int j = 0; j < destObjs.Length; j++)
                    {
                        if (destObjs[j] == null) continue;

                        // If there is a collider on the destructible object, capture the tallest point on the collider. We'll use it to apply force so the object won't be stationary once destroyed.
                        Collider coll = destObjs[j].GetComponentInChildren<Collider>();
                        if (coll != null)
                        {
                            Vector3 pos = destObjs[j].transform.position;
                            Vector3 tallestPoint = coll.ClosestPoint(new Vector3(pos.x, pos.y+5000f, pos.z));

                            // Apply the force to a random offset spot around the tallest point of the collider. This will make it so the force is a little unpredictable.
                            float randomX = Random.Range(1, 4);
                            if (Random.Range(0, 2) == 1) randomX *= -1;
                            float randomZ = Random.Range(1, 4);
                            if (Random.Range(0, 2) == 1) randomZ *= -1;
                            Vector3 forcePoint = new Vector3(tallestPoint.x + randomX, tallestPoint.y, tallestPoint.z + randomZ);

                            // Apply the damage, with random force added
                            destObjs[j].ApplyDamage(new ExplosiveDamage {
                                BlastForce = force, DamageAmount = destObjs[j].CurrentHitPoints + 1,
                                Position = forcePoint, Radius = radius, UpwardModifier = upwardModifier
                            });
                        }
                        else // If there is no collider on the Destructible object, just destroy it
                            destObjs[j].Destroy();
                    }
                }
            }
        }
    }

    [Serializable]
    public class ObjectToDestroy
    {
        public string key;
        public Destructible[] destructibles;
    }
}