using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public enum STATS
    {
        MAX_SPEED,
        ACCELERATION,
        ENDURANCE,
        NORMAL_DAMAGE,
        PUSH_DAMAGE,
        COOLDOWN_SPEED
    }
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
        public bool IsDead { get => charDataSO.Dead; }

        [Header("Movement")]
        [SerializeField] private float currentMaxSpeed;
        [SerializeField] private float currentMaxSpeedDashing;
        [SerializeField] private float currentMaxSpeedAscend;
        [SerializeField] private float currentMaxSpeedDescend;
        [SerializeField] private float currentAcceleration;
        public float MaxSpeed { get => currentMaxSpeed;}
        public float MaxSpeedDashing { get => currentMaxSpeedDashing; }
        public float MaxSpeedAscend { get => currentMaxSpeedAscend; }
        public float MaxSpeedDescend { get => currentMaxSpeedDescend; }
        public float MinSpeed { get => charDataSO.MinSpeed; }

        public float Acceleration { get => currentAcceleration; }
        public float BrakeTorque { get => charDataSO.BrakeTorque; }
        public float Handling { get => charDataSO.Handling; }
        public float DashDistance { get => charDataSO.DashDistance; }

        [Header("Damage")]

        [SerializeField] private float currentNormalShootDMG;
        [SerializeField] private float currentPushShootDMG;
        public float NormalShootDamage { get => currentNormalShootDMG; }
        public float PushShootDamage { get => currentPushShootDMG; }

        //Physics
        public float Weight { get => charDataSO.Weight; }
        public float Traction { get => charDataSO.Traction; }
        public float Damping { get => charDataSO.Damping; }

        [Header("COOLDOWNS")]

        [SerializeField] private float currentcooldownSpeed;

        public float CooldownSpeed { get => currentcooldownSpeed; }
        public float DashCooldown { get => charDataSO.DashCooldown; }
        public float PushCooldown { get => charDataSO.PushShootCooldown; }
        public float UniqueCooldown { get => charDataSO.UniqueAbilityCooldown; }
        public float NormalOverHead { get => charDataSO.NormalShootOverHeat;}
        public float RecoveryCooldown { get => charDataSO.RecoveryCooldown; }

        #endregion

        public delegate void Died();
        public Died OnDied;


        #region START EVENTS
        void CustomStart()
        {

        }
        // Start is called before the first frame update
        void Start()
        {
            //For Try Propouses. Delete if game manager call the function SetCharacter()
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
            currentEndurance = charDataSO.MinEndurance;
            //Movement
            currentMaxSpeed = charDataSO.MaxSpeed;
            currentMaxSpeedDashing = charDataSO.MaxSpeedDashing;
            currentAcceleration = charDataSO.Acceleration;
            //Damage
            currentNormalShootDMG = charDataSO.NormalShootDMG;
            currentPushShootDMG = charDataSO.PushShootDMG;
            //Cooldowns
            currentcooldownSpeed = charDataSO.CooldownSpeed;
        }


        #region Health
        public bool TakeDamage(float sum,bool isProduct)
        {
            if (sum > 0)
            {
                if (ChoseCharToMod(STATS.ENDURANCE, sum,isProduct)&&!charDataSO.Dead)
                {
                    if (currentEndurance >= charDataSO.MaxEndurance)
                    {
                        Dead();
                    }
                }
                else
                    return false;
                return true;
            }
            return false;
        }

        public bool RecoverResist(float subs, bool isProduct)
        {
            if (subs > 0)
            { return ChoseCharToMod(STATS.ENDURANCE,subs*-1,isProduct); }
            return false;
        }

        private void Dead()
        {
            Debug.Log("Toy Muerto");
            charDataSO.Dead = true;
        }
        #endregion

        #region OtherStats
        #region StatsModificators
        public bool UpgradeCharStat(STATS type, float sum)
        {
            if (IsStatAndModificatorCorrect(type, sum)) { return ChoseCharToMod(type, sum,false); }
            return false;
        }

        public bool ReduceCharStat(STATS type, float subs)
        {
            if (IsStatAndModificatorCorrect(type, subs)) { return ChoseCharToMod(type, subs * -1,false); }
            return false;
        }

        public bool ProductCharStats(STATS type, float multiplier)
        {
            if (IsStatAndModificatorCorrect(type, multiplier)){ return ChoseCharToMod(type, multiplier, true); }
            return false;
        }

        private bool ChoseCharToMod(STATS stat, float mod, bool isProduct)
        {
            switch (stat)
            {
                case STATS.MAX_SPEED:
                    currentMaxSpeed = ModCharStat(currentMaxSpeed, mod, charDataSO.MinSpeed, charDataSO.MaxSpeed * charDataSO.MaxSpeedMultiplier, isProduct);
                    currentMaxSpeedDashing = ModCharStat(currentMaxSpeedDashing, mod, charDataSO.MinSpeed, charDataSO.MaxSpeedDashing * charDataSO.MaxSpeedMultiplier, isProduct);
                    return true;
                case STATS.ACCELERATION:
                    currentAcceleration = ModCharStat(currentAcceleration, mod, charDataSO.MinAcceleration, charDataSO.MaxAcceleration, isProduct);
                    return true;
                case STATS.ENDURANCE:
                    currentEndurance = ModCharStat(currentEndurance, mod, charDataSO.MinEndurance, charDataSO.MaxEndurance, isProduct);
                    return true;
                case STATS.COOLDOWN_SPEED:
                    currentcooldownSpeed = ModCharStat(currentcooldownSpeed, mod, charDataSO.MinCooldownSpeed, charDataSO.MinCooldownSpeed, isProduct);
                    return true;
                case STATS.PUSH_DAMAGE:
                    currentPushShootDMG = ModCharStat(currentPushShootDMG, mod, charDataSO.MinPushShootDMG, charDataSO.MaxPushShootDMG, isProduct);
                    return true;
                case STATS.NORMAL_DAMAGE:
                    currentNormalShootDMG = ModCharStat(currentNormalShootDMG, mod, charDataSO.MinNormalShootDMG, charDataSO.MaxNormalShootDMG, isProduct);
                    return true;
            }
            return false;
        }

        private float ModCharStat(float charStat, float mod, float minVal, float maxVal, bool isProduct)
        {
            if (!isProduct)
            {
                charStat += mod;
            }
            else
            {
                charStat *= mod;
            }

            if (charStat > maxVal)
                charStat = maxVal;
            else if (charStat < minVal)
                charStat = minVal;
            return charStat;
        }
        #endregion
        #region TemporalModificators
        public bool TemporalStatUp(STATS type, float sum,float time)
        {
           return TemporalStatMod(type, sum, time,false);
        }
         public bool TemporalStatDown(STATS type, float subs, float time)
        {
           return TemporalStatMod(type, subs, time, false);
        }

        public bool TemporalProductStat(STATS type, float multiplier, float time)
        {
           return TemporalStatMod(type, multiplier, time, true);
        }

        private bool TemporalStatMod(STATS type, float mod, float time, bool isProduct)
        {
            float previousValue = GetCurrentStat(type);
            ChoseCharToMod(type, mod,isProduct);
            float currentValue = GetCurrentStat(type);
            if (previousValue == -1 || currentValue == -1)
                return false;
            StartCoroutine(WaitTimeToModStat(previousValue, currentValue, type, previousValue < currentValue, time));
            return true;
        }

        //Coroutine is for try propuses. It will be susbtitute for a Timer. 
        private IEnumerator WaitTimeToModStat(float previousValue, float currentValue, STATS stat, bool iscurrentBigger, float time)
        {
            float mod;
            if (iscurrentBigger)
                mod = (currentValue - previousValue) * -1;
            else
                mod = previousValue - currentValue;
            yield return new WaitForSeconds(time);
            ChoseCharToMod(stat, mod, false);
        }
        #endregion  

        private float GetCurrentStat(STATS type)
        {
            switch (type)
            {
                case STATS.MAX_SPEED:
                    return currentMaxSpeed;
                case STATS.ACCELERATION:
                    return currentAcceleration;
                case STATS.ENDURANCE:
                    return currentEndurance;
                case STATS.NORMAL_DAMAGE:
                    return currentNormalShootDMG;
                case STATS.PUSH_DAMAGE:
                    return currentPushShootDMG;

                case STATS.COOLDOWN_SPEED:
                    return currentcooldownSpeed;
            }
            return -1;
        }

            private bool IsStatAndModificatorCorrect(STATS type, float mod)
        {
            return mod > 0 && type != STATS.ENDURANCE;
        }
            #endregion
        }
    }
