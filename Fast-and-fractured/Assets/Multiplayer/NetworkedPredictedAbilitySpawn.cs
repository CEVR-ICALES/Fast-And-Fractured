using FishNet.Component.Ownership;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Serializing;
using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    public enum PredictedAbilityObjectKind : byte { PepeAscendingTomato, CarmeChicken }

    /// <summary>FishNet Pro predicted spawning for owner-created ultimate objects.</summary>
    public sealed class NetworkedPredictedAbilitySpawn : PredictedSpawn
    {
        [SerializeField] private PredictedAbilityObjectKind _kind;
        private bool _configured;
        private NetworkObject _caster;
        private Vector3 _positionOrTarget;
        private Vector3 _direction;

        public void ConfigurePepe(NetworkObject caster, Vector3 spawnPosition)
        { _configured = true; _caster = caster; _positionOrTarget = spawnPosition; }

        public void ConfigureCarme(NetworkObject caster, Vector3 landPoint, Vector3 direction)
        { _configured = true; _caster = caster; _positionOrTarget = landPoint; _direction = direction; }

        public override void WritePayload(NetworkConnection connection, Writer writer)
        {
            writer.WriteBoolean(_configured);
            if (!_configured) return;
            writer.WriteNetworkObject(_caster);
            writer.WriteVector3(_positionOrTarget);
            writer.WriteVector3(_direction);
        }

        public override void ReadPayload(NetworkConnection connection, Reader reader)
        {
            _configured = reader.ReadBoolean();
            if (!_configured) return;
            _caster = reader.ReadNetworkObject();
            _positionOrTarget = reader.ReadVector3();
            _direction = reader.ReadVector3();
        }

        public override bool OnTrySpawnServer(NetworkConnection spawner, NetworkConnection owner = null)
        {
            if (!_configured || _caster == null || spawner == null || owner != spawner || _caster.Owner != spawner)
            {
                MpCanary.Mark($"PredictedAbility REJECT identity kind={_kind} spawner={spawner?.ClientId}");
                return false;
            }
            var ability = _caster.GetComponentInChildren<NetworkedUniqueAbility>(true);
            if (ability == null || !ability.TryConsumePredictedObjectSpawnPermit(spawner))
            {
                MpCanary.Mark($"PredictedAbility REJECT permit kind={_kind} spawner={spawner.ClientId}");
                return false;
            }

            if (_kind == PredictedAbilityObjectKind.PepeAscendingTomato)
            {
                var pepe = _caster.GetComponentInChildren<NetworkedPepeUniqueAbility>(true);
                bool accepted = pepe != null && pepe.InitializeValidatedPredictedTomato(
                    GetComponent<NetworkedAscendingTomato>(), _positionOrTarget);
                MpCanary.Mark($"PredictedAbility {(accepted ? "ACCEPT" : "REJECT payload")} kind={_kind} spawner={spawner.ClientId}");
                return accepted;
            }

            var carme = _caster.GetComponentInChildren<NetworkedCarmeUniqueAbility>(true);
            bool chickenAccepted = carme != null && carme.InitializeValidatedPredictedChicken(
                GetComponent<NetworkedMcChickenBehaviour>(), _positionOrTarget, _direction);
            MpCanary.Mark($"PredictedAbility {(chickenAccepted ? "ACCEPT" : "REJECT payload")} kind={_kind} spawner={spawner.ClientId}");
            return chickenAccepted;
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            if (!_configured) return;
            if (_kind == PredictedAbilityObjectKind.PepeAscendingTomato)
            {
                var tomato = GetComponent<NetworkedAscendingTomato>();
                if (tomato != null && !IsServerInitialized)
                {
                    var pepe = _caster != null ? _caster.GetComponentInChildren<NetworkedPepeUniqueAbility>(true) : null;
                    // Client values are only visual prediction; the server overwrites authoritative state.
                    if (pepe != null) tomato.transform.position = _positionOrTarget;
                }
            }
            else
                GetComponent<NetworkedMcChickenBehaviour>()?.InitializePredictedClient(_positionOrTarget, _direction);
        }
    }
}
