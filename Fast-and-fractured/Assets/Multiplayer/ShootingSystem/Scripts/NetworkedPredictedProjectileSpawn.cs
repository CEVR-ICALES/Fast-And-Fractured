using FishNet.Component.Ownership;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;
using Enums;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Launch state of a shot, carried in its spawn message so every peer starts the flight from the
    /// same data at the same moment the object appears - both for shots a client predicts (FishNet Pro
    /// predicted spawn, validated here by the server) and for shots the server spawns itself.
    ///
    /// LaunchTick is when the shot left the gun on the server's tick clock (a client's
    /// TimeManager.Tick is its estimate of the server tick). Peers that receive the spawn later - the
    /// server for a predicted shot, every observer - fly it forward by the difference, so the shot is
    /// at the same place on every screen instead of trailing by the latency.
    ///
    /// LaunchLocalTick is the same moment on the shooter's OWN clock (TimeManager.LocalTick), which is
    /// what the server validates against (its PacketTick for the connection is on that clock). The
    /// server-tick estimate is not safe for validation: measured 2026-09-30, a client's estimate sat
    /// ~49 ticks (1 s) behind the real server tick after a one-second editor hitch, and a push shot
    /// fired right then was rejected as "too old".
    /// </summary>
    [RequireComponent(typeof(NetworkedProjectile))]
    public sealed class NetworkedPredictedProjectileSpawn : PredictedSpawn
    {
        // Late peers catch up at most this much; beyond it the shot simply starts from the gun.
        internal const uint MAX_CATCH_UP_TICKS = 5;

        private bool _configured;
        private NetworkShootParams _data;
        private NetworkObject _shooter;
        private Vector3 _spawnPosition;
        private Quaternion _spawnRotation;
        private uint _launchTick;
        private uint _launchLocalTick;

        public void Configure(NetworkShootParams data, NetworkObject shooter, Vector3 position, Quaternion rotation,
            uint launchTick, uint launchLocalTick)
        {
            _configured = true;
            _data = data;
            _shooter = shooter;
            _spawnPosition = position;
            _spawnRotation = rotation;
            _launchTick = launchTick;
            _launchLocalTick = launchLocalTick;
        }

        public override void WritePayload(NetworkConnection connection, Writer writer)
        {
            writer.WriteBoolean(_configured);
            if (!_configured) return;
            WriteParams(writer, _data);
            writer.WriteNetworkObject(_shooter);
            writer.WriteVector3(_spawnPosition);
            writer.WriteQuaternion(_spawnRotation, AutoPackType.Packed);
            writer.WriteUInt32(_launchTick);
            writer.WriteUInt32(_launchLocalTick);
        }

        public override void ReadPayload(NetworkConnection connection, Reader reader)
        {
            _configured = reader.ReadBoolean();
            if (!_configured) return;
            _data = ReadParams(reader);
            _shooter = reader.ReadNetworkObject();
            _spawnPosition = reader.ReadVector3();
            _spawnRotation = reader.ReadQuaternion(AutoPackType.Packed);
            _launchTick = reader.ReadUInt32();
            _launchLocalTick = reader.ReadUInt32();
        }

        // Kept separate so the round trip can be unit tested without a NetworkObject.
        internal static void WriteParams(Writer writer, NetworkShootParams data)
        {
            writer.WriteInt32((int)data.Pooltype);
            writer.WriteVector3(data.InitialVelocity);
            writer.WriteSingle(data.Range); writer.WriteSingle(data.Damage); writer.WriteSingle(data.PushForce);
            writer.WriteSingle(data.ExplosionRadius); writer.WriteVector3(data.ExplosionCenterOffset);
            writer.WriteVector3(data.CustomGravity); writer.WriteInt32(data.BouncingNum);
            writer.WriteSingle(data.BouncingStrenght); writer.WriteSingle(data.TimeToExplode);
            writer.WriteBoolean(data.IsMine);
        }

        internal static NetworkShootParams ReadParams(Reader reader)
        {
            NetworkShootParams data = default;
            data.Pooltype = (Pooltype)reader.ReadInt32();
            data.InitialVelocity = reader.ReadVector3();
            data.Range = reader.ReadSingle(); data.Damage = reader.ReadSingle(); data.PushForce = reader.ReadSingle();
            data.ExplosionRadius = reader.ReadSingle(); data.ExplosionCenterOffset = reader.ReadVector3();
            data.CustomGravity = reader.ReadVector3(); data.BouncingNum = reader.ReadInt32();
            data.BouncingStrenght = reader.ReadSingle(); data.TimeToExplode = reader.ReadSingle();
            data.IsMine = reader.ReadBoolean();
            return data;
        }

        public override bool OnTrySpawnServer(NetworkConnection spawner, NetworkConnection owner = null)
        {
            if (!_configured || _shooter == null || spawner == null || _shooter.Owner != spawner || owner != spawner)
            {
                MpCanary.Mark($"PredictedProjectile REJECT identity configured={_configured} shooter={_shooter != null} spawner={spawner?.ClientId}");
                ShootingNetworkStats.ProjectilesRejected++;
                return false;
            }
            var shoot = _shooter.GetComponentInChildren<NetworkedShoot>(true);
            string reason = "no NetworkedShoot";
            if (shoot == null || !shoot.TryValidatePredictedProjectile(NetworkObject, _data, _spawnPosition, _spawnRotation,
                    _launchLocalTick, spawner.PacketTick.LastRemoteTick, out _data, out _spawnPosition, out _spawnRotation, out reason))
            {
                MpCanary.Mark($"PredictedProjectile REJECT {reason} spawner={spawner.ClientId} prefabId={NetworkObject.PrefabId}");
                ShootingNetworkStats.ProjectilesRejected++;
                return false;
            }
            // Only the validated launch state is stored here; the flight starts in OnStartNetwork. This
            // callback runs BEFORE the object is spawned on the server, and a shot that hit something
            // while catching up in here ended without its server-side end (damage, despawn), so it was
            // never removed on any peer (measured: 3 of Carme's 5 eggs lingered for the whole match).
            transform.SetPositionAndRotation(_spawnPosition, _spawnRotation);
            MpCanary.Mark($"PredictedProjectile ACCEPT spawner={spawner.ClientId} prefabId={NetworkObject.PrefabId} mine={_data.IsMine} catchUp={TicksSinceLaunch()}");
            ShootingNetworkStats.ProjectilesAccepted++;
            return true;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_configured) return;
            // The shooter's own copy starts at the gun; everyone else joins the flight in progress.
            bool launchedHere = base.Owner.IsLocalClient && !IsServerInitialized ||
                                IsServerInitialized && _launchTick == TimeManager.Tick;
            GetComponent<NetworkedProjectile>().InitializeProjectile(_data, _shooter, _spawnPosition, _spawnRotation,
                launchedHere ? 0u : TicksSinceLaunch());
        }

        private uint TicksSinceLaunch()
        {
            uint now = TimeManager.Tick;
            if (_launchTick == 0 || now <= _launchTick) return 0;
            return System.Math.Min(now - _launchTick, MAX_CATCH_UP_TICKS);
        }
    }
}
