using FastAndFractured;
using System;
using UnityEngine;
using UnityEngine.Events;
using Utilities;
using Enums;

namespace FastAndFractured
{
    public class StatsBoostInteractable : GenericInteractable
    {
        [SerializeField] private StatsBoost[] boostList;
        public StatsBoost[] BoostList => boostList;
        private const float PERMANENT_BOOST_VALUE = -1;
        public string ingameEventText;
        public float ingameEventTimeOnScreen = 2f;

        public override void OnInteract(GameObject interactionFrom, GameObject intearactionTo)
        {
            StatsController statsController = interactionFrom.GetComponentInParent<StatsController>();
            GameObject player = LevelController.Instance.playerReference;
            if (interactionFrom == player)
            {
                IngameEventsManager.Instance.CreateEvent(ingameEventText, ingameEventTimeOnScreen);
            }
            if (!statsController) return;

            base.OnInteract(interactionFrom, intearactionTo);


            foreach (var boost in boostList)
            {
                switch (boost.StatToBoost)
                {
                    case Stats.ENDURANCE:
                        if (boost.BoostValue < 0)
                        {
                            statsController.TakeEndurance(boost.BoostValue, false,this.gameObject);
                        }
                        else
                        {
                            statsController.RecoverEndurance(boost.BoostValue, false);
                        }
                        break;
                    default:
                        statsController.UpgradeCharStat(boost.StatToBoost, boost.BoostValue);
                        break;
                }

                if (boost.StatToBoost == Stats.COOLDOWN_SPEED)
                {
                    UpdateExistingCooldowns(interactionFrom, statsController.CooldownSpeed);
                }

                boost.OnBoostStartEvent?.Invoke();
                if (boost.BoostTime == PERMANENT_BOOST_VALUE) return;
                TimerSystem.Instance.CreateTimer(boost.BoostTime, onTimerDecreaseComplete: () =>
                {
                    switch (boost.StatToBoost)
                    {
                        case Stats.ENDURANCE:
                            if (boost.BoostValue < 0)
                            {
                                statsController.RecoverEndurance(boost.BoostValue, false);
                            }
                            else
                            {
                                statsController.TakeEndurance(boost.BoostValue, false,this.gameObject);
                            }
                            break;
                        default:
                            statsController.ReduceCharStat(boost.StatToBoost, boost.BoostValue);
                            break;
                    }

                    if (boost.StatToBoost == Stats.COOLDOWN_SPEED)
                    {
                        UpdateExistingCooldowns(interactionFrom, statsController.CooldownSpeed);
                    }

                    boost.OnBoostEndEvent?.Invoke();
                });
                
            }

            onInteractEmpty?.Invoke();
            onInteract?.Invoke(interactionFrom, intearactionTo);
        }

        private void UpdateExistingCooldowns(GameObject character, float speed)
        {
            ITimeSpeedModifiable[] cooldowns = character.GetComponentsInChildren<ITimeSpeedModifiable>();
            foreach (ITimeSpeedModifiable cd in cooldowns)
            {
                cd.ModifySpeedOfExistingTimer(speed);
            }
        }

        [Serializable]
        public class StatsBoost
        {
            [SerializeField] private Stats _statToBoost;
            [SerializeField] private float _boostValue = -1;

            [Tooltip("Use -1 if you want the boost to be permanent")]
            [SerializeField]
            private float _boostTime;

            [SerializeField] UnityEvent _onBoostStartEvent;
            [SerializeField] UnityEvent _onBoostEndEvent;

            public Stats StatToBoost
            {
                get => _statToBoost;
                set => _statToBoost = value;
            }

            public float BoostValue
            {
                get => _boostValue;
                set => _boostValue = value;
            }

            public float BoostTime
            {
                get => _boostTime;
                set => _boostTime = value;
            }

            public UnityEvent OnBoostStartEvent
            {
                get => _onBoostStartEvent;
                set => _onBoostStartEvent = value;
            }

            public UnityEvent OnBoostEndEvent
            {
                get => _onBoostEndEvent;
                set => _onBoostEndEvent = value;
            }
        }
    }
}
