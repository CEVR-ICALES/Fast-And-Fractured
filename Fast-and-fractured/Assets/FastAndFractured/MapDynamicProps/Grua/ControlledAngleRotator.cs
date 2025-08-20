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
        private float startY;
        private float targetY;

        public int CurrentPointIndex => currentPointIndex;

        private void Start()
        {
            RotateToPoint(1);
        }

        public void RotateToPoint(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= rotationPoints.Length || pointIndex == currentPointIndex) return;

            Quaternion startRotation = objectToRotate.rotation;

            Vector3 direction = rotationPoints[pointIndex].position - objectToRotate.position;
            if (direction == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);

            TimerSystem.Instance.CreateTimer(
                rotationDuration,
                TimerDirection.INCREASE,
                onTimerIncreaseComplete: () =>
                {
                    objectToRotate.rotation = targetRotation;
                    currentPointIndex = pointIndex;
                },
                onTimerIncreaseUpdate: (progress) =>
                {
                    objectToRotate.rotation = Quaternion.Lerp(startRotation, targetRotation, progress / 10);
                }
            );
        }
    }
}
