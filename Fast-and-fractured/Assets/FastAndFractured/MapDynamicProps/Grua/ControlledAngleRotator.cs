using UnityEngine;
using Utilities;
using Enums;

namespace FastandFractured
{
    public class ControlledAngleRotator : MonoBehaviour
    {
        [SerializeField] private Transform[] rotationPoints;
        [SerializeField] private float rotationDuration = 10f;
        [SerializeField] private Transform objectToRotate;

        private int currentPointIndex = 0;
        private Quaternion startRotation;
        private Quaternion targetRotation;

        public int CurrentPointIndex => currentPointIndex;

        private void Start()
        {
            RotateToPoint(2);
        }

        public void RotateToPoint(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= rotationPoints.Length || pointIndex == currentPointIndex) return;

            startRotation = objectToRotate.rotation;

            Vector3 targetPoint = rotationPoints[pointIndex].position;
            targetRotation = Quaternion.LookRotation(targetPoint - objectToRotate.position);

            TimerSystem.Instance.CreateTimer(
                rotationDuration,
                TimerDirection.INCREASE,
                onTimerIncreaseComplete: () =>
                {
                    currentPointIndex = pointIndex;
                },
                onTimerIncreaseUpdate: (progress) =>
                {
                    objectToRotate.rotation = Quaternion.Slerp(startRotation, targetRotation, progress);
                }
            );
        }
    }
}
