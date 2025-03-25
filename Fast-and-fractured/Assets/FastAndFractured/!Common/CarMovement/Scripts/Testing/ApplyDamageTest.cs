using Game;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ApplyDamageTest : MonoBehaviour
{
    public float damageAmount;

    [ContextMenu("Apply Damage")]
    private void ApplyDamage()
    {
        gameObject.GetComponent<StatsController>().TakeEndurance(damageAmount, false);
    }
}
