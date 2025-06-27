
using FastAndFractured;  
using FastAndFractured.Multiplayer;
using FishNet.Connection;
 
using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    public class NetworkedLevelControllerManager : NetworkBehaviour
    {
        public override void OnStartServer()
        {
            base.OnStartServer();

           
            Debug.Log("NetworkedLevelControllerManager started on server.");
            Debug.Log("<color=green>OnStartServer called on NetworkedLevelControllerManager.</color>");

        }



        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log("<color=cyan>OnStartClient called on NetworkedLevelControllerManager. Client is now active for this object!</color>");
        }    
        public void AddCharacterSkin(NetworkConnection connection,string name)
        {
            LevelControllerButBetter.Instance.AddCharacterToListOfSelectedCharacters(connection.ClientId,name);
            Debug.Log($"<color=cyan>Client {connection.ClientId} selected {name} </color>");

        }

        [ServerRpc(RequireOwnership = false)]
        public void StartGame()
        { 
		
            if (LevelControllerButBetter.Instance != null)
            {
                PlayerPrefs.SetInt("Player_Num", FishNetNetworkManagerWrapper.Instance.PlayerCount);
                LevelControllerButBetter.Instance.PerformConstruct();
                LevelControllerButBetter.Instance.PerformInitialize();
            }
            IngameEventsManager ingameEventsManager = FindFirstObjectByType<IngameEventsManager>(FindObjectsInactive.Exclude);
            ingameEventsManager.PerformConstruct();
            ingameEventsManager.PerformInitialize();
            SpawnCharactersOnClients();


    }

        void SpawnCharactersOnClients()
        {
            List<GameObject> charactersToSpawn = new List<GameObject>(LevelControllerButBetter.Instance.InGameCharacters);
            int playerIndex = 0;
            foreach (var conn in base.ServerManager.Clients.Values)
            {
                if (playerIndex < charactersToSpawn.Count)
                {
                    GameObject playerVehicleGO = charactersToSpawn[playerIndex];
                    NetworkObject playerVehicleNob = playerVehicleGO.GetComponent<NetworkObject>();

                    if (playerVehicleNob != null)
                    {
                        base.ServerManager.Spawn(playerVehicleNob, conn);
                        Debug.Log($"Spawning vehicle {playerVehicleGO.name} and giving ownership to ClientId {conn.ClientId}.");
                    }
                    else
                    {
                        Debug.LogError($"Vehicle '{playerVehicleGO.name}' created by LevelController is missing a NetworkObject component. It cannot be spawned.");
                    }

                    charactersToSpawn.RemoveAt(playerIndex);                }
                else
                {
                    Debug.LogWarning($"Not enough player vehicles spawned by LevelController for all connected clients. ClientId {conn.ClientId} will not get a car.");
                }
            }

            foreach (GameObject aiVehicleGO in charactersToSpawn)
            {
                NetworkObject aiVehicleNob = aiVehicleGO.GetComponent<NetworkObject>();
                if (aiVehicleNob != null)
                {
                    base.ServerManager.Spawn(aiVehicleNob);
                    Debug.Log($"Spawning AI vehicle {aiVehicleGO.name} with server ownership.");
                }
                else
                {
                    Debug.LogError($"AI Vehicle '{aiVehicleGO.name}' created by LevelController is missing a NetworkObject component. It cannot be spawned.");
                }
            }
        }
    }
} 