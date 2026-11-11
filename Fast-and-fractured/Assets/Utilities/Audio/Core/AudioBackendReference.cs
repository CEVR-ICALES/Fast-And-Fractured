using System;

namespace Utilities.Audio
{
    public readonly struct AudioBackendReference : IEquatable<AudioBackendReference>
    {
        public string StableId { get; }
        public string Path { get; }
        public bool IsEmpty => string.IsNullOrEmpty(StableId) && string.IsNullOrEmpty(Path);

        public AudioBackendReference(string stableId, string path)
        {
            StableId = stableId ?? "";
            Path = path ?? "";
        }

        public bool Equals(AudioBackendReference other)
        {
            if (!string.IsNullOrEmpty(StableId) || !string.IsNullOrEmpty(other.StableId))
                return string.Equals(StableId, other.StableId, StringComparison.Ordinal);
            return string.Equals(Path, other.Path, StringComparison.Ordinal);
        }
        public override bool Equals(object obj) => obj is AudioBackendReference other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(!string.IsNullOrEmpty(StableId) ? StableId : Path ?? "");
        public override string ToString() => !string.IsNullOrEmpty(StableId) ? StableId : Path ?? "";
    }
}
