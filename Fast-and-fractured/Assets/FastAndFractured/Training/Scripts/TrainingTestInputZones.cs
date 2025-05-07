using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public class TrainingTestInputZones : MonoBehaviour
    {
        [SerializeField]
        private EnemyAIBrain enemyAIBrain;
        private bool _lisentInput = false;

        private void Update()
        {
            if (_lisentInput && PlayerInputController.Instance.IsUsingAbility&&!enemyAIBrain.DoTrainingAction)
            {
                enemyAIBrain.DoTrainingAction = true;
            }
        }
        private void OnTriggerEnter(Collider other)
        {
            if(other.TryGetComponent(out StatsController character))
            {
                if (character.IsPlayer)
                {
                    _lisentInput = true;
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out StatsController character))
            {
                if (character.IsPlayer)
                {
                    _lisentInput = false;
                }
            }
        }
    }
}
