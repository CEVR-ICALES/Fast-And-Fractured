using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    public class SandstormSpawnPoint : MonoBehaviour
    {
        public Vector3 OwnPosition { get => transform.position; }
        public Vector3 MirrorPosition { get => mirrorTransform.OwnPosition; }
        [SerializeField]
        private SandstormSpawnPoint mirrorTransform;
        [SerializeField]
        private Vector3 initialSandstormSize = new Vector3(862.4f,113.25f,48.2f);
    }
}
