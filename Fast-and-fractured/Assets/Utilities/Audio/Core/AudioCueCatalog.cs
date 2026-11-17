using System;
using System.Collections.Generic;
using UnityEngine;

namespace Utilities.Audio
{
    [CreateAssetMenu(menuName = "Audio/Cue Catalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string id;
            public string displayName;
            public string codeName;
            public string backendCueId;
            public string backendStableId;
        }

        [SerializeField] private string backendId;
        [SerializeField] private List<Entry> entries = new List<Entry>();
        public string BackendId => backendId;
        public void SetBackendId(string value) => backendId = value;
        private Dictionary<string, Entry> _lookup;
        private HashSet<string> _duplicates;
        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetEntry(AudioCueId id, out Entry entry)
        {
            EnsureLookup();
            entry = null;
            return !id.IsEmpty && !_duplicates.Contains(id.Value) && _lookup.TryGetValue(id.Value, out entry);
        }

        public AudioBackendReference ResolveBinding(AudioCueId id) => TryGetEntry(id, out var entry)
            ? new AudioBackendReference(entry.backendStableId, entry.backendCueId) : default;

        public string Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLookup();
            if (_duplicates.Contains(id)) return null;
            return _lookup.TryGetValue(id, out var entry) ? entry.backendCueId : id;
        }

        public bool AddMissing(string id, string displayName, string backendCueId)
        {
            EnsureLookup();
            if (string.IsNullOrEmpty(id) || _lookup.ContainsKey(id)) return false;
            var entry = new Entry { id = id, displayName = displayName, backendCueId = backendCueId };
            entries.Add(entry);
            _lookup.Add(id, entry);
            return true;
        }

        public void Invalidate()
        {
            _lookup = null;
            _duplicates = null;
        }

        private void OnValidate() => Invalidate();
        private void OnEnable() => Invalidate();

        private void EnsureLookup()
        {
            if (_lookup != null) return;
            _lookup = new Dictionary<string, Entry>(StringComparer.Ordinal);
            _duplicates = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                if (_lookup.ContainsKey(entry.id)) _duplicates.Add(entry.id);
                else _lookup.Add(entry.id, entry);
            }
        }
    }
}
