using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using Enums;
using Utilities;

namespace FastAndFractured
{
    public class PepeUniqueAbility : BaseUniqueAbility
    {
        [SerializeField] private GameObject spawnPoint;
        [SerializeField] private float tomatoSpeed = 100f;
        [SerializeField] private float descendingTomatoSpeed = 100f;
        [SerializeField] private float distanceEffect;
        [SerializeField] private float ascendingTime = 3f;
        [SerializeField] private Pooltype pooltypeAscendingTomato;
        [SerializeField] private Pooltype pooltypeDescendingTomato;

        public override void ActivateAbility()
        {
            base.ActivateAbility();
            AbilityEffect();
        }
        private void AbilityEffect()
        {
            GameObject tomato = ObjectPoolManager.Instance.GivePooledObject(pooltypeAscendingTomato);
            if (tomato != null)
            {
                SetTomatoVariables(tomato);
            }
        }
        private void SetTomatoVariables(GameObject tomato)
        {
                Controller controller = GetComponentInParent<Controller>();
                Vector3 spawnPosition = spawnPoint.transform.position;
                tomato.transform.position = spawnPosition;
                tomato.transform.rotation = Quaternion.Euler(0, 0, 0);
                AscendingTomatoBehaviour ascendingTomatoBehaviour = tomato.GetComponent<AscendingTomatoBehaviour>();
                ascendingTomatoBehaviour.speed = tomatoSpeed;
                ascendingTomatoBehaviour.Caster = controller.gameObject;
                ascendingTomatoBehaviour.effectDistance = distanceEffect;
                ascendingTomatoBehaviour.OriginPosition = spawnPosition;
                ascendingTomatoBehaviour.descendingTomatoSpeed = descendingTomatoSpeed;
                ascendingTomatoBehaviour.pooltype = pooltypeAscendingTomato;
                ascendingTomatoBehaviour.pooltypeDescendingTomato = pooltypeDescendingTomato;
                ascendingTomatoBehaviour.ascendingTime = ascendingTime;
                ascendingTomatoBehaviour.StartTimer();
        }
    }

}