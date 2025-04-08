using Enums;
using UnityEngine;
using UnityEngine.Events;
using Utilities;

namespace FastAndFractured
{
    public class StatsController : MonoBehaviour
    {
        [SerializeField]
        private CharacterData charDataSO;

        [Header("CURRENT STATS")]

        [Header("Health")]

        #region CharStats
        [SerializeField] private float currentEndurance;
        public float Endurance { get => currentEndurance; }
        public float MaxEndurance { get => charDataSO.MaxEndurance; }
        public bool IsInvulnerable { get => charDataSO.Invulnerable; set => charDataSO.Invulnerable = value; }

        [Header("Movement")]
        [SerializeField] private float currentMaxSpeed;
        [SerializeField] private float currentMaxSpeedDashing;
        [SerializeField] private float currentMaxSpeedAscend;
        [SerializeField] private float currentMaxSpeedDescend;
        [SerializeField] private float currentAcceleration;
        private float _currentMaxSpeedMultiplier;
        public float MaxSpeed { get => currentMaxSpeed; }
        public float MaxSpeedDashing { get => currentMaxSpeedDashing; }
        public float MaxSpeedAscend { get => currentMaxSpeedAscend; }
        public float MaxSpeedDescend { get => currentMaxSpeedDescend; }
        public float MaxSpeedMultiplier { get => MaxSpeedMultiplier; }
        public float MinSpeed { get => charDataSO.MinSpeed; }

        public float Acceleration { get => currentAcceleration; }
        public float BrakeTorque { get => charDataSO.BrakeTorque; }
        public float Handling { get => charDataSO.Handling; }
        public float HandlingSmothnees { get => charDataSO.HandlingSmoothnes; }
        public float DashTime { get => charDataSO.DashTime; }
        public float DriftThreshold { get => charDataSO.DriftThreshold; }
        public float DriftingFactorToSpeed { get => charDataSO.DriftingFactorToSpeed; }
        public float DriftingSmoothFactor { get => charDataSO.DriftingSmoothFactor; }
        public float DriftForce { get => charDataSO.DriftForce; }

        //Wheels
        public float FrontWheelsStrenghtFactor { get => charDataSO.FrontWheelsStrenghtFactor; }
        public float RearWheelsStrenghtFactor { get => charDataSO.RearWheelsStrenghtFactor; }

        //Roll Prevention
        public float BaseDownwardForce { get => charDataSO.BaseDownwardForce; }
        public float TurningForceMultiplier { get => charDataSO.TurningForceMultiplier; }
        public float SpeedForceMultiplier { get => charDataSO.SpeedForceMultiplier; }

        [Header("Damage")]

        [SerializeField] private float currentNormalShootDMG;
        [SerializeField] private float currentPushShootDMG;
        public float NormalShootDamage { get => currentNormalShootDMG; }
        public float PushShootDamage { get => currentPushShootDMG; }
        public float PushShootForce { get => charDataSO.PushShootFORCE; }
        public float ExplosionRadius { get => charDataSO.ExplosionRadius; }
        public Vector3 ExplosionCenterOffset { get => charDataSO.ExplosionCenterOffset; }

        //Shoot Movement
        public float NormalShootSpeed { get => charDataSO.NormalShootSpeed; }
        public float NormalShootCadenceTime { get => charDataSO.NormalShootCadenceTime; }
        public float NormalShootMaxRange { get => charDataSO.NormalShootMaxRange; }
        public float PushShootRange { get => charDataSO.PushShootRange; }
        public float PushShootAngle { get => charDataSO.PushShootAngle; }
        public float PushShootGravityMultiplier { get => charDataSO.PushShootGravityMultiplier; }
        public int PushShootBounceNum { get => charDataSO.PushShootBounceNum; }
        public float PushShootBounceForce { get => charDataSO.PushShootBounceForce; }
        public float MineShootAngle { get => charDataSO.MineShootAngle; }
        public float MineShootRange { get => charDataSO.MineShootRange; }

        //Physics
        public float Weight { get => charDataSO.Weight; }
        public float BaseForce { get => charDataSO.BaseForce; }
        public float FrontalHitAnlgeThreshold { get => charDataSO.FrontalHitAnlgeThreshold; }
        public float EnduranceImportanceWhenColliding { get => charDataSO.EnduranceImportanceWhenColliding; }

        [Header("COOLDOWNS")]

        [SerializeField] private float currentCooldownSpeed;

        public float CooldownSpeed { get => currentCooldownSpeed; }
        public float DashCooldown { get => charDataSO.DashCooldown; }
        public float PushCooldown { get => charDataSO.PushShootCooldown; }
        public float MineExplosionTime { get=>  charDataSO.MineExplosionTime; }
        public float UniqueCooldown { get => charDataSO.UniqueAbilityCooldown; }
        public float NormalOverHeat { get => charDataSO.NormalShootOverHeat; }
        #endregion

        private bool _isPlayer = false;
        private const float ERROR_GET_STAT_FLOAT = -1;
        public UnityEvent<float,GameObject> onEnduranceDamageTaken;
        public UnityEvent<float> onEnduranceDamageHealed;
        public UnityEvent<float,GameObject,bool> onDead;

        #region START EVENTS
        public void CustomStart()
        {
            onDead.AddListener(LevelController.Instance.OnPlayerDead);
            //just for try propouses
            charDataSO.Invulnerable = false;
            _isPlayer = !GetComponent<CarMovementController>().IsAi;
            //For Try Propouses. Delete when game manager call the function SetCharacter()
            InitCurrentStats();
        }

        #endregion

        public void SetCharacter(CharacterData charData)
        {
            var copyOfCharData = Instantiate(charData);
            if (copyOfCharData != null)
            {
                charDataSO = copyOfCharData;
                InitCurrentStats();
            }
        }
        private void InitCurrentStats()
        {
            //Health
            currentEndurance = charDataSO.MaxEndurance;
            //Movement
            currentMaxSpeed = charDataSO.MaxSpeed;
            currentMaxSpeedDashing = charDataSO.MaxSpeedDashing;
            currentAcceleration = charDataSO.Acceleration;
            //Damage
            currentNormalShootDMG = charDataSO.NormalShootDMG;
            //Cooldowns
            currentCooldownSpeed = charDataSO.FromTopSpeedToMaxSpeed;
        }
        [ContextMenu(nameof(DebugTake100Endurance))]
        public void DebugTake100Endurance()
        {
            TakeEndurance(100, false);
        }


        #region Health
        public void TakeEndurance(float substract, bool isProduct)
        {
            if (substract > 0)
            {
                if (!charDataSO.Invulnerable)
                {
                    if (ChoseCharToMod(Stats.ENDURANCE, -substract, isProduct))
                    {
                        onEnduranceDamageTaken?.Invoke(substract,this.gameObject);
                        //This is not the real dead condition, just an example. 
                        /*if (currentEndurance <= charDataSO.MinEndurance)
                        {
                            Dead();
                        }*/
                    }
                    else
                        Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                            "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }
                else
                {
                    IsInvulnerable = false;
                }
            }
            else Debug.LogError("Value can't be negative or 0.");
        }

        public void RecoverEndurance(float sum, bool isProduct)
        {
            if (sum > 0)
            {
                if (ChoseCharToMod(Stats.ENDURANCE, sum, isProduct))
                {
                    onEnduranceDamageHealed?.Invoke(sum);
                } else
                {
                    Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                                            "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }
            }
        }

        public void Dead()
        {
            Debug.Log("He muerto soy " + charDataSO.name);
            charDataSO.Invulnerable = true;
            onDead?.Invoke(charDataSO.DeadDelay,transform.parent.gameObject,_isPlayer);
        }

        public float GetEndurancePercentage()
        {
            return Endurance / MaxEndurance * 100;
        }

        public float GetEndurancePercentage()
        {
            return Endurance / MaxEndurance * 100;
        }
        #endregion

        #region OtherStats
        #region StatsModificators
        public void UpgradeCharStat(Stats type, float sum)
        {
            if (IsStatAndModificatorCorrect(type, sum))
            {
                if (!ChoseCharToMod(type, sum, false))
                {
                    Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                     "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }
            }
            else
                Debug.LogError("Value can't be positive or you are trying to change the endurance." +
                    " If that's the case, use the TakeEndurance or RecoverEndurance methods.");

        }

        public void ReduceCharStat(Stats type, float subtrahend)
        {
            if (IsStatAndModificatorCorrect(type, subtrahend))
            {
                if (!ChoseCharToMod(type, -subtrahend, false))
                {
                    Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                    "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }
            }
            else
                Debug.LogError("Value can't be positive or you are trying to change the endurance." +
                  " If that's the case, use the TakeEndurance or RecoverEndurance methods.");
        }

        public void ProductCharStats(Stats type, float multiplier)
        {
            if (IsStatAndModificatorCorrect(type, multiplier))
            {
                if (!ChoseCharToMod(type, multiplier, true))
                {
                    Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                    "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }
            }
            else
                Debug.LogError("Value can't be positive or you are trying to change the endurance." +
                 " If that's the case, use the TakeEndurance or RecoverEndurance methods.");
        }

        private bool ChoseCharToMod(Stats stat, float mod, bool isProduct)
        {
            switch (stat)
            {
                case Stats.MAX_SPEED:
                    currentMaxSpeed = ModCharStat(currentMaxSpeed, mod, charDataSO.MinSpeed, charDataSO.MaxSpeed * charDataSO.MaxSpeedMultiplier, isProduct, false);
                    currentMaxSpeedDashing = ModCharStat(currentMaxSpeedDashing, mod, charDataSO.MinSpeed, charDataSO.MaxSpeedDashing * charDataSO.MaxSpeedMultiplier, isProduct, false);
                    return true;
                case Stats.MAX_SPEED_MULTIPLIER:
                    _currentMaxSpeedMultiplier = ModCharStat(_currentMaxSpeedMultiplier, mod, 1, float.MaxValue, isProduct, false);
                    return true;
                //case Stats.MAX_SPEED_MULTIPLIER:
                //    _currentMaxSpeedMultiplier = ModCharStat(_currentMaxSpeedMultiplier, mod, 1, float.MaxValue, isProduct);
                //    return true;
                case Stats.ACCELERATION:
                    currentAcceleration = ModCharStat(currentAcceleration, mod, charDataSO.MinAcceleration, charDataSO.MaxAcceleration, isProduct, false);
                    return true;
                case Stats.ENDURANCE:
                    currentEndurance = ModCharStat(currentEndurance, mod, charDataSO.MinEndurance, charDataSO.MaxEndurance, isProduct, true);
                    if (_isPlayer)
                    {
                        HUDManager.Instance.UpdateUIElement(UIElementType.HEALTH_BAR, currentEndurance, charDataSO.MaxEndurance);
                    }
                    return true;
                case Stats.NORMAL_DAMAGE:
                    currentNormalShootDMG = ModCharStat(currentNormalShootDMG, mod, charDataSO.MinNormalShootDMG, charDataSO.MaxNormalShootDMG, isProduct, true);
                    return true;
            }
            return false;
        }

        private float ModCharStat(float charStat, float mod, float minVal, float maxVal, bool isProduct, bool applyClamp)
        {
            charStat = isProduct ? charStat * mod : charStat + mod;

            if (applyClamp)
            {
                charStat = Mathf.Clamp(charStat, minVal, maxVal);

            }
            return charStat;
        }
        #endregion
        #region TemporalModificators
        public void TemporalStatUp(Stats type, float sum, float time)
        {
            TemporalStatMod(type, sum, time, false);
        }
        public void TemporalStatDown(Stats type, float subtrahend, float time)
        {
            TemporalStatMod(type, -subtrahend, time, false);
        }

        public void TemporalProductStat(Stats type, float multiplier, float time)
        {
            TemporalStatMod(type, multiplier, time, true);
        }

        private void TemporalStatMod(Stats type, float mod, float time, bool isProduct)
        {
            float previousValue = GetCurrentStat(type);
            ChoseCharToMod(type, mod, isProduct);
            float currentValue = GetCurrentStat(type);
            if (previousValue == ERROR_GET_STAT_FLOAT || currentValue == ERROR_GET_STAT_FLOAT)
            {
                Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                   "Comprove if GetCurrentStat method of class Stats Controller contains this states");
            }
            //StartCoroutine(WaitTimeToModStat(previousValue, currentValue, type, previousValue < currentValue, time));
            RemoveStatModificationByTimer(previousValue, currentValue, type, previousValue < currentValue, time);
        }

        //Coroutine is for try propuses. It will be susbtitute for a Timer. 
        private void RemoveStatModificationByTimer(float previousValue, float currentValue, Stats stat, bool iscurrentBigger, float time)
        {
            float mod;

            if (iscurrentBigger)
            {
                if (currentValue / previousValue > 1)
                    mod = 1 / (currentValue / previousValue);
                else
                    mod = -(currentValue - previousValue);
            }
            else
            {
                if (previousValue / currentValue > 1)
                    mod = previousValue / currentValue;
                else
                    mod = previousValue - currentValue;
            }

            TimerSystem.Instance.CreateTimer(time, onTimerDecreaseComplete: () =>
            {
                ChoseCharToMod(stat, mod, true);
            });
        }
        #endregion  

        private float GetCurrentStat(Stats type)
        {
            switch (type)
            {
                case Stats.MAX_SPEED:
                    return currentMaxSpeed;
                case Stats.ACCELERATION:
                    return currentAcceleration;
                case Stats.ENDURANCE:
                    return currentEndurance;
                case Stats.NORMAL_DAMAGE:
                    return currentNormalShootDMG;
                case Stats.PUSH_DAMAGE:
                    return currentPushShootDMG;
                case Stats.COOLDOWN_SPEED:
                    return currentCooldownSpeed;
                case Stats.MAX_SPEED_MULTIPLIER:
                    return _currentMaxSpeedMultiplier;
            }
            return ERROR_GET_STAT_FLOAT;
        }

        private bool IsStatAndModificatorCorrect(Stats type, float mod)
        {
            return mod > 0 && type != Stats.ENDURANCE;
        }
        #endregion
    }
}