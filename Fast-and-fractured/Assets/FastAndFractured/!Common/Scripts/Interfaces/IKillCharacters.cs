using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public interface IKillCharacters
    {
        public int KillPriority { get; }
        public float KillTime { get; }
        public void StartKillNotify(StatsController statsController)
        {

        }
        public void CharacterEscapedDead(StatsController statsController)
        {

        }

        public GameObject GetKillerGameObject();
    }
}
