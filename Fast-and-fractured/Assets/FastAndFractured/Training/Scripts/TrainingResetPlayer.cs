using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainingResetPlayer : MonoBehaviour
{
    [SerializeField]
    private List<StatsController> charactersToReset;
    private List<Vector3> _spawnPosition;
    private void Start()
    {
        foreach (var character in charactersToReset)
        {
            _spawnPosition.Add(character.transform.position);
        }
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
            charactersToReset[i].gameObject.transform.position = _spawnPosition[i];
            charactersToReset[i].RecoverEndurance(charactersToReset[i].MaxEndurance, false);
        }
    }
}
