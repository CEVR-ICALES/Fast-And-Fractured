using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    public class DynamicDestructible : MonoBehaviour
    {
        public GameObject objectToSpawn;
        public GameObject destroyedPrefab;
        public List<MaterialMapping> materialsToReplace;

        public void Start()
        {
            if (objectToSpawn != null)
            {
                GameObject go = Instantiate(objectToSpawn, transform, false);
                Destructible dest = go.AddComponent<Destructible>();

                if (destroyedPrefab != null)
                {
                    dest.destroyedPrefab = destroyedPrefab;
                    if (materialsToReplace != null && materialsToReplace.Count > 0)
                        dest.replaceMaterials = materialsToReplace;
                }
            }
        }
    }
}