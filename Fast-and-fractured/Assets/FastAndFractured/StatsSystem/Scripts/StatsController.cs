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
        public bool IsInvulnerable { get => charDataSO.Invulnerable; set => charDataSO.Invulnerable = value;}

        [Header("Movement")]
        [SerializeField] private float currentMaxSpeed;
        [SerializeField] private float currentMaxSpeedDashing;
        [SerializeField] private float currentMaxSpeedAscend;
        [SerializeField] private float currentMaxSpeedDescend;
        [SerializeField] private float currentAcceleration;
        public float MaxSpeed { get => currentMaxSpeed; }
        public float MaxSpeedDashing { get => currentMaxSpeedDashing; }
        public float MaxSpeedAscend { get => currentMaxSpeedAscend; }
        public float MaxSpeedDescend { get => currentMaxSpeedDescend; }
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

        //Physics
        public float Weight { get => charDataSO.Weight; }
        public float Traction { get => charDataSO.Traction; }
        public float Damping { get => charDataSO.Damping; }
        public float BaseForce { get => charDataSO.BaseForce; }
        public float FrontalHitAnlgeThreshold { get => charDataSO.FrontalHitAnlgeThreshold; }
        public float EnduranceImportanceWhenColliding { get => charDataSO.EnduranceImportanceWhenColliding; }

        [Header("COOLDOWNS")]

        [SerializeField] private float currentCooldownSpeed;

        public float CooldownSpeed { get => currentCooldownSpeed; }
        public float DashCooldown { get => charDataSO.DashCooldown; }
        public float PushCooldown { get => charDataSO.PushShootCooldown; }
        public float UniqueCooldown { get => charDataSO.UniqueAbilityCooldown; }
        public float NormalOverHeat { get => charDataSO.NormalShootOverHeat; }
        public float RecoveryCooldown { get => charDataSO.RecoveryCooldown; }

        #endregion
        private float _errorGetStatFloat = -1;
        public UnityEvent<float> onEnduranceDamageTaken;

        #region START EVENTS
        public void CustomStart()
        {
            //just for try propouses
            charDataSO.Invulnerable = false;
            //For Try Propouses. Delete when game manager call the function SetCharacter()
            InitCurrentStats();
        }

        #endregion
        // Update is called once per frame
        void Update()
        {

        }

        public void SetCharacter(CharacterData charData)
        {
            var copyOfCharData = Instantiate(charData);
            if (copyOfCharData != null)
            {
                charDataSO = copyOfCharData;
                //OnDied += Dead;
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
            currentPushShootDMG = charDataSO.PushShootDMG;
            //Cooldowns
            currentCooldownSpeed = charDataSO.FromTopSpeedToMaxSpeed;
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
                        onEnduranceDamageTaken?.Invoke(currentEndurance);
                        //This is not the real dead condition, just an example. 
                        /*if (currentEndurance <= charDataSO.MinEndurance)
                        {
                            Dead();
                        }*/
                    }
                    else
                        Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                            "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }else
                {
                    IsInvulnerable=false;
                }
            }
            else Debug.LogError("Value can't be negative or 0.");
        }

        public void RecoverEndurance(float sum, bool isProduct)
        {
            if (sum > 0)
            {
                if (!ChoseCharToMod(Stats.ENDURANCE, sum, isProduct))
                {
                    Debug.LogError("Stat selected doesn't exist or can't be modified. " +
                                            "Comprove if ChooseCharToMod method of class Stats Controller contains this states");
                }
            }
        }

        public float Dead()
        {
            Debug.Log("He muerto soy " + charDataSO.name);
            charDataSO.Invulnerable = true;
            return charDataSO.DeadDelay;
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
                    currentMaxSpeed = ModCharStat(currentMaxSpeed, mod, charDataSO.MinSpeed, charDataSO.MaxSpeed * charDataSO.MaxSpeedMultiplier, isProduct);
                    currentMaxSpeedDashing = ModCharStat(currentMaxSpeedDashing, mod, charDataSO.MinSpeed, charDataSO.MaxSpeedDashing * charDataSO.MaxSpeedMultiplier, isProduct);
                    return true;
                case Stats.ACCELERATION:
                    currentAcceleration = ModCharStat(currentAcceleration, mod, charDataSO.MinAcceleration, charDataSO.MaxAcceleration, isProduct);
                    return true;
                case Stats.ENDURANCE:
                    currentEndurance = ModCharStat(currentEndurance, mod, charDataSO.MinEndurance, charDataSO.MaxEndurance, isProduct);
                    if (gameObject.TryGetComponent<CarMovementController>(out CarMovementController testCarMController) && !testCarMController.IsAi) //Provisional Refactoring
                    {
                        HUDManager.Instance.UpdateUIElement(UIElementType.HEALTH_BAR, currentEndurance, charDataSO.MaxEndurance);
                    }
                    return true;
                case Stats.PUSH_DAMAGE:
                    currentPushShootDMG = ModCharStat(currentPushShootDMG, mod, charDataSO.MinPushShootDMG, charDataSO.MaxPushShootDMG, isProduct);
                    return true;
                case Stats.NORMAL_DAMAGE:
                    currentNormalShootDMG = ModCharStat(currentNormalShootDMG, mod, charDataSO.MinNormalShootDMG, charDataSO.MaxNormalShootDMG, isProduct);
                    return true;
            }
            return false;
        }

        private float ModCharStat(float charStat, float mod, float minVal, float maxVal, bool isProduct)
        {
            charStat = isProduct ? charStat * mod : charStat + mod;
            //charStat = Mathf.Clamp(charStat, minVal, maxVal);
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
            if (previousValue == _errorGetStatFloat || currentValue == _errorGetStatFloat)
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
                if (currentValue / previousValue > 1) // Si fue un producto
                    mod = 1 / (currentValue / previousValue); // Se revierte con una división
                else
                    mod = -(currentValue - previousValue); // Si fue una suma/resta, se revierte con resta
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
                ChoseCharToMod(stat, mod, true); // Se usa como producto si fue una multiplicación
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
            }
            return _errorGetStatFloat;
        }

        private bool IsStatAndModificatorCorrect(Stats type, float mod)
        {
            return mod > 0 && type != Stats.ENDURANCE;
        }
        #endregion
    }
}
