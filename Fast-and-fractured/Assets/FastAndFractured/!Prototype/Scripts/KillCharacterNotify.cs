using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FastAndFractured;
public class KillCharacterNotify : MonoBehaviour
{
    public delegate void TouchCharacter(StatsController statsController);
    public event TouchCharacter onTouchCharacter;

    private void OnTriggerEnter(Collider other)
    {
        if(other.TryGetComponent(out StatsController statsController))
        {
            onTouchCharacter(statsController);
        }
    }
}
