using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public interface IKillCharacters
    {
        public int killPriority { get; }
        public float killTime { get; }
        public void StartKillNotify(StatsController statsController)
        {

        }
        public void CharacterEscapedDead(StatsController statsController)
        {

        }
    }
}
