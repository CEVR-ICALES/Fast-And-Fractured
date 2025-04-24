using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using Enums;
using Utilities;
namespace FastAndFractured
{
    public class DescendingTomatoBehaviour : MonoBehaviour, IPooledObject
    {
        private bool initValues = true;
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => initValues;
        public GameObject objective;
        public float speed;
        public float effectTime = 5f;
        public virtual void InitializeValues()
        {
            
        }

        void Update()
        {
            if (objective != null)
            {
                Vector3 direction = (objective.transform.position - transform.position).normalized;
                transform.position += direction * speed * Time.deltaTime;
            }
            else
            {
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
            }
        }
        void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == LayerMask.NameToLayer("Characters"))
            {
                EffectOnCharacter(other);
            }
        }
        private void EffectOnCharacter(Collider other)
        {
            if (other.gameObject == LevelController.Instance.playerReference)
            {
                IngameEventsManager.Instance.RemoveTomatoAlert();
                IngameEventsManager.Instance.SetTomatoScreenEffect(effectTime);
            }
            else
            {
                //todo efect on IA
            }
            
            ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
        }
    }
}