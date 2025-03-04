using UnityEngine;

public class BasicMovement : MonoBehaviour
{
    public float speed = 5f;
    private CharacterController characterController;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
    }

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 move = transform.forward * vertical + transform.right * horizontal;
        characterController.Move(move * speed * Time.deltaTime);
    }
}
