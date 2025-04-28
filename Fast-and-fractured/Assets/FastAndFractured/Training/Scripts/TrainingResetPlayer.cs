using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainingResetPlayer : MonoBehaviour
{
    StatsController[] players;
    List<Vector3> startingPositions = new List<Vector3>();
    private void Start()
    {
        players = FindObjectsOfType<StatsController>();
        foreach (StatsController player in players)
        {
            startingPositions.Add(player.gameObject.transform.position);
        }

    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out StatsController controller))
        {
            for (int i = 0; i < players.Length; i++)
            {
                players[i].gameObject.transform.position = startingPositions[i];
                players[i].RecoverEndurance(players[i].MaxEndurance, false);
            }
        }
    }
}
