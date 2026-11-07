using UnityEngine;

namespace FastAndFractured.Tests
{
    public sealed class AudioReferenceFixture : ScriptableObject
    {
        public AudioClip clip;
        public string cueId;
        public FMODUnity.EventReference nativeSound;
    }
}
