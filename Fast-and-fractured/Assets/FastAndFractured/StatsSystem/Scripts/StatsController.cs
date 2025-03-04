using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace Game
{
    public enum STATS
    {
        SPEED,
        ACCELERATION,
        RESIST,
        DAMAGE,
        COOLDOWNS,
    }
    public class StatsController : MonoBehaviour
    {
        [SerializeField]
        private CharacterData charDataSO;

        [SerializeField] private float currentResist;
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
                charDataSO.OnDied += Dead;
                currentResist = 0f;
            }
       }

        public void ModStat(STATS stats, float mod)
        {
            switch (stats)
            {
                case STATS.SPEED:
                    break;
                case STATS.ACCELERATION:
                    break;
                case STATS.DAMAGE:
                    break;
            }
        }

        public void ModStat(STATS stats, float mod, float time)
        {

        }

        public void ModResist(float mod)
        {
            currentResist += mod;
            if (currentResist < 0)
                currentResist = 0;
            //else if(currentResist>)
        }

        public void Dead()
        {

        }
    }
}
