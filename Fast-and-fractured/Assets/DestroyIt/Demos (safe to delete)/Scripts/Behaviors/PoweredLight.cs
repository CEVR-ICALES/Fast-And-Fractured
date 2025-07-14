using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace DestroyIt
{
    /// <summary>
    /// This script checks if an object that contains lights is getting power from its
    /// parent, determined by whether the parent is tagged as "Powered".
    /// </summary>
    public class PoweredLight : MonoBehaviour
    {
        public PowerSource powerSource;         // The source of power for this light.
        public MeshRenderer emissiveMesh;       // Any additional mesh for which to turn off the emissive property of its shader when this light is unpowered. (For example, the lampshade of a light bulb.)
        public Material emissiveOffMaterial;

        private List<Light> lights;
        private Transform parent;
        private bool isPowered;

        void Start()
        {
            isPowered = false;
            lights = gameObject.GetComponentsInChildren<Light>().ToList();
            if (lights.Count == 0)
            {
                Debug.Log("PoweredLight: No Light components found on [" + gameObject.name + "]. Removing script.");
                Destroy(this);
            }
            parent = this.gameObject.transform.parent;
            if (parent == null)
            {
                Debug.Log("PoweredLight: No parent found for [" + gameObject.name + "]. Removing script.");
                Destroy(this);
            }
        }

        void Update()
        {
            // remove any light from the list that is null or missing.
            lights.RemoveAll(x => x == null);

            // find out if we have power from the parent.
            if (parent.gameObject.HasTag(Tag.Powered))
                isPowered = true;
            else
                isPowered = false;

            if (isPowered)
            {
                for (int i = 0; i < lights.Count; i++)
                    lights[i].enabled = true;
            }
            else
            {
                for (int i = 0; i < lights.Count; i++)
                    lights[i].enabled = false;

                // Turn off emissive material
                emissiveMesh.material = emissiveOffMaterial;
            }
        }
    }
}