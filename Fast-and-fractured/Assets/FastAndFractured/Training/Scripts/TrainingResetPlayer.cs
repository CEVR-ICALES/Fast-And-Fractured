using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainingResetPlayer : MonoBehaviour
{
    [SerializeField]
    private List<StatsController> charactersToReset;
    [SerializeField]
    private Transform[] spawnPosition;
    private void Start()
    {

    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out StatsController character))
        {
            ResetLevel();
        }
    }

   public void ResetLevel()
    {
        for (int i = 0; i < charactersToReset.Count; i++)
        {
            charactersToReset[i].gameObject.transform.position = spawnPosition[i].position;
            charactersToReset[i].RecoverEndurance(charactersToReset[i].MaxEndurance, false);
        }
    }
}
