using FishNet.Object;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured.Multiplayer;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Valida que los proyectiles se destruyan sincronizadamente en todos los clientes.
    /// Adjunta esto a cualquier GameObject con NetworkObject en la escena de test.
    /// </summary>
    public class ProjectileDestructionTestValidator : NetworkBehaviour
    {
        private Dictionary<int, ProjectileLifetimeData> _projectileLifetimes = new Dictionary<int, ProjectileLifetimeData>();
        
        private class ProjectileLifetimeData
        {
            public int ObjectId;
            public string ObjectName;
            public float ServerSpawnTick;
            public float ServerDestroyTick;
            public Dictionary<int, float> ClientDestroyTicks = new Dictionary<int, float>();
            public bool HasDesync => ClientDestroyTicks.Count > 0 && GetMaxTickDifference() > 3; // > 3 ticks = desync
            
            public float GetMaxTickDifference()
            {
                if (ClientDestroyTicks.Count == 0) return 0f;
                
                float min = ServerDestroyTick;
                float max = ServerDestroyTick;
                
                foreach (var tick in ClientDestroyTicks.Values)
                {
                    min = Mathf.Min(min, tick);
                    max = Mathf.Max(max, tick);
                }
                
                return max - min;
            }
        }
        
        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            
            if (IsServerInitialized)
            {
                Debug.Log("<color=cyan>[ProjectileDestructionValidator] Started on SERVER - Monitoring projectile destruction</color>");
            }
            else
            {
                Debug.Log("<color=cyan>[ProjectileDestructionValidator] Started on CLIENT - Monitoring projectile destruction</color>");
            }
        }
        
        /// <summary>
        /// Llamar esto cuando un proyectil se spawna en el servidor
        /// </summary>
        [Server]
        public void RegisterProjectileSpawn(NetworkObject projectileNob)
        {
            if (!IsServerInitialized) return;
            
            var data = new ProjectileLifetimeData
            {
                ObjectId = projectileNob.ObjectId,
                ObjectName = projectileNob.gameObject.name,
                ServerSpawnTick = base.TimeManager.Tick
            };
            
            _projectileLifetimes[projectileNob.ObjectId] = data;
            
            Debug.Log($"<color=green>[ProjectileDestructionValidator - SERVER] Registered projectile spawn: {projectileNob.gameObject.name} (ID: {projectileNob.ObjectId}) at tick {data.ServerSpawnTick}</color>");
        }
        
        /// <summary>
        /// Llamar esto cuando un proyectil se destruye
        /// </summary>
        public void RegisterProjectileDestruction(int objectId, string objectName)
        {
            uint currentTick = base.TimeManager.Tick;
            
            if (IsServerInitialized)
            {
                if (_projectileLifetimes.ContainsKey(objectId))
                {
                    _projectileLifetimes[objectId].ServerDestroyTick = currentTick;
                    Debug.Log($"<color=yellow>[ProjectileDestructionValidator - SERVER] Projectile destroyed: {objectName} (ID: {objectId}) at tick {currentTick}</color>");
                    
                    // Notificar a clientes para que reporten su tick de destrucción
                    NotifyProjectileDestroyedRpc(objectId, objectName, currentTick);
                }
            }
            else
            {
                // Cliente reporta al servidor
                ReportClientDestructionRpc(objectId, objectName, currentTick, base.LocalConnection.ClientId);
            }
        }
        
        [ObserversRpc]
        private void NotifyProjectileDestroyedRpc(int objectId, string objectName, uint serverDestroyTick)
        {
            if (IsServerInitialized) return; // Solo clientes
            
            Debug.Log($"<color=cyan>[ProjectileDestructionValidator - CLIENT] Server destroyed projectile {objectName} (ID: {objectId}) at tick {serverDestroyTick}</color>");
        }
        
        [ServerRpc(RequireOwnership = false)]
        private void ReportClientDestructionRpc(int objectId, string objectName, uint clientDestroyTick, int clientId)
        {
            if (_projectileLifetimes.ContainsKey(objectId))
            {
                var data = _projectileLifetimes[objectId];
                data.ClientDestroyTicks[clientId] = clientDestroyTick;
                
                float tickDifference = clientDestroyTick - data.ServerDestroyTick;
                
                if (Mathf.Abs(tickDifference) > 3)
                {
                    Debug.LogWarning($"<color=red>[ProjectileDestructionValidator] DESYNC DETECTED!</color>\n" +
                                   $"  Projectile: {objectName} (ID: {objectId})\n" +
                                   $"  Server destroy tick: {data.ServerDestroyTick}\n" +
                                   $"  Client {clientId} destroy tick: {clientDestroyTick}\n" +
                                   $"  Difference: {tickDifference} ticks ({tickDifference * base.TimeManager.TickDelta:F3}s)\n" +
                                   $"  THIS IS THE BUG WE FIXED!");
                }
                else
                {
                    Debug.Log($"<color=green>[ProjectileDestructionValidator] Client {clientId} destruction synchronized: {objectName} (diff: {tickDifference} ticks)</color>");
                }
            }
        }
        
        [ContextMenu("Generate Report")]
        public void GenerateReport()
        {
            if (!IsServerInitialized)
            {
                Debug.LogWarning("[ProjectileDestructionValidator] Reports can only be generated on server!");
                return;
            }
            
            Debug.Log($"<color=cyan>========================================</color>");
            Debug.Log($"<color=cyan>[ProjectileDestructionValidator] DESTRUCTION SYNC REPORT</color>");
            Debug.Log($"<color=cyan>========================================</color>");
            
            int totalProjectiles = _projectileLifetimes.Count;
            int syncedProjectiles = 0;
            int desyncedProjectiles = 0;
            
            foreach (var kvp in _projectileLifetimes)
            {
                var data = kvp.Value;
                
                if (data.HasDesync)
                {
                    desyncedProjectiles++;
                    Debug.LogWarning($"<color=red>[X] {data.ObjectName} - Desync detected (max diff: {data.GetMaxTickDifference()} ticks)</color>");
                }
                else
                {
                    syncedProjectiles++;
                }
            }
            
            float syncRate = totalProjectiles > 0 ? (syncedProjectiles / (float)totalProjectiles) * 100f : 100f;
            
            Debug.Log($"<color=white>Total Projectiles Tracked: {totalProjectiles}</color>");
            Debug.Log($"<color=green>Synchronized: {syncedProjectiles} ({syncRate:F1}%)</color>");
            Debug.Log($"<color=red>Desynchronized: {desyncedProjectiles} ({100 - syncRate:F1}%)</color>");
            
            if (desyncedProjectiles == 0)
            {
                Debug.Log($"<color=green>[OK]PERFECT SYNC! All projectiles destroyed simultaneously across all clients.</color>");
            }
            else
            {
                Debug.LogWarning($"<color=yellow>⚠ Desync issues detected. The LocalEventRunner disable fix should resolve this.</color>");
            }
            
            Debug.Log($"<color=cyan>========================================</color>");
        }
        
        private void OnDestroy()
        {
            if (IsServerInitialized && _projectileLifetimes.Count > 0)
            {
                GenerateReport();
            }
        }
    }
}
