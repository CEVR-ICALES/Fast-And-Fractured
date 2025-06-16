using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;

namespace FastAndFractured
{
    public class SandstormDirectionMinimap : MonoBehaviour
    {
        private GameObject _player;
        private Vector3 _sandstormDirection;
        private bool _isDataReceived = false;
        [SerializeField] private GameObject arrowIcon;
        private Camera _cameraReference;
        void Start()
        {
            arrowIcon.SetActive(false);
        }

        void Update()
        {
            if (_isDataReceived)
            {
                if (_cameraReference != null)
                {
                    Vector3 cameraForward = _cameraReference.transform.forward;
                    float angle = -Vector3.SignedAngle(cameraForward, _sandstormDirection, Vector3.up);
                    transform.rotation = Quaternion.Euler(0, 0, angle);
                }
            }
        }
        public void SetSandstormDirection(Vector3 direction)
        {
            _player = LevelControllerButBetter.Instance.LocalPlayer;
            _sandstormDirection = direction;
            _isDataReceived = true;
            arrowIcon.SetActive(true);
            _cameraReference = _player.transform.parent.GetComponent<CameraHolder>().CameraToHold;
        }
    }
}