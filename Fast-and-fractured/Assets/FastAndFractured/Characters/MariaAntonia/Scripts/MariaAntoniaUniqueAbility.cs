using FastAndFractured;
using FMODUnity;
using UnityEngine;
using Utilities;

public class MariaAntoniaUniqueAbility : BaseUniqueAbility
{
    #region Variables
    [Tooltip("Multiplier to reduce the cooldown timers (example: 1.5 increases the cooldown timer by 50%")]
    public float cooldownReductionMultiplier;

    [Tooltip("Multiplier to increase the stats (example: 1.2 increases the stats by 20%")]
    public float statBoostMultiplier;

    [Tooltip("Unique ability duration in seconds")]
    public float uniqueAbilityDuration;

    public EventReference ssjUltiReference;

    [SerializeField] private StatsController _statsController;
    #endregion

    public override void ActivateAbility()
    {
        if (IsAbilityActive || IsOnCooldown)
            return;

        base.ActivateAbility();
        Debug.Log("Boosted Stats!");

        SoundManager.Instance.PlayOneShot(ssjUltiReference, transform.position);

        //_statsController = GetComponent<StatsController>();

        if (_statsController == null)
        {
            Debug.Log("Stats Controller not Found");
            return;
        }

        Debug.Log("After return");

        _statsController.TemporalProductStat(Enums.Stats.COOLDOWN_SPEED, cooldownReductionMultiplier, uniqueAbilityDuration);

        _statsController.TemporalProductStat(Enums.Stats.MAX_SPEED, statBoostMultiplier, uniqueAbilityDuration);
        _statsController.TemporalProductStat(Enums.Stats.ACCELERATION, statBoostMultiplier, uniqueAbilityDuration);
        _statsController.TemporalProductStat(Enums.Stats.NORMAL_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
        _statsController.TemporalProductStat(Enums.Stats.PUSH_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
    }
}