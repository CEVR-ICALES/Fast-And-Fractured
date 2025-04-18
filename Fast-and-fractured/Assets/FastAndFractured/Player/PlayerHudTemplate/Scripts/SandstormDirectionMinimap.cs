using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class SandstormDirectionMinimap : MonoBehaviour
    {
        [SerializeField] private SandstormController sandstormController;
        [SerializeField] private GameObject player;
        [SerializeField] private Vector3 sandstormDirection;
        [SerializeField] private bool isDataReceived = false;
        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            sandstormDirection = sandstormController.direction;
            if(isDataReceived == false)
            {
                player = LevelController.Instance.playerReference;
                isDataReceived = true;
                
            }
            Vector3 playerForward = player.transform.forward;
            float angle = -Vector3.SignedAngle(playerForward, sandstormDirection, Vector3.up);
            transform.rotation = Quaternion.Euler(0, 0, angle);
        }
    }
}