using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif
// ReSharper disable ForCanBeConvertedToForeach

namespace DestroyIt
{
    public static class DestructionExtensions
    {
        public static void Update(this List<float> models, int withinSeconds)
        {
            bool isChanged = false;
            if (models.Count > 0)
            {
                for (int i = 0; i < models.Count; i++)
                {
                    if (Time.time > (models[i] + withinSeconds))
                    {
                        models.Remove(models[i]);
                        isChanged = true;
                    }
                }
                if (isChanged)
                    DestructionManager.Instance.FireDestroyedPrefabCounterChangedEvent(); 
            }
        }

        public static void ReleaseClingingDebris(this Destructible destroyedObj)
        {
            List<Transform> clingingDebris = new List<Transform>();
            TagIt[] tagIts = destroyedObj.GetComponentsInChildren<TagIt>();
            if (tagIts == null) return;

            for (int i = 0; i < tagIts.Length; i++)
            {
                for (int j = 0; j < tagIts[i].tags.Count; j++)
                {
                    if (tagIts[i].tags[j] == Tag.ClingingDebris)
                        clingingDebris.Add(tagIts[i].transform);
                }
            }

            for (int i = 0; i < clingingDebris.Count; i++)
            {
                //TODO: When releasing clinging debris, we need to add back the same rigidbody configuration the object had before becoming debris.
                clingingDebris[i].gameObject.AddComponent<Rigidbody>();
            }
        }

        public static void MakeDebrisCling(this GameObject destroyedObj)
        {
            // Check to see if any debris pieces will be clinging to nearby rigidbodies
            ClingPoint[] clingPoints = destroyedObj.GetComponentsInChildren<ClingPoint>();
            for (int i=0; i<clingPoints.Length; i++)
            {
                Rigidbody clingPointRbody = clingPoints[i].transform.parent.GetComponent<Rigidbody>();
                if (clingPointRbody == null) continue;

                // Check percent chance first
                if (clingPoints[i].chanceToCling < 100) // 100% chance always clings
                {
                    int randomNbr = Random.Range(1, 100);
                    if (randomNbr > clingPoints[i].chanceToCling) // exit if random number is outside the possible chance.
                        continue;
                }

                // Check if there's anything to cling to.
                Ray ray = new Ray(clingPoints[i].transform.position - (clingPoints[i].transform.forward * 0.025f), clingPoints[i].transform.forward); // need to start the ray behind the transform a little
                RaycastHit hitInfo;
                if (Physics.Raycast(ray, out hitInfo, 0.075f))
                {
                    if (hitInfo.collider.isTrigger) continue; // ignore trigger colliders.

                    clingPointRbody.transform.parent = hitInfo.collider.gameObject.transform;

                    // If the debris is Destructible, add DestructibleParent script to the parent so debris will get OnCollisionEnter() events.
                    if (clingPointRbody.gameObject.GetComponent<Destructible>() && !hitInfo.collider.gameObject.GetComponent<DestructibleParent>())
                        hitInfo.collider.gameObject.AddComponent<DestructibleParent>();

                    // If the object this debris is clinging to is also destructible, set it up so it will be released when the parent object is destroyed.
                    Destructible destructibleObj = hitInfo.collider.gameObject.GetComponentInParent<Destructible>();
                    if (destructibleObj != null)
                    {
                        destructibleObj.unparentOnDestroy.Add(clingPointRbody.gameObject);
                        DelayedRigidbody delayedRbody = clingPointRbody.gameObject.AddComponent<DelayedRigidbody>();
                        delayedRbody.mass = clingPointRbody.mass;
                        delayedRbody.drag = clingPointRbody.drag;
                        delayedRbody.angularDrag = clingPointRbody.angularDrag;
                    }
                    // Remove all cling points from this clinging debris object
                    ClingPoint[] clingPointsToDestroy = clingPointRbody.gameObject.GetComponentsInChildren<ClingPoint>();
                    for (int j = 0; j < clingPointsToDestroy.Length; j++)
                        Object.Destroy(clingPointsToDestroy[j].gameObject);
                        
                    // Remove all rigidbodies from this clinging debris object
                    clingPointRbody.gameObject.RemoveAllFromChildren<Rigidbody>();
                }
            }
        }

        public static void ProcessDestructibleCollision(this Destructible destructibleObj, Collision collision, Rigidbody collidingRigidbody)
        {
            // Ignore collisions if collidingRigidbody is null
            if (collidingRigidbody == null) return;

            // Check for DontDoDamage tag on the collidingRigidbody
            if (collidingRigidbody.gameObject.HasTag(Tag.DontDoDamage)) return;

            // Ignore collisions if this object is destroyed.
            if (destructibleObj.IsDestroyed) return;

            // Check that the impact is forceful enough to cause damage
            if (collision.relativeVelocity.magnitude < destructibleObj.ignoreCollisionsUnder) return;

            if (collision.contacts.Length == 0) return;

            float impactDamage;
            Rigidbody otherRbody = collision.contacts[0].otherCollider.gameObject.GetComponentInParent<Rigidbody>(true);

            // If we've collided with another rigidbody, use the average mass of the two objects for impact damage.
            if (otherRbody != null)
            {
                if (otherRbody.gameObject.HasTag(Tag.DontDoDamage)) return;
                float avgMass = (otherRbody.mass + collidingRigidbody.mass) / 2;
                impactDamage = Vector3.Dot(collision.contacts[0].normal, collision.relativeVelocity) * avgMass;
            }
            else // If we've collided with a static object (terrain, static collider, etc), use this object's attached rigidbody.
            {
                Rigidbody rbody = destructibleObj.GetComponent<Rigidbody>();
                impactDamage = Vector3.Dot(collision.contacts[0].normal, collision.relativeVelocity) * collidingRigidbody.mass;
            }

            impactDamage = Mathf.Abs(impactDamage); // can't have negative damage

            if (impactDamage > 1f) // impact must do at least 1 damage to bother with.
            {
                //Debug.Log("Impact Damage: " + impactDamage);
                //Debug.DrawRay(otherRbody.transform.position, collision.relativeVelocity, Color.yellow, 10f); // yellow: where the impact force is heading
                ImpactDamage impactInfo = new ImpactDamage() { ImpactObject = otherRbody, DamageAmount = impactDamage, ImpactObjectVelocityFrom = collision.relativeVelocity * -1 };
                destructibleObj.ApplyDamage(impactInfo);
            }
        }

        public static void CalculateDamageLevels(this List<DamageLevel> damageLevels, float maxHitPoints)
        {
            if (maxHitPoints <= 0) { return; }
            if (damageLevels == null || damageLevels.Count == 0) { return; }

            // Sort the list descending on Damage Percent field.
            //damageLevels.Sort((x, y) => -1 * x.damagePercent.CompareTo(y.damagePercent));

            int prevHealthPercent = -1;
            for (int i = 0; i < damageLevels.Count; i++)
            {
                if (damageLevels[i] == null) continue;
                if (damageLevels[i].healthPercent <= 0)
                {
                    damageLevels[i].hasError = true;
                    continue;
                }
                if (prevHealthPercent > -1 && damageLevels[i].healthPercent >= prevHealthPercent)
                {
                    damageLevels[i].hasError = true;
                    prevHealthPercent = damageLevels[i].healthPercent;
                    continue; // Health percents should go down with every subsequent damage level.
                }

                damageLevels[i].hasError = false;
                if (i == 0) // highest damage level, set max hit points to maxHitPoints of destructible object.
                    damageLevels[i].maxHitPoints = maxHitPoints;
                else // not the highest damage level, so set the previous level's minHitPoints to this level's maxHitPoints.
                {
                    damageLevels[i].maxHitPoints = maxHitPoints * (.01f * damageLevels[i].healthPercent);
                    damageLevels[i - 1].minHitPoints = maxHitPoints * (.01f * damageLevels[i].healthPercent) + .1f;
                }
                if (i == damageLevels.Count - 1) // lowest damage level, set min hit point range to 0.
                    damageLevels[i].minHitPoints = 0;

                prevHealthPercent = damageLevels[i].healthPercent;
            }
        }

        public static DamageLevel GetDamageLevel(this List<DamageLevel> damageLevels, float hitPoints)
        {
            if (damageLevels == null || damageLevels.Count == 0) return null;

            if (hitPoints <= 0)
                return damageLevels[damageLevels.Count - 1];

            for (int i = 0; i < damageLevels.Count; i++)
            {
                if (damageLevels[i].maxHitPoints >= hitPoints && damageLevels[i].minHitPoints <= hitPoints)
                    return damageLevels[i];
            }

            return null;
        }

        public static void ReparentChildren(this Destructible destObj, GameObject newObj)
        {
            if (destObj.childrenToReParentByName != null)
            {
                if (destObj.childrenToReParentByName.Count > 0)
                {
                    foreach (string childName in destObj.childrenToReParentByName)
                    {
                        Transform child = destObj.transform.Find(childName);
                        if (child != null)
                            child.SetParent(newObj.transform);
                    }
                }
            }
        }

        /// <summary>If autoDeativateDestructibles is set to true on DestructionManager, start the Destructible component deactivated.</summary>
        /// <remarks>Very useful for trees and other terrain objects, makes performance better.</remarks>
        public static void SetActiveOrInactive(this Destructible destObj, DestructionManager destructionManager)
        {
            if (!destObj.isTerrainTree) // non-terrain tree destructibles
            {
                if (destructionManager.autoDeactivateDestructibles)
                    destObj.enabled = false;
            }
            else // terrain tree destructibles
            {
                if (destructionManager.autoDeactivateDestructibleTerrainObjects)
                    destObj.enabled = false;
            }
        }
    }
}