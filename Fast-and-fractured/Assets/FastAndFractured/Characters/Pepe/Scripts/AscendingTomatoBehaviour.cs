using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using StateMachine;
using Enums;
namespace FastAndFractured
{
    public class AscendingTomatoBehaviour : MonoBehaviour, IPooledObject
    {
        private bool initValues = true;
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => initValues;
        public Pooltype pooltypeDescendingTomato;
        public GameObject Caster;
        public Vector3 OriginPosition;
        public float speed = 100f;
        public float effectDistance;
        public float descendingTomatoSpeed = 100f;
        public float ascendingTime = 3f;
        private Vector3 randomRotation;
        public virtual void InitializeValues()
        {
            
        }
        
        
        public void StartTimer()
        {
            randomRotation = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            TimerSystem.Instance.CreateTimer(ascendingTime, onTimerDecreaseComplete: () =>
            {
                //TODO cambiar el FindObjectsOfType cuando el level manager este terminado
                foreach (GameObject obj in FindObjectsOfType<GameObject>())
                {
                    if (obj.GetComponent<StatsController>() != null)
                    {
                        float distance = Vector3.Distance(OriginPosition, obj.transform.position);
                        if(distance<=effectDistance)
                        {
                            if (!obj.transform.IsChildOf(Caster.transform))
                            {
                                GameObject tomato = ObjectPoolManager.Instance.GivePooledObject(pooltypeDescendingTomato);
                                if(tomato!=null)
                                {
                                    SetTomatoVariables(tomato, obj);
                                }
                            }
                        }
                    }
                }
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
            });
        }

        void Update()
        {
            transform.position += Vector3.up * speed * Time.deltaTime;
            transform.Rotate(randomRotation * Time.deltaTime);
            // Add a way to alert players in range that a tomato is coming, if the player manages to get out of the
            // range in time the alert disappears
        }
        private void SetTomatoVariables(GameObject tomato, GameObject obj)
        {
            tomato.transform.position = transform.position;
            tomato.transform.rotation = Quaternion.Euler(0, 0, 0);
            DescendingTomatoBehaviour descendingTomatoBehaviour = tomato.GetComponent<DescendingTomatoBehaviour>();
            descendingTomatoBehaviour.speed = descendingTomatoSpeed;
            descendingTomatoBehaviour.objective = obj;
            descendingTomatoBehaviour.pooltype = pooltypeDescendingTomato;
            descendingTomatoBehaviour.randomRotation = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
        }
    }
}