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
            if (other.gameObject.layer == LayerMask.NameToLayer("Characters"))
            {
                EffectOnCharacter(other);
            }
        }
        private void EffectOnCharacter(Collider other)
        {
            Controller controller = other.GetComponent<Controller>();
            EnemyAIBrain enemyAI = other.GetComponent<EnemyAIBrain>();
            if(enemyAI != null)
            {
                // Apply effect on the player that got hit by the tomato
            }
            else if (controller != null)
            {
                // Apply effect on the character AI that got hit by the tomato
            }
            ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
        }
    }
}