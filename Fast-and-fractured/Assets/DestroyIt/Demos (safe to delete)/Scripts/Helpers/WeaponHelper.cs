using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DestroyIt
{
    public static class WeaponHelper
    {
        /// <summary>Launches a weapon from the player's controller transform.</summary>
        /// <param name="weaponPrefab">The weapon to launch in front of the player.</param>
        /// <param name="player">The transform of the player controller.</param>
        /// <param name="startDistance">The initial distance from the player transform to instantiate the weapon.</param>
        /// <param name="initialVelocity">The initial force velocity applied to the weapon (if any). For example, a bullet fired from a gun.</param>
        /// <param name="randomRotation">If TRUE, the weapon prefab will be instantiated and rotated randomly before launch.</param>
        public static void Launch(GameObject weaponPrefab, Transform weaponLauncher, float startDistance, float initialVelocity, bool randomRotation)
        {
            Quaternion rotation = randomRotation ? UnityEngine.Random.rotation : weaponLauncher.rotation;

            // Instantiate the projectile.
            var startPos = weaponLauncher.TransformPoint(Vector3.forward * startDistance);
            var projectile = ObjectPool.Instance.Spawn(weaponPrefab, startPos, rotation);
            var projectileRbody = projectile.GetComponent<Rigidbody>();

            // Get the fire direction based on where the player is facing and apply force to propel it forward.
            if (projectileRbody != null)
            {
                projectileRbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                projectileRbody.velocity = Vector3.zero; // zero out the velocity
                if (initialVelocity > 0.0f)
                    projectileRbody.AddForce(weaponLauncher.forward * initialVelocity, ForceMode.Impulse);
            }
        }

        /// <summary>
        /// Launches projectiles that have no rigidbody (such as gameobject bullets), and therefore have no initial velocity.
        /// </summary>
        public static void Launch(GameObject weaponPrefab, Transform weaponLauncher, float startDistance, bool randomRotation)
        {
            Quaternion rotation = randomRotation ? UnityEngine.Random.rotation : weaponLauncher.rotation;

            // Instantiate the projectile.
            var startPos = weaponLauncher.TransformPoint(Vector3.forward * startDistance);
            ObjectPool.Instance.Spawn(weaponPrefab, startPos, rotation);
        }

        /// <summary>Gets the next weapon type available, or cycles back to the beginning if there are no more.</summary>
        public static WeaponType GetNext(WeaponType currentWeaponType)
        {
	        List<WeaponType> weaponTypes = Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>().ToList();
	        
	        int index = (int)currentWeaponType;
	        if (index == weaponTypes.Count - 1)
		        index = 0;
	        else
		        index++;
	        
	    	// Remove the nuke from WebGL builds, because Chrome does not take advantage of asm.js and therefore performance is terrible.
	        #if UNITY_WEBGL
	        if (index == (int)WeaponType.Nuke)
		        index++;
	        if (index > weaponTypes.Count - 1)
		        index = 0;
	        #endif
	        
	        return weaponTypes[index];
        }

        /// <summary>Gets the previous weapon type available, or cycles back to the beginning if there are no more.</summary>
        public static WeaponType GetPrevious(WeaponType currentWeaponType)
        {
	        List<WeaponType> weaponTypes = Enum.GetValues(typeof(WeaponType)).Cast<WeaponType>().ToList();
	        
            int index = (int)currentWeaponType;
            if (index == 0)
                index = weaponTypes.Count - 1;
            else
                index--;
	        
	    	// Remove the nuke from WebGL builds, because Chrome does not take advantage of asm.js and therefore performance is terrible.
	        #if UNITY_WEBGL
	        if (index == (int)WeaponType.Nuke)
		        index--;
	        #endif
	        
            return weaponTypes[index];
        }
    }
}