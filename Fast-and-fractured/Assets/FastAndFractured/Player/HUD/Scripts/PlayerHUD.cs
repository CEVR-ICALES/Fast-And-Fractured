using Enums;
using FastAndFractured.Core;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class PlayerHUD : AbstractAutoInitializableMonoBehaviour
    {
        [SerializeField] NormalShootHandle normalShootHandle;
        [SerializeField] PushShootHandle pushShootHandle;
        [SerializeField] CarMovementController carMovementController;
        [SerializeField] BaseUniqueAbility uniqueAbility;
        protected override void Construct()
        {
            GetComponentsNeededInChildren();
        }
        protected override void Initialize()
        {
            base.Initialize();
            GetComponentsNeededInChildren();
            LevelControllerButBetter.Instance.onLevelPreStart.AddListener(InitUpdateHUDEvents);
            
        }
        private void GetComponentsNeededInChildren()
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
                carMovementController = GetComponentInChildren<CarMovementController>();
            }
            if (!uniqueAbility)
            {
                uniqueAbility = GetComponentInChildren<BaseUniqueAbility>();
            }
        }
        private void InitUpdateHUDEvents()
        {
            GetComponentsNeededInChildren();
            normalShootHandle.onOverheatUpdate?.AddListener(UpdateOverheatHUD);
            pushShootHandle.onCooldownUpdate?.AddListener(UpdatePushCooldownHUD);
            carMovementController.onDashCooldownUpdate?.AddListener(UpdateDashCooldownHUD);
            uniqueAbility.onCooldownUpdate?.AddListener(UpdateUniqueAbilityCooldownHUD);
        }
        private void OnDisable()
        {
            if (normalShootHandle && normalShootHandle.onOverheatUpdate != null)
            {
                normalShootHandle.onOverheatUpdate?.RemoveListener(UpdateOverheatHUD);
                pushShootHandle.onCooldownUpdate?.RemoveListener(UpdatePushCooldownHUD);
                carMovementController.onDashCooldownUpdate?.RemoveListener(UpdateDashCooldownHUD);
                uniqueAbility.onCooldownUpdate?.RemoveListener(UpdateUniqueAbilityCooldownHUD);
            }
        }

        private void UpdateOverheatHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIDynamicElementType.SHOOT_COOLDOWN, currentValue, maxValue);
        }

        private void UpdatePushCooldownHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIDynamicElementType.PUSH_COOLDOWN, currentValue, maxValue);
        }

        private void UpdateDashCooldownHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIDynamicElementType.DASH_COOLDOWN, currentValue, maxValue);
        }

        private void UpdateUniqueAbilityCooldownHUD(float currentValue, float maxValue)
        {
            HUDManager.Instance.UpdateUIElement(UIDynamicElementType.ULT_COOLDOWN, currentValue, maxValue);
        }

    }
}

