using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Utilities
{
    public interface ITimeSpeedModifiable
    {
        public void ModifySpeedOfExistingTimer(float newTimerSpeed);
    }
}
