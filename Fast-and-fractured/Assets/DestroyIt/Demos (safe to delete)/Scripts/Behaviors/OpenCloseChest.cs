using UnityEngine;

namespace DestroyIt
{
    public class OpenCloseChest : MonoBehaviour
    {
        void Start()
        {
            InvokeRepeating("SwapOpenClose", 3.5f, 3.5f);
        }

        public void SwapOpenClose()
        {
            HingeJoint joint = this.GetComponent<HingeJoint>();
            if (joint != null)
            {
                
                joint.motor = new JointMotor()
                {
                    targetVelocity = -1 * joint.motor.targetVelocity,
                    force = 10
                };

                joint.useMotor = true;
                GetComponent<Rigidbody>().WakeUp();
            }
            else
                Destroy(this);
        }
    }
}