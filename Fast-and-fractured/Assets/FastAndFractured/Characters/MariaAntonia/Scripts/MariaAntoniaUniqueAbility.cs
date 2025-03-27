using UnityEngine;
using FastAndFractured;
using System.Collections.Generic;

public class MariaAntoniaUniqueAbility : BaseUniqueAbility
{
    #region Variables
    [Tooltip("")]
    public float cooldownReductionMultiplier;

    [Tooltip("")]
    public float statBoostMultiplier;

    public float uniqueAbilityDuration;

    private StatsController _statsController;

    private List<BaseUniqueAbility> _allAbilities;

    private Dictionary<BaseUniqueAbility, float> _originalCooldowns = new Dictionary<BaseUniqueAbility, float>();
    #endregion

    public override void ActivateAbility()
    {
        if (IsAbilityActive || IsOnCooldown)
            return;

        base.ActivateAbility();

        _statsController = GetComponent<StatsController>();

        _allAbilities = new List<BaseUniqueAbility>(GetComponents<BaseUniqueAbility>());

        foreach (BaseUniqueAbility ability in _allAbilities)
        {
            if (ability != this)
            {
                _originalCooldowns[ability] = abilityData.CooldownDuration;

            }
        }
    }


}
