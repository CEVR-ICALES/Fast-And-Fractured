using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// ReSharper disable ForCanBeConvertedToForeach

namespace DestroyIt
{
    public static class GameObjectExtensions
    {
        /// <summary>Removes all components of type T from the game object and its children.</summary>
        public static void RemoveAllFromChildren<T>(this GameObject obj) where T : Component
        {
            if (obj == null) return;
            foreach (T comp in obj.GetComponentsInChildren<T>())
                Object.Destroy(comp);
        }

        public static void RemoveComponent<T>(this GameObject obj) where T : Component
        {
            if (obj == null) return;
            T component = obj.GetComponent<T>();

            if (component != null)
                Object.Destroy(component);
        }

        public static List<T> GetComponentsInChildrenOnly<T>(this GameObject obj) where T : Component
        {
            return GetComponentsInChildrenOnly<T>(obj, false);
        }

        public static List<T> GetComponentsInChildrenOnly<T>(this GameObject obj, bool includeInactive) where T : Component
        {
            var components = obj.GetComponentsInChildren<T>(includeInactive).ToList();
            components.Remove(obj.GetComponent<T>());
            return components;
        }

        /// <summary>Be sure to set SolverIterationCount to around 25-30 in your Project Settings in order to get solid joints.</summary>
        public static void AddStiffJoint(this GameObject go, Rigidbody connectedBody, Vector3 anchorPosition, Vector3 axis, float breakForce, float breakTorque)
        {
            FixedJoint joint = go.AddComponent<FixedJoint>();
            joint.anchor = anchorPosition;
            joint.connectedBody = connectedBody;
            joint.breakForce = breakForce;
            joint.breakTorque = breakTorque;
        }

        /// <summary>Attempts to get the center point location of a game object's combined meshes.</summary>
        /// <example>If your gameobject has multiple meshes under it which together make up a car (wheels, body, etc), this function will attempt to find
        /// the centerpoint of the car, taking into account all of the child meshes.</example>
        /// <param name="go">The gameobject parent containing the meshes.</param>
        /// <param name="meshRenderers">Pass in the collection of mesh renderers on this game object (including children) to save on performance.</param>
        /// <returns>The centerpoint location of the gameobject's meshes.</returns>
        public static Vector3 GetMeshCenterPoint(this GameObject go, MeshRenderer[] meshRenderers = null)
        {
            // first, get all the mesh renderers on the game object and children if they are not provided
            if (meshRenderers == null)
                meshRenderers = go.GetComponentsInChildren<MeshRenderer>();
            
            // if there are no mesh renderers, return a zero vector (the gameobject's pivot position).
            if (meshRenderers.Length == 0)
                return Vector3.zero;

            // if any mesh renderer on this game object is marked as Static, return a zero vector (the gameobject's pivot position) instead
            // of trying to get the bounding boxes of static meshes.
            if (go.IsAnyMeshPartOfStaticBatch(meshRenderers))
                return Vector3.zero;

            // if we made it this far, calculate the center point of the combined mesh bounds for the object and use that.
            Bounds combinedBounds = new Bounds();

            MeshFilter[] meshFilters = go.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter meshFilter in meshFilters)
            {
                Mesh sharedMesh = meshFilter.sharedMesh;
                if (sharedMesh != null) // some meshFilters do not have shared meshes.
                    combinedBounds.Encapsulate(sharedMesh.bounds);
            }

            return combinedBounds.center;
        }

        public static bool IsAnyMeshPartOfStaticBatch(this GameObject go, MeshRenderer[] meshRenderers = null)
        {
            // first, get all the mesh renderers on the game object and children if they are not provided
            if (meshRenderers == null)
                meshRenderers = go.GetComponentsInChildren<MeshRenderer>();

            // if there are no mesh renderers, return false.
            if (meshRenderers.Length == 0)
                return false;

            // if any mesh renderer on this game object is marked as Static, return true.
            for (int i = 0; i < meshRenderers.Length; i++)
            {
                if (meshRenderers[i].isPartOfStaticBatch)
                    return true;
            }

            return false;
        }
    }
}