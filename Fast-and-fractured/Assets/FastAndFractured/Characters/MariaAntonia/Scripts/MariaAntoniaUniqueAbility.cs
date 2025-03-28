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

    private StatsController _statsController;
    #endregion

    void Start()
    {
        ssjUltiReference = new EventReference
        {
            Guid = new FMOD.GUID { Data1 = 526829551, Data2 = 1239381257, Data3 = -1023862356, Data4 = -154867332 }
        };
    }

    public override void ActivateAbility()
    {
        if (IsAbilityActive || IsOnCooldown)
            return;

        base.ActivateAbility();

        SoundManager.Instance.PlayOneShot(ssjUltiReference, transform.position);

        _statsController = GetComponentInParent<StatsController>();

        if (_statsController == null)
        {
            Debug.LogError("StatsController not found");
            return;
        }

        _statsController.TemporalProductStat(Enums.Stats.COOLDOWN_SPEED, cooldownReductionMultiplier, uniqueAbilityDuration);

        _statsController.TemporalProductStat(Enums.Stats.MAX_SPEED, statBoostMultiplier, uniqueAbilityDuration);
        _statsController.TemporalProductStat(Enums.Stats.ACCELERATION, statBoostMultiplier, uniqueAbilityDuration);
        _statsController.TemporalProductStat(Enums.Stats.NORMAL_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);
        _statsController.TemporalProductStat(Enums.Stats.PUSH_DAMAGE, statBoostMultiplier, uniqueAbilityDuration);

        PrintCurrentStats();

        Invoke(nameof(PrintBoostedStats), uniqueAbilityDuration + 0.1f);
    }

    public void PrintCurrentStats()
    {
        //Debug.LogError($"CooldownSpeed: {_statsController.CooldownSpeed}");
        Debug.LogError($"MaxSpeed: {_statsController.MaxSpeed}");
        //Debug.LogError($"Acceleration: {_statsController.Acceleration}");
        //Debug.LogError($"NormalDamage: {_statsController.NormalShootDamage}");
        //Debug.LogError($"PushDamage: {_statsController.PushShootDamage}");
    }

    public void PrintBoostedStats()
    {
        Debug.LogWarning("VALUES AFTER ULTI------------------");
        PrintCurrentStats();
    }
}
