using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class TestDeCustomStart : MonoBehaviour
    {
        void OnEnable()
        {
            CustomStart.OnCustomStart += HandleCustomStart;
        }
        void OnDisable()
        {
            CustomStart.OnCustomStart -= HandleCustomStart;
        }

        private void HandleCustomStart()
        {
            // Code that should run in the start
        }
    }
}