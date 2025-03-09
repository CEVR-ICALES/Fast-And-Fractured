using System.Collections;
using System.Collections.Generic;
using Game;
using UnityEngine;

public class InitObjectPoolHandle : MonoBehaviour
{
    [SerializeField]
    private List<ObjectPoolSO> poolSOs;
    // Start is called before the first frame update
    void Start()
    {
        foreach (var poolSO in poolSOs)
        {
            ObjectPoolManager.Instance.CreateObjectPool(poolSO);
        }
    }
}
