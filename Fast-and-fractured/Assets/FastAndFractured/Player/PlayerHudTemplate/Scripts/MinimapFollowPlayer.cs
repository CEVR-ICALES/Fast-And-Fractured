using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    public class MinimapFollowPlayer : MonoBehaviour
    {
        [SerializeField] private GameObject player;
        [SerializeField] private float cameraSize = 80f;
        void Start()
        {
            Camera camera = GetComponent<Camera>();
            camera.orthographicSize = cameraSize;
            //TODO when player spawn at beggining of game set variable player to player object
        }

        void Update()
        {
            Vector3 newPosition = player.transform.position;
            newPosition.y = transform.position.y;
            transform.position = newPosition; 
            transform.rotation = Quaternion.Euler(90, player.transform.eulerAngles.y, 0);
            
        }
    }
}