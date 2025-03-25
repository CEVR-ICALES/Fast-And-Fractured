using FastAndFractured;
using System;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

namespace FastAndFractured
{
    public class StatsBoostInteractable : GenericInteractable
    {
        [SerializeField] private StatsBoost[] boostList;
        private const float PERMANENT_BOOST_VALUE = -1;

        public override void OnInteract(GameObject interactionFrom, GameObject intearactionTo)
        {
            StatsController statsController = interactionFrom.GetComponentInParent<StatsController>();
            if (!statsController) return;

            base.OnInteract(interactionFrom, intearactionTo);


            foreach (var boost in boostList)
            {
                if (boost.StatToBoost == STATS.ENDURANCE)
                {
                    if (boost.BoostValue < 0)
                    {
                        statsController.RecoverEndurance(boost.BoostValue, false);
                    }
                    else
                    {
                        statsController.TakeEndurance(boost.BoostValue, false);
                    }
                }
                else
                {
                    statsController.UpgradeCharStat(boost.StatToBoost, boost.BoostValue);
                }


                boost.OnBoostStartEvent?.Invoke();
                if (boost.BoostTime == PERMANENT_BOOST_VALUE) return;
                TimerSystem.Instance.CreateTimer(boost.BoostTime, onTimerDecreaseComplete: () =>
                {
                    if (boost.StatToBoost == STATS.ENDURANCE)
                    {
                        if (boost.BoostValue < 0)
                        {
                            statsController.TakeEndurance(boost.BoostValue, false);
                        }
                        else
                        {
                            statsController.RecoverEndurance(boost.BoostValue, false);
                        }
                    }
                    else
                    {
                        statsController.ReduceCharStat(boost.StatToBoost, boost.BoostValue);
                    }

                    boost.OnBoostEndEvent?.Invoke();
                });
            }

            onInteractEmpty?.Invoke();
            onInteract?.Invoke(interactionFrom, intearactionTo);
        }


        [Serializable]
        public class StatsBoost
        {
            [SerializeField] private STATS _statToBoost;
            [SerializeField] private float _boostValue = -1;

            [Tooltip("Use -1 if you want the boost to be permanent")]
            [SerializeField]
            private float _boostTime;

            [SerializeField] UnityEvent _onBoostStartEvent;
            [SerializeField] UnityEvent _onBoostEndEvent;

            public STATS StatToBoost
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
