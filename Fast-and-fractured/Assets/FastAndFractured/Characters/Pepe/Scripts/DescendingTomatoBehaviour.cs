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
        public Vector3 randomRotation;
        public virtual void InitializeValues()
        {
            
        }

        void Update()
        {
            if (objective != null)
            {
                Vector3 direction = (objective.transform.position - transform.position).normalized;
                transform.position += direction * speed * Time.deltaTime;
                transform.Rotate(randomRotation * Time.deltaTime);
            }
            else
            {
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
            }
        }
        void OnTriggerEnter(Collider other)
        {
            //si el objetivo es el player quitara la alerta, independientemente de si a colisionado con otro objeto
            if(objective == LevelController.Instance.playerReference)
            {
                IngameEventsManager.Instance.RemoveTomatoAlert();
            }

            
            if (other.gameObject.layer == LayerMask.NameToLayer("Characters"))
            {
                StatsController statsController = other.GetComponent<StatsController>();
                if(statsController.IsInvulnerable)
                {
                    statsController.IsInvulnerable = false;
                    ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
                }
                else
                {
                    EffectOnCharacter(other);
                }
            }
        }
        private void EffectOnCharacter(Collider other)
        {
            if (other.gameObject == LevelController.Instance.playerReference)
            {
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