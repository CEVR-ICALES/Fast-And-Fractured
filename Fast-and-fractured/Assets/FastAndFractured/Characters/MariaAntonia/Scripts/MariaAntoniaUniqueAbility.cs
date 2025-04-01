using FMODUnity;
using Utilities;
using UnityEngine;
using System.Collections.Generic;

namespace FastAndFractured
{
    public class MariaAntoniaUniqueAbility : BaseUniqueAbility
    {
        #region Variables
        [SerializeField] private StatsController _statsController;

        [Tooltip("Multiplier to reduce the cooldown timers (example: 1.5 increases the cooldown timer by 50%")]
        public float cooldownReductionMultiplier;

        [Tooltip("Multiplier to increase the stats (example: 1.2 increases the stats by 20%")]
        public float statBoostMultiplier;

        [Tooltip("Unique ability duration in seconds")]
        public float uniqueAbilityDuration;

        public GameObject croquetasGameObject;

        public int numberOfCroquetas;

        private float _orbitRadius;

        private float _orbitSpeed;

        private List<GameObject> croquetaList = new List<GameObject> ();

        public EventReference ssjUltiReference;

        #endregion

        public override void ActivateAbility()
        {
            if (IsAbilityActive || IsOnCooldown)
                return;

            base.ActivateAbility();

            SoundManager.Instance.PlayOneShot(ssjUltiReference, transform.position);

            if (_statsController == null)
            {
                Debug.Log("Stats Controller not Found");
                return;
            }

            _statsController.TemporalProductStat(Enums.Stats.COOLDOWN_SPEED, cooldownReductionMultiplier, uniqueAbilityDuration);

            _statsController.TemporalProductStat(Enums.Stats.MAX_SPEED, statBoostMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.ACCELERATION, statBoostMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.NORMAL_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
            _statsController.TemporalProductStat(Enums.Stats.PUSH_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
        }
    }
}