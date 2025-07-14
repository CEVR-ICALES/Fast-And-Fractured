using UnityEngine;
using System.Collections.Generic;

namespace DestroyIt
{
    public class SmoothMouseLook : MonoBehaviour
    {
        // Public variables
        public float sensitivityX = 2.0f;
        public float sensitivityY = 2.0f;
        public float minimumY = -60f;
        public float maximumY = 60f;
        public int frameCounterX = 20;
        public int frameCounterY = 20;

        // Private variables
        private float rotationX;
        private float rotationY;
        private Quaternion xQuaternion;
        private Quaternion yQuaternion;
        private Quaternion origRotation;
        private List<float> rotationsX = new List<float>();
        private List<float> rotationsY = new List<float>();

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;

            if (GetComponent<Rigidbody>())
                GetComponent<Rigidbody>().freezeRotation = true;

            origRotation = transform.localRotation;
        }

        void Update()
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // Add X rotation
                rotationX += Input.GetAxis("Mouse X") * sensitivityX;
                rotationsX.Add(rotationX);

                // Add Y rotation
                rotationY += Input.GetAxis("Mouse Y") * sensitivityY;
                rotationY = ClampAngle(rotationY, minimumY, maximumY);
                rotationsY.Add(rotationY);

                // Average rotations
                float rotAverageX = AverageRotations(rotationsX, frameCounterX);
                float rotAverageY = AverageRotations(rotationsY, frameCounterY);

                // Perform rotation based on averages
                xQuaternion = Quaternion.AngleAxis(rotAverageX, Vector3.up);
                yQuaternion = Quaternion.AngleAxis(rotAverageY, Vector3.left);
                transform.localRotation = origRotation * xQuaternion * yQuaternion;
            }
            else
                Cursor.visible = true;
        }

        private static float AverageRotations(List<float> rotations, int frameCounter)
        {
            float rotAverage = 0f;

            if (rotations.Count >= frameCounter)
                rotations.RemoveAt(0);

            for (int i = 0; i < rotations.Count; i++)
                rotAverage += rotations[i];

            rotAverage /= rotations.Count;
            return rotAverage;
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f)
                angle += 360f;
            if (angle > 360f)
                angle -= 360f;

            return Mathf.Clamp(angle, min, max);
        }
    }
}