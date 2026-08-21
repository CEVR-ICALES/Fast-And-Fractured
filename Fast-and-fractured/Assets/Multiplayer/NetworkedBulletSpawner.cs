using FishNet.Connection;
using FishNet.Object;
using FishNet.Managing.Server;
using UnityEngine;
using FishNet;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Shared server-authoritative bullet spawning logic used by both
    /// NetworkedShoot (player) and NetworkedAIShoot (AI).
    /// Eliminates code duplication between the two shoot handlers.
    /// </summary>
    public static class NetworkedBulletSpawner
    {
        /// <summary>
        /// Spawns a networked projectile on the server, initializes it via RPC, and returns the spawned GameObject.
        /// Returns null if spawning failed.
        /// </summary>
        /// <param name="prefab">The networked projectile prefab to instantiate.</param>
        /// <param name="spawnParams">Bullet parameters from the shoot handle.</param>
        /// <param name="shooterNetworkObject">The NetworkObject of the shooter (for collision ignoring). Can be null for ownerless AI.</param>
        /// <param name="serverManager">The ServerManager used to network-spawn the object.</param>
        /// <param name="ownerConnection">The owner connection for the spawned bullet. Null for AI (server-owned).</param>
        public static GameObject SpawnServerAuthoritative(
            GameObject prefab,
            BulletSpawnParameters spawnParams,
            NetworkObject shooterNetworkObject,
            ServerManager serverManager,
            NetworkConnection ownerConnection = null)
        {
            if (prefab == null)
            {
                Debug.LogError("[NetworkedBulletSpawner] No networked prefab assigned for this bullet type.");
                return null;
            }

            if (spawnParams.ShootPoint == null)
            {
                Debug.LogWarning("[NetworkedBulletSpawner] Server spawn aborted: ShootPoint is null.");
                return null;
            }

            // Validate the asset before instantiating it. Previously an invalid single-player
            // projectile stayed alive until deferred Destroy() ran at the end of the frame;
            // its LocalEventRunner could then participate in Physics.Simulate and throw before
            // it disappeared.
            if (prefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError($"[NetworkedBulletSpawner] Prefab '{prefab.name}' is missing NetworkObject. Spawn aborted.", prefab);
                return null;
            }

            if (prefab.GetComponent<NetworkedProjectile>() == null)
            {
                Debug.LogError($"[NetworkedBulletSpawner] Prefab '{prefab.name}' is missing NetworkedProjectile. Spawn aborted.", prefab);
                return null;
            }

            GameObject spawnedBulletGO = Object.Instantiate(prefab, spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);
            spawnedBulletGO.transform.SetParent(null);

            var networkObject = spawnedBulletGO.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError("[NetworkedBulletSpawner] Prefab missing NetworkObject.", spawnedBulletGO);
                Object.Destroy(spawnedBulletGO);
                return null;
            }

            if (ownerConnection != null)
                serverManager.Spawn(spawnedBulletGO, ownerConnection);
            else
                serverManager.Spawn(spawnedBulletGO);

            var networkedProjectile = spawnedBulletGO.GetComponent<NetworkedProjectile>();
            if (networkedProjectile == null)
            {
                Debug.LogError("[NetworkedBulletSpawner] Prefab missing NetworkedProjectile.", spawnedBulletGO);
                networkObject.Despawn();
                return null;
            }

            var netParams = ToNetworkParams(spawnParams);

            networkedProjectile.RpcInitialize(netParams, shooterNetworkObject,
                spawnedBulletGO.transform.position, spawnedBulletGO.transform.rotation);

            return spawnedBulletGO;
        }

        /// <summary>Creates the real FishNet predicted object immediately on a remote owner.</summary>
        public static GameObject SpawnPredictedClient(GameObject prefab, BulletSpawnParameters spawnParams,
            NetworkObject shooterNetworkObject, NetworkConnection ownerConnection)
        {
            if (InstanceFinder.IsServerStarted || !InstanceFinder.IsClientStarted || prefab == null ||
                spawnParams.ShootPoint == null || ownerConnection == null)
                return null;

            GameObject go = Object.Instantiate(prefab, spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);
            go.transform.SetParent(null);
            var payload = go.GetComponent<NetworkedPredictedProjectileSpawn>();
            if (payload == null)
            {
                Debug.LogError($"[NetworkedBulletSpawner] '{prefab.name}' lacks NetworkedPredictedProjectileSpawn.", prefab);
                Object.Destroy(go);
                return null;
            }

            payload.Configure(ToNetworkParams(spawnParams), shooterNetworkObject,
                spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation);
            InstanceFinder.ServerManager.Spawn(go, ownerConnection);
            return go;
        }

        internal static NetworkShootParams ToNetworkParams(BulletSpawnParameters spawnParams) => new NetworkShootParams
        {
            Pooltype = spawnParams.Pooltype,
            InitialVelocity = spawnParams.Velocity,
            Range = spawnParams.Range,
            Damage = spawnParams.Damage,
            PushForce = spawnParams.PushForce,
            ExplosionRadius = spawnParams.ExplosionRadius,
            ExplosionCenterOffset = spawnParams.ExplosionCenterOffset,
            CustomGravity = spawnParams.CustomGravity,
            BouncingNum = spawnParams.BouncingNum,
            BouncingStrenght = spawnParams.BouncingStrenght,
            TimeToExplode = spawnParams.TimeToExplode,
            IsMine = spawnParams.IsMine
        };
    }
}
