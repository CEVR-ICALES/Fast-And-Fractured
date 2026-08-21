using FishNet.Component.Ownership;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;
using Enums;

namespace FastAndFractured.Multiplayer
{
    /// <summary>FishNet Pro predicted-spawn payload and authoritative validation for projectiles.</summary>
    [RequireComponent(typeof(NetworkedProjectile))]
    public sealed class NetworkedPredictedProjectileSpawn : PredictedSpawn
    {
        private bool _configured;
        private NetworkShootParams _data;
        private NetworkObject _shooter;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;

        public void Configure(NetworkShootParams data, NetworkObject shooter, Vector3 position, Quaternion rotation)
        {
            _configured = true;
            _data = data;
            _shooter = shooter;
            _spawnPosition = position;
            _spawnRotation = rotation;
        }

        public override void WritePayload(NetworkConnection connection, Writer writer)
        {
            writer.WriteBoolean(_configured);
            if (!_configured) return;
            writer.WriteInt32((int)_data.Pooltype);
            writer.WriteVector3(_data.InitialVelocity);
            writer.WriteSingle(_data.Range); writer.WriteSingle(_data.Damage); writer.WriteSingle(_data.PushForce);
            writer.WriteSingle(_data.ExplosionRadius); writer.WriteVector3(_data.ExplosionCenterOffset);
            writer.WriteVector3(_data.CustomGravity); writer.WriteInt32(_data.BouncingNum);
            writer.WriteSingle(_data.BouncingStrenght); writer.WriteSingle(_data.TimeToExplode);
            writer.WriteBoolean(_data.IsMine); writer.WriteNetworkObject(_shooter);
            writer.WriteVector3(_spawnPosition); writer.WriteQuaternion(_spawnRotation, AutoPackType.Packed);
        }

        public override void ReadPayload(NetworkConnection connection, Reader reader)
        {
            _configured = reader.ReadBoolean();
            if (!_configured) return;
            _data.Pooltype = (Pooltype)reader.ReadInt32();
            _data.InitialVelocity = reader.ReadVector3();
            _data.Range = reader.ReadSingle(); _data.Damage = reader.ReadSingle(); _data.PushForce = reader.ReadSingle();
            _data.ExplosionRadius = reader.ReadSingle(); _data.ExplosionCenterOffset = reader.ReadVector3();
            _data.CustomGravity = reader.ReadVector3(); _data.BouncingNum = reader.ReadInt32();
            _data.BouncingStrenght = reader.ReadSingle(); _data.TimeToExplode = reader.ReadSingle();
            _data.IsMine = reader.ReadBoolean(); _shooter = reader.ReadNetworkObject();
            _spawnPosition = reader.ReadVector3(); _spawnRotation = reader.ReadQuaternion(AutoPackType.Packed);
        }

        public override bool OnTrySpawnServer(NetworkConnection spawner, NetworkConnection owner = null)
        {
            if (!_configured || _shooter == null || spawner == null || _shooter.Owner != spawner || owner != spawner)
            {
                MpCanary.Mark($"PredictedProjectile REJECT identity configured={_configured} shooter={_shooter != null} spawner={spawner?.ClientId}");
                return false;
            }
            var shoot = _shooter.GetComponentInChildren<NetworkedShoot>(true);
            if (shoot == null || !shoot.TryValidatePredictedProjectile(NetworkObject, _data,
                    out _data, out _spawnPosition, out _spawnRotation))
            {
                MpCanary.Mark($"PredictedProjectile REJECT validation spawner={spawner.ClientId} prefabId={NetworkObject.PrefabId}");
                return false;
            }
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            GetComponent<NetworkedProjectile>().InitializeProjectile(_data, _shooter, _spawnPosition, _spawnRotation);
            MpCanary.Mark($"PredictedProjectile ACCEPT spawner={spawner.ClientId} prefabId={NetworkObject.PrefabId} mine={_data.IsMine}");
            return true;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (_configured)
                GetComponent<NetworkedProjectile>().InitializeProjectile(_data, _shooter, _spawnPosition, _spawnRotation);
        }
    }
}
