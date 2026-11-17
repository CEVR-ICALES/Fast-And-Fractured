using UnityEngine;

namespace FastAndFractured.Multiplayer
{
    public sealed class PredictedPoseHistory
    {
        public struct Sample
        {
            public uint Tick;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Velocity;
            public Vector3 AngularVelocity;
        }

        private readonly Sample[] _samples;

        public PredictedPoseHistory(int capacity) => _samples = new Sample[Mathf.Max(1, capacity)];

        public void Record(uint tick, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            if (tick == 0) return;
            _samples[tick % _samples.Length] = new Sample
            {
                Tick = tick,
                Position = position,
                Rotation = rotation,
                Velocity = velocity,
                AngularVelocity = angularVelocity,
            };
        }

        public bool TryGet(uint tick, out Sample sample)
        {
            sample = _samples[tick % _samples.Length];
            return tick != 0 && sample.Tick == tick;
        }

        // Without this the next server state would see the same error again and correct it twice.
        public void ShiftAfter(uint tick, Vector3 position, Quaternion rotation, Vector3 velocity, Vector3 angularVelocity)
        {
            for (int i = 0; i < _samples.Length; i++)
            {
                if (_samples[i].Tick <= tick) continue;
                _samples[i].Position += position;
                _samples[i].Rotation = rotation * _samples[i].Rotation;
                _samples[i].Velocity += velocity;
                _samples[i].AngularVelocity += angularVelocity;
            }
        }

        public void Clear()
        {
            for (int i = 0; i < _samples.Length; i++) _samples[i] = default;
        }
    }
}
