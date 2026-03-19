using System.Collections;
using System.Collections.Generic;
using FastAndFractured;
using UnityEngine;
namespace Utilities
{
    public class CameraHolder : MonoBehaviour
    {
        [SerializeField] Camera cameraToHold;
        [SerializeField] TurretRotationMovement TurretRotationMovement;

        public Camera CameraToHold { get => cameraToHold; set => cameraToHold = value; }

        private void Awake()
        {
            if (!cameraToHold)
            {
                cameraToHold = Camera.main;
            }
        }
        private void Start()
        {
            if (TurretRotationMovement == null)
            {
                TurretRotationMovement = GetComponentInChildren<TurretRotationMovement>();
                TurretRotationMovement.TargetDirection = ReturnCurrentLookAtFromCamera();
            }
        }

        private void Update()
        {
            if (TurretRotationMovement != null)
            {
                TurretRotationMovement.TargetDirection = ReturnCurrentLookAtFromCamera();
            }
        }

        public Vector3 ReturnCurrentLookAtFromCamera()
        {
            return cameraToHold.transform.forward;
        } 
    }
}