using NRandom;
using UnityEngine;

namespace Utilities
{
   public static class DeterministicRandom
    {
        public static bool _isInitialized = false;

        private static IRandom _randomInstance = new Xoshiro256StarStarRandom();

      
        public static IRandom Instance
        {
            get
            {
                if (!_isInitialized)
                {
                    Debug.LogWarning("DeterministicRandom.Instance fue accedido antes de ser inicializado por la red." +
                                     "Se inicializará con una semilla del sistema. En multijugador, esto causará desincronización.");
                    Initialize(System.Environment.TickCount);
                }
                return _randomInstance;
            }
        }

        public static void Initialize(int seed)
        {
            Debug.Log($"<color=lime>DeterministicRandom: Inicializando con semilla {seed}</color>");
            _randomInstance.InitState((uint)seed);
            _isInitialized = true;
        }
    }
}