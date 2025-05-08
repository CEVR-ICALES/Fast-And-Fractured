using FastAndFractured;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainingResetPlayer : MonoBehaviour
{
    [SerializeField]
    private List<GameObject> charactersToReset;
    private List<StatsController> _characterToResetStatsController = new List<StatsController>();
    private List<PhysicsBehaviour> _charactersPhysicsBehaivours =  new List<PhysicsBehaviour>();
    private List<Vector3> _spawnPosition = new List<Vector3>();
    [SerializeField]
    private Transform playerBaseCar;
    private GameObject _actualPlayer;
    private Vector3 _playerInitialPosition;
    private PhysicsBehaviour _playerPhysicsBehaivour;
    private void Start()
    {
        foreach (var character in charactersToReset)
        {
            _spawnPosition.Add(character.transform.position);
            _characterToResetStatsController.Add(character.GetComponentInChildren<StatsController>());
            _charactersPhysicsBehaivours.Add(character.GetComponentInChildren<PhysicsBehaviour>());
        }
        _actualPlayer = playerBaseCar.transform.GetComponentInChildren<StatsController>().gameObject;
        _playerPhysicsBehaivour = _actualPlayer.GetComponent<PhysicsBehaviour>();
        _playerInitialPosition = _actualPlayer.transform.position;
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out StatsController character))
        {
            ResetLevel();
            if (character.IsPlayer)
            {
                _actualPlayer.transform.position = _playerInitialPosition;
                _playerPhysicsBehaivour.Rb.velocity = Vector3.zero;
            }
        }
    }

   public void ResetLevel()
    {
        for (int i = 0; i < charactersToReset.Count; i++)
        {
            _characterToResetStatsController[i].gameObject.transform.position = _spawnPosition[i];
            _characterToResetStatsController[i].gameObject.transform.rotation = charactersToReset[i].transform.rotation;
            _characterToResetStatsController[i].RecoverEndurance(_characterToResetStatsController[i].MaxEndurance, false);
            _charactersPhysicsBehaivours[i].Rb.velocity = Vector3.zero;
        }
    }
}
