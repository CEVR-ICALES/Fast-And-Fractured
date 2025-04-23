using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SandstormDirectionMinimap : MonoBehaviour
    {
        private GameObject _player;
        private Vector3 _sandstormDirection;
        private bool _isDataReceived = false;
        [SerializeField] private GameObject arrowIcon;
        void Start()
        {
            arrowIcon.SetActive(false);
        }

        void Update()
        {
            if(_isDataReceived)
            {
                Vector3 playerForward = _player.transform.forward;
                float angle = -Vector3.SignedAngle(playerForward, _sandstormDirection, Vector3.up);
                transform.rotation = Quaternion.Euler(0, 0, angle);     
            }
        }
        public void SetSandstormDirection(Vector3 direction)
        {
            _player = LevelController.Instance.playerReference;
            _sandstormDirection = direction;
            _isDataReceived = true;
            arrowIcon.SetActive(true);
        }
    }
}