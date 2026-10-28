using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities;
using Enums;
using DG.Tweening;
namespace FastAndFractured
{
    public class MinimapFollowPlayer : MonoBehaviour
    {
        private GameObject _player;
        [SerializeField] private float cameraSize = 80f;
        [SerializeField] private GameObject characterIconMinimap;
        private bool _isPlayerReceived = false;
        private Camera _cameraReference;
        private const int IMAGE_X_ROTATION = 90;
        private const int IMAGE_Z_ROTATION = 0;
        void Start()
        {
            Camera camera = GetComponent<Camera>();
            camera.orthographicSize = cameraSize;
        }
        void OnEnable()
        {
            if (LevelController.Instance != null)
            {
                LevelController.Instance.onLevelPreStart.AddListener(OnCharactersCustomStart);
            }
        }

        void OnDestroy()
        {
            if (LevelController.Instance != null)
            {
                LevelController.Instance.onLevelPreStart.RemoveListener(OnCharactersCustomStart);
            }
        }
        
        void Update()
        {
            // The player car can exist before this subscribes to onLevelPreStart (it already fired): pick it up here.
            if (!_isPlayerReceived && LevelController.HasInstance && LevelController.Instance.LocalPlayer != null)
                OnCharactersCustomStart();

            if (_player != null && _isPlayerReceived)
            {
                Vector3 newPosition = _player.transform.position;
                newPosition.y = transform.position.y;
                transform.position = newPosition;
                
                if (_cameraReference != null)
                {
                    Vector3 forward = _cameraReference.transform.forward;
                    Quaternion newRotation = Quaternion.LookRotation(forward, Vector3.up);
                    transform.rotation = Quaternion.Euler(IMAGE_X_ROTATION, newRotation.eulerAngles.y, IMAGE_Z_ROTATION);
                }
                characterIconMinimap.transform.rotation = Quaternion.Euler(IMAGE_X_ROTATION, _player.transform.eulerAngles.y, IMAGE_Z_ROTATION);
            }
            
        }
        private void OnCharactersCustomStart()
        {
            if (!_isPlayerReceived)
            {
                _player = LevelController.Instance.LocalPlayer;
                if (_player == null) return;
                _isPlayerReceived = true;
                string icon = PlayerPrefs.GetString("Selected_Player");
                if (_player != null)
                {
                    characterIconMinimap.GetComponent<Image>().sprite = ResourcesManager.Instance.GetResourcesSprite(icon);
                    CameraHolder holder = _player.transform.parent != null ? _player.transform.parent.GetComponent<CameraHolder>() : null;
                    _cameraReference = holder != null && holder.CameraToHold != null ? holder.CameraToHold : Camera.main;
                }
            }
        }
    }
}
