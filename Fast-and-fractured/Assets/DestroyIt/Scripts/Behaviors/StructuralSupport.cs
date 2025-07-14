using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
// ReSharper disable once InlineOutVariableDeclaration

namespace DestroyIt
{
    /// <summary>
    /// Put this script on a destroyed object that has debris pieces you want to stay connected together with joints. (Note: if the object is a prefab, unpack it first.)
    /// The joints this script creates will give your destroyed object's debris pieces structural support until they are destroyed or knocked off by force.
    /// </summary>
    public class StructuralSupport : MonoBehaviour
    {
        [Tooltip("This is the maximum distance allowed to make a structural support connection. Reduce it if you're getting pieces that float in the air and defy physics. Increase it if too many pieces aren't connecting when they should be.")]
        public float maxConnectionDistance = 1.25f;

        [Tooltip("The force required to break a joint on the structure. Set to -1 for Infinity.")]
        public float breakForce = 1250f;

        [Tooltip("The torque required to break a joint on the structure. Set to -1 for Infinity.")]
        public float breakTorque = 3000f;

        private class StructuralPiece
        {
            public GameObject GameObject { get; set; }
            public Rigidbody Rigidbody { get; set; }
            public Vector3 CenterPoint { get; set; }
        }

        public void FixedUpdate()
        {
            // Inspect all the joints during every physics loop and remove any that have missing connected rigidbodies
            FixedJoint[] joints = gameObject.GetComponentsInChildren<FixedJoint>();
            bool jointsRemoved = false;
            for (int i = 0; i < joints.Length; i++)
            {
                if (joints[i].connectedBody == null)
                {
                    Destroy(joints[i]); // remove the joint
                    jointsRemoved = true;
                }
            }

            // If any joints were removed, wake up the rigidbodies on the object
            if (jointsRemoved)
            {
                Rigidbody[] rbodies = gameObject.GetComponentsInChildren<Rigidbody>();
                foreach (Rigidbody rbody in rbodies)
                    rbody.WakeUp();
            }
        }

        [ExecuteInEditMode]
        public void AddStructuralSupport()
        {
#if UNITY_EDITOR
            List<StructuralPiece> pieces = new List<StructuralPiece>();
            List<StructuralPiece> otherPieces = new List<StructuralPiece>();

            // First, get a list of all rigidbodies on the object
            List<Rigidbody> rbodies = gameObject.GetComponentsInChildren<Rigidbody>().ToList();

            // Clear off any old joints on the object so we can create new joint connections.
            foreach (var comp in gameObject.GetComponentsInChildren<Component>())
            {
                if (comp is Joint)
                    DestroyImmediate(comp);
            }

            // Next, get the mesh centerpoint of each rigidbody's game object
            foreach (Rigidbody rbody in rbodies)
            {
                Vector3 center = Vector3.zero;
                foreach (Collider coll in rbody.gameObject.GetComponentsInChildren<Collider>())
                    center = center != Vector3.zero ? Vector3.Lerp(center, coll.bounds.center, 0.5f) : coll.bounds.center;

                pieces.Add(new StructuralPiece {
                    GameObject = rbody.gameObject,
                    Rigidbody = rbody,
                    CenterPoint = center
                });

                otherPieces.Add(new StructuralPiece {
                    GameObject = rbody.gameObject,
                    Rigidbody = rbody,
                    CenterPoint = center
                });
            }

            // Now, for each piece, try to linecast from the center of it to the center of every other piece.
            foreach (StructuralPiece piece in pieces)
            {
                for (int i = 0; i < otherPieces.Count; i++)
                {
                    // skip if this piece is trying to linecast to itself.
                    if (piece.GameObject.GetInstanceID() == otherPieces[i].GameObject.GetInstanceID()) continue; 

                    // If the connection distance is farther than our threshold will allow, exit.
                    if (Vector3.Distance(piece.CenterPoint, otherPieces[i].CenterPoint) > maxConnectionDistance) continue;

                    // Capture the layer this object is on, and move it to the IgnoreRaycast layer temporarily before doing the linecast, so it ignores itself.
                    int originalLayer = piece.GameObject.layer;
                    int ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
                    piece.GameObject.SetLayerRecursively(ignoreLayer);

                    // If we hit a collider while linecasting to the other piece, check it and see if it IS the other piece. If so, that means it's adjacent, and we want to attach a joint connecting the two.
                    RaycastHit hitInfo;
                    if (Physics.Linecast(piece.CenterPoint, otherPieces[i].CenterPoint, out hitInfo))
                    {
                        if (hitInfo.collider.attachedRigidbody == otherPieces[i].Rigidbody)
                        {
                            //Debug.Log($"{piece.GameObject.name} is connected to {otherPieces[i].GameObject.name}");
                            for (int j=0; j<8; j++)
                                Debug.DrawLine(piece.CenterPoint, otherPieces[i].CenterPoint, Color.green, 10f);
                            
                            Vector3 midPoint = Vector3.Lerp(piece.CenterPoint, otherPieces[i].CenterPoint, 0.5f);
                            if (breakForce <= -0.1f)
                                breakForce = Single.PositiveInfinity;
                            if (breakTorque <= -0.1f)
                                breakTorque = Single.PositiveInfinity;

                            piece.GameObject.AddStiffJoint(otherPieces[i].Rigidbody, midPoint, Vector3.zero, breakForce, breakTorque);
                        }
                    }

                    // Set the layer on the piece back to what it was originally
                    piece.GameObject.SetLayerRecursively(originalLayer);
                }

                // Remove this piece from the "other pieces" to check
                otherPieces.RemoveAll(x => x.GameObject.GetInstanceID() == piece.GameObject.GetInstanceID());
            }
#endif
        }

        [ExecuteInEditMode]
        public void RemoveStructuralSupport()
        {
#if UNITY_EDITOR
            FixedJoint[] joints = gameObject.GetComponentsInChildren<FixedJoint>();
            for (int i = 0; i < joints.Length; i++)
                DestroyImmediate(joints[i]); // remove the joint
#endif
        }
    }

    public static class LayerExtensions
    {
        public static void SetLayerRecursively(this GameObject obj, int layer)
        {
            obj.layer = layer;

            foreach (Transform child in obj.transform)
                child.gameObject.SetLayerRecursively(layer);
        }
    }
}
