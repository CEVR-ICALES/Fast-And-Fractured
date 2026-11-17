using System;
using UnityEngine;

namespace Utilities.Audio
{
    [Serializable]
    public struct AudioCueId : IEquatable<AudioCueId>
    {
        [SerializeField] private string value;
        public string Value => value ?? "";
        public bool IsEmpty => string.IsNullOrEmpty(value);
        public AudioCueId(string value) { this.value = value ?? ""; }
        public bool Equals(AudioCueId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is AudioCueId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(AudioCueId a, AudioCueId b) => a.Equals(b);
        public static bool operator !=(AudioCueId a, AudioCueId b) => !a.Equals(b);
    }
}
