#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IPrefabPlacerMode
{
    abstract void OnModeActivated(ProfilePlacedObjectsTrackerSO trackerSO);

    abstract void OnModeDeactivated();
}

#endif
