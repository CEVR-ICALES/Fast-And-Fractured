using UnityEngine;
using System;

namespace FastAndFractured.Utilities
{
    /// <summary>
     
    /// </summary>
    public interface IEventSimulable
    { 
        event Action<GameObject> OnDespawnRequested;
 
        void OnSimulateStart(object[] args);
 
        void OnSimulateTick(float deltaTime);
 
        void OnSimulateTriggerEnter(Collider other);
 
        void OnSimulateCollisionEnter(Collision collision);
    }
}
