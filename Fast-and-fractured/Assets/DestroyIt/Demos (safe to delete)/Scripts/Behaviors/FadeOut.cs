using System.Collections.Generic;
using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// Attach this script to any object that has a transparent shader on its mesh renderer.
    /// 
    /// Also, you can attach this script to a destroyed prefab that has child "debris" objects. Example: a broken wall object that
    /// is composed of multiple child rubble pieces. This script will fade-out and clean up all the child rubble pieces for you.
    /// </summary>
    public class FadeOut : MonoBehaviour
    {
        [Range(0f, 30f)]
        public float afterSeconds = 6f;   // seconds to wait before starting the fade.
        [Range(0f, 10f)]
        public float fadeLength = 2f; // how long (in seconds) to fade-out objects before destroying them. 

        private List<ObjectToFade> objectsToFade; 
        private float timeLeft;
        private bool isInitialized;
        private bool isBeingDestroyed;

        void Start()
        {
            timeLeft = afterSeconds;
            isInitialized = true;

            MeshRenderer[] meshRenderers = transform.GetComponentsInChildren<MeshRenderer>();
            if (meshRenderers.Length == 0)
            {
                Debug.LogWarning("FadeOut: No MeshRenderers found under \"" + transform.name + "\". Cannot fade out.");
                Destroy(this);
            }
            else
            {
                // Collect all the objects to fade into a list.
                objectsToFade = new List<ObjectToFade>();
                for (int i = 0; i < meshRenderers.Length; i++)
                {
                    objectsToFade.Add(new ObjectToFade()
                    {
                        MeshRenderer = meshRenderers[i],
                        Colliders = meshRenderers[i].GetComponentsInChildren<Collider>(),
                        Rigidbody = meshRenderers[i].GetComponent<Rigidbody>(),
                        CanBeFaded = true
                    });
                }
            }
        }

        void OnEnable()
        {
            timeLeft = afterSeconds;
        }

        void Update()
        {
            if (!isInitialized || isBeingDestroyed) return;
            timeLeft -= Time.deltaTime;

            if (timeLeft <= 0)
            {
                if (timeLeft <= -1*fadeLength)
                {
                    isBeingDestroyed = true;
                    Destroy(transform.gameObject);
                }
                else
                    Fade();
            }
        }

        private void StripColliders(ObjectToFade obj)
        {
            // Try to strip Colliders
            if (obj.Colliders.Length == 0)
                obj.IsStripped = true;
            else
            {
                for (int i=0; i<obj.Colliders.Length; i++)
                    Destroy(obj.Colliders[i]);
                obj.IsStripped = true;
            }   
        }

        /// <summary>Fade this object and its children one step towards invisible.</summary>
        private void Fade()
        {
            foreach (ObjectToFade obj in objectsToFade)
            {
                if (obj.MeshRenderer == null) continue;

                // Try to strip Rigidbody and Colliders
                if (!obj.IsStripped)
                {
                    if (obj.Rigidbody == null)
                    {
                        // No Rigidbody, so try to strip Colliders
                        StripColliders(obj);
                    }
                    else if (obj.Rigidbody.IsSleeping())
                    {
                        // Rigidbody is sleeping, so destroy it and then strip Colliders
                        Destroy(obj.Rigidbody);
                        StripColliders(obj);
                    }
                    // Else Rigidbody isn't sleeping, so leave it alone for now.
                }

                if (!obj.IsTransparencyChecked)
                {
                    Material[] mats = obj.MeshRenderer.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        // If the material on the object doesn't have a transparency property...
                        if (!mats[i].HasProperty("_Transparency"))
                        {
                            // Try to find the appropriate "Transparent" version of its shader.
                            mats[i].shader = mats[i].shader.GetTransparentVersion();
                            mats[i].SetFloat("_Transparency", 0f);
                        }
                    }
                    obj.MeshRenderer.materials = mats;
                    obj.IsTransparencyChecked = true;
                }

                for (int i = 0; i < obj.MeshRenderer.materials.Length; i++)
                {
                    float currTransparency = obj.MeshRenderer.materials[i].GetFloat("_Transparency");
                    if (currTransparency >= 1f)
                        continue;
                    currTransparency += Mathf.Clamp01(Time.deltaTime / fadeLength);
                    obj.MeshRenderer.materials[i].SetFloat("_Transparency", currTransparency);
                }
            }
        }
    }

    public class ObjectToFade
    {
        public MeshRenderer MeshRenderer { get; set; }
        /// <summary>Are the rigidbody and colliders stripped from this object?</summary>
        public bool IsStripped { get; set; }
        public bool CanBeFaded { get; set; }
        public Rigidbody Rigidbody { get; set; }
        public Collider[] Colliders { get; set; }
        public bool IsTransparencyChecked { get; set; }
    }

    public static class ShaderExtensions
    {
        public static Shader GetTransparentVersion(this Shader currentShader)
        {
            Shader transShader;
            // Try to find the appropriate "Transparent" version of the shader.
            if (currentShader.name.Contains("DestroyIt/"))
                transShader = Shader.Find(currentShader.name.Replace("DestroyIt/", "DestroyIt/Transparent"));
            else
                transShader = Shader.Find("DestroyIt/Transparent" + currentShader.name.Replace(" ", ""));
            if (transShader != null)
                return transShader;

            // Transparent version of shader not found. Try to fallback on DestroyIt/TransparentDiffuse.
            transShader = Shader.Find("DestroyIt/TransparentDiffuse");
            if (transShader != null)
                return transShader;

            // if no transparency shader could be found, log an error and destroy this script
            Debug.LogError("DestroyIt: No progressive damage transparency shader could be found. Cannot fade out material with shader \"" + currentShader.name + "\" object.");
            return currentShader;
        }
    }
}