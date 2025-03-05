using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public enum STATS
    {
        MAX_SPEED,
        ACCELERATION,
        RESIST,
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
        [SerializeField] private float currentResist;
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

        private float ModCharStat(float charStat, float mod, float minVal, float maxVal,bool isProduct)
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

        private void InitCurrentStats()
        {
            //Health
            currentResist = 0f;
            //Movement
            currentMaxSpeed = 0f;
            currentAcceleration = charDataSO.Acceleration;
            //Damage
            currentNormalShootDMG = charDataSO.NormalShootDMG;
            currentPushShootDMG = charDataSO.PushShootDMG;
            //Cooldowns
            currentcooldownSpeed = charDataSO.CooldownSpeed;
        }


        #region Health
        public bool TakeDamage(float subs,bool isProduct)
        {
            if (subs > 0)
            {
                if (ChoseCharToMod(STATS.RESIST, subs,isProduct)&&!charDataSO.Dead)
                {
                    if (currentResist >= charDataSO.MaxResits)
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

        public bool RecoverResist(float sum, bool isProduct)
        {
            if (sum > 0)
            { return ChoseCharToMod(STATS.RESIST,sum,isProduct); }
            return false;
        }

        private void Dead()
        {
            Debug.Log("Toy Muerto");
        }
        #endregion

        #region OtherStats
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

        private bool IsStatAndModificatorCorrect(STATS type, float mod)
        {
            return mod > 0 && type != STATS.RESIST;
        }

        private bool ChoseCharToMod(STATS stat, float mod, bool isProduct)
        {
            switch (stat)
            {
                case STATS.MAX_SPEED:
                    ModCharStat(currentMaxSpeed,mod,charDataSO.MinSpeed,charDataSO.MaxSpeed*charDataSO.MaxSpeedMultiplier,isProduct);
                    ModCharStat(currentMaxSpeedAscend, mod, charDataSO.MinSpeed, charDataSO.MaxSpeedAscend * charDataSO.MaxSpeedMultiplier, isProduct);
                    ModCharStat(currentMaxSpeedDescend, mod, charDataSO.MinSpeed, charDataSO.MaxSpeedDescend * charDataSO.MaxSpeedMultiplier, isProduct);
                    ModCharStat(currentMaxSpeedDashing, mod, charDataSO.MinSpeed, charDataSO.MaxSpeedDashing * charDataSO.MaxSpeedMultiplier, isProduct);
                    return true;
                case STATS.ACCELERATION:
                    ModCharStat(currentAcceleration, mod, charDataSO.MinAcceleration, charDataSO.MaxAcceleration,isProduct);
                    return true;
                case STATS.RESIST:
                    ModCharStat(currentResist, mod, charDataSO.MinResists, charDataSO.MaxResits,isProduct);
                    return true;
                case STATS.COOLDOWN_SPEED:
                    ModCharStat(currentcooldownSpeed, mod, charDataSO.MinCooldownSpeed, charDataSO.MinCooldownSpeed,isProduct);
                    return true;
                case STATS.PUSH_DAMAGE:
                    ModCharStat(currentPushShootDMG, mod, charDataSO.MinPushShootDMG, charDataSO.MaxPushShootDMG,isProduct);
                    return true;
                case STATS.NORMAL_DAMAGE:
                    ModCharStat(currentNormalShootDMG, mod, charDataSO.MinNormalShootDMG, charDataSO.MaxNormalShootDMG, isProduct);
                    return true;
            }
            return false;
        }
        #endregion
    }
}
