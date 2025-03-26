using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class CustomStart : MonoBehaviour
    {
        public static event Action OnCustomStart;
        
        void Start()
        {
            TriggerCustomStart();
        }
        private void TriggerCustomStart()
        {
            OnCustomStart?.Invoke();
        }
    }
}