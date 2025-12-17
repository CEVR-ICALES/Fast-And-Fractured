using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities;

namespace FastAndFractured.Utilities
{
    public class LocalEventRunner : MonoBehaviour
    {
        private List<IEventSimulable> _simulables;

        private void Awake()
        {
            _simulables = GetComponents<IEventSimulable>().ToList();
            foreach (var _simulable in _simulables)
            {
                _simulable.OnDespawnRequested += HandleDespawnRequest;

            } 
        }

        private void Start()
        {
            foreach (var _simulable in _simulables)
            {
                _simulable?.OnSimulateStart(null);

            }
        }

        private void OnDestroy()
        {
            foreach (var _simulable in _simulables)
            {
                _simulable.OnDespawnRequested -= HandleDespawnRequest;
            }
        }

        private void FixedUpdate()
        {
            foreach (var _simulable in _simulables)
            {
                _simulable?.OnSimulateTick(Time.fixedDeltaTime);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            foreach (var _simulable in _simulables)
            {
                _simulable?.OnSimulateTriggerEnter(other);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            foreach (var _simulable in _simulables)
            {
                _simulable?.OnSimulateCollisionEnter(collision);
            }
        }

        private void HandleDespawnRequest(GameObject obj)
        {
            if (obj.TryGetComponent<IPooledObject>(out var pooledObject))
            {
                ObjectPoolManager.Instance.DesactivatePooledObject(pooledObject, obj);
            }
            else
            {
                Destroy(obj);
            }
        }
    }
}