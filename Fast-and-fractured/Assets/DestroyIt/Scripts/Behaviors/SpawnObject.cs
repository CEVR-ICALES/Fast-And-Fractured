using UnityEngine;

namespace DestroyIt
{
	/// <summary>
	/// This script is used for directly spawning objects into the scene from the Object Pool.
	/// Simply place this script on an empty gameobject at the same location/rotation you would normally instantiate your prefab.
	/// Useful for spawning particle effects on destroyed objects.
	/// </summary>
	public class SpawnObject : MonoBehaviour
	{
		[Tooltip("The prefab of the object you want to spawn into the scene from the object pool.")]
		public GameObject prefab;
		
		private ObjectPool _objectPool;

		private void Start()
		{
			_objectPool = ObjectPool.Instance;
			if (_objectPool == null)
			{
				Debug.LogWarning("Object Pool was not found or could not be created. Removing script and exiting.");
				Destroy(this);
                return;
            }
			
			_objectPool.Spawn(prefab, transform.localPosition, transform.localRotation, transform.parent);
			gameObject.SetActive(false); 
		}
	}
}

