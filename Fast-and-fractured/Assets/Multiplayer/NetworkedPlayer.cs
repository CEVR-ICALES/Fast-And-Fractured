using FishNet.Object;
using FishNet.Connection;
using UnityEngine;
using FastAndFractured.Multiplayer;  

namespace FastAndFractured.Multiplayer
{
    public class NetworkedPlayer : NetworkBehaviour
    {
        [Tooltip("Prefab for the player's vehicle. Must have NetworkObject and NetworkedVehicle components.")]
        [SerializeField] private NetworkObject _vehiclePrefab;
        private NetworkObject _possessedVehicleInstance;
        [SerializeField] private CarInjector _carInjector;
        private NetworkedLevelControllerManager _networkedLevelManager;

        public override void OnStartServer()
        {
            base.OnStartServer(); 
            _networkedLevelManager = FindFirstObjectByType<NetworkedLevelControllerManager>();
            if (_networkedLevelManager == null)
            {
                if (FastAndFractured.LevelControllerButBetter.Instance == null)
                    Debug.LogError("NetworkedLevelControllerManager AND LevelControllerButBetter not found on server. Cannot spawn vehicle correctly.");
            }

        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (_possessedVehicleInstance != null && _possessedVehicleInstance.IsSpawned)
            {
                _possessedVehicleInstance.Despawn();
            }
        }

       
    }
}