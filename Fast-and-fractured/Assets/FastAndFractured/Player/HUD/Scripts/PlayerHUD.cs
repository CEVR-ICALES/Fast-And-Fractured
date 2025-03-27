using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class PlayerHUD : MonoBehaviour
    {
        [SerializeField] NormalShootHandle normalShootHandle;
        [SerializeField] PushShootHandle pushShootHandle;
        [SerializeField] CarMovementController carMovementController;
        private void Awake()
        {
            if (!normalShootHandle)
            {
                normalShootHandle = GetComponentInChildren<NormalShootHandle>();
            }
            if (!pushShootHandle)
            {
                pushShootHandle = GetComponentInChildren<PushShootHandle>();
            }
            if (!carMovementController)
            {
                carMovementController = GetComponent<CarMovementController>();
            }
        }

        private void OnEnable()
        {
            normalShootHandle.onOverheatUpdate.AddListener(UpdateOverheatHUD);
            pushShootHandle.onCooldownUpdate.AddListener(UpdatePushCooldownHUD);
            carMovementController.onDashCooldownUpdate.AddListener(UpdateDashCooldownHUD);
        }
        private void OnDisable()
        {
            normalShootHandle.onOverheatUpdate.RemoveListener(UpdateOverheatHUD);
            pushShootHandle.onCooldownUpdate.AddListener(UpdatePushCooldownHUD);
            carMovementController.onDashCooldownUpdate.AddListener(UpdateDashCooldownHUD);

        }

        private void UpdateOverheatHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIElementType.ShootCooldown, currentValue, maxValue);
        }

        private void UpdatePushCooldownHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIElementType.PushCooldown, currentValue, maxValue);
        }

        private void UpdateDashCooldownHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIElementType.DashCooldown, currentValue, maxValue);
        }

    }
}

