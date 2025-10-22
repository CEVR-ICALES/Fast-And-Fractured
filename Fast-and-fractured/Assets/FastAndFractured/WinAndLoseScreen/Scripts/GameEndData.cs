using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    [CreateAssetMenu(fileName = "GameEndData", menuName = "GameEndData", order = 1)]
    public class GameEndData : ScriptableObject
    {
        public bool isWin;
        public string totalDamageDealt;
        public string totalDamageTaken;
        public string totalDistanceTraveled;
        public GameObject finalAnimation;
        public int sceneBuildIndex;
    }
}