using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Utilities
{
    public class CameraHolder : MonoBehaviour
    {
        [SerializeField] Camera cameraToHold;

        public Camera CameraToHold { get => cameraToHold; set => cameraToHold = value; }

        private void Start()
        {
            if (!cameraToHold)
            {
                cameraToHold = Camera.main;
            }
        }
    }
}