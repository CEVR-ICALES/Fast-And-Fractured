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

        void Update()
        {
            if(isPlayerReceived == false)
            {
                player = LevelController.Instance.playerReference;
                isPlayerReceived = true;
                // Set the sprite of the character icon in the minimap
                string icon = PlayerPrefs.GetString("Selected_Player");
                Debug.Log("Selected Player: " + icon);
                characterIconMinimap.GetComponent<Image>().sprite = ResourcesManager.Instance.GetResourcesSprite(icon);
            }
            Vector3 newPosition = player.transform.position;
            newPosition.y = transform.position.y;
            transform.position = newPosition; 
            transform.rotation = Quaternion.Euler(90, player.transform.eulerAngles.y, 0);
            
        }
    }
}