using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities;
using Enums;
namespace FastAndFractured
{
    public class MinimapFollowPlayer : MonoBehaviour
    {
        [SerializeField] private GameObject player;
        [SerializeField] private float cameraSize = 80f;
        [SerializeField] private GameObject characterIconMinimap;
        private bool isPlayerReceived = false;
        void Start()
        {
            Camera camera = GetComponent<Camera>();
            camera.orthographicSize = cameraSize;
        }
        void OnEnable()
        {
            if (LevelController.Instance != null)
            {
                LevelController.Instance.charactersCustomStart.AddListener(OnCharactersCustomStart);
            }
        }

        void OnDestroy()
        {
            if (LevelController.Instance != null)
            {
                LevelController.Instance.charactersCustomStart.RemoveListener(OnCharactersCustomStart);
            }
        }
        
        void Update()
        {
            if (player != null && isPlayerReceived)
            {
                Vector3 newPosition = player.transform.position;
                newPosition.y = transform.position.y;
                transform.position = newPosition;
                Camera mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    Vector3 forward = mainCamera.transform.forward;
                    Quaternion newRotation = Quaternion.LookRotation(forward, Vector3.up);
                    transform.rotation = Quaternion.Euler(90, newRotation.eulerAngles.y, 0);
                }
                characterIconMinimap.transform.rotation = Quaternion.Euler(90, player.transform.eulerAngles.y, 0);
            }
            
        }
        private void OnCharactersCustomStart()
        {
            if (!isPlayerReceived)
            {
                player = LevelController.Instance.playerReference;
                isPlayerReceived = true;
                string icon = PlayerPrefs.GetString("Selected_Player");
                characterIconMinimap.GetComponent<Image>().sprite = ResourcesManager.Instance.GetResourcesSprite(icon);
            }
        }
    }
}