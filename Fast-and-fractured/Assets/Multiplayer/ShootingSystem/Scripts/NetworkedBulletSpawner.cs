using FishNet.Connection;
using FishNet.Object;
using FishNet.Managing.Server;
using UnityEngine;
using FishNet;

namespace FastAndFractured.Multiplayer
{
    /// <summary>Server bullet spawning shared by NetworkedShoot and NetworkedAIShoot.</summary>
    public static class NetworkedBulletSpawner
    {
        /// <summary>Spawns a networked projectile on the server; null on failure. Null shooter and owner for AI.</summary>
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

            // Validate before instantiating: an invalid projectile's LocalEventRunner could throw in Physics.Simulate.
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

            // The launch state travels in the spawn message (NetworkedPredictedProjectileSpawn).
            var payload = spawnedBulletGO.GetComponent<NetworkedPredictedProjectileSpawn>();
            var netParams = ToNetworkParams(spawnParams);
            var timeManager = serverManager.NetworkManager.TimeManager;
            if (payload != null)
                payload.Configure(netParams, shooterNetworkObject, spawnParams.ShootPoint.position,
                    spawnParams.ShootPoint.rotation, timeManager.Tick, timeManager.LocalTick);

            if (ownerConnection != null)
                serverManager.Spawn(spawnedBulletGO, ownerConnection);
            else
                serverManager.Spawn(spawnedBulletGO);

            if (payload == null)
            {
                spawnedBulletGO.GetComponent<NetworkedProjectile>().RpcInitialize(netParams, shooterNetworkObject,
                    spawnedBulletGO.transform.position, spawnedBulletGO.transform.rotation);
            }

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
                spawnParams.ShootPoint.position, spawnParams.ShootPoint.rotation,
                InstanceFinder.TimeManager.Tick, InstanceFinder.TimeManager.LocalTick);
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
