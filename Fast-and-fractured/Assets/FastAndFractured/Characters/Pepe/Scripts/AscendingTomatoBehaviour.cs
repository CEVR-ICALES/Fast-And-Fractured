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
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => initValues;
        [SerializeField] private bool initValues = true;
        public Pooltype pooltypeDescendingTomato;
        public GameObject tomatoPrefab;
        public GameObject Caster;
        public Vector3 OriginPosition;
        public float speed = 100f;
        public float effectDistance;
        public float descendingTomatoSpeed = 100f;
        public virtual void InitializeValues()
        {
            
        }
        
        void Start()
        {
            TimerSystem.Instance.CreateTimer(3f, onTimerDecreaseComplete: () =>
            {
                foreach (GameObject obj in FindObjectsOfType<GameObject>())
                {
                    if (obj.GetComponent<StatsController>() != null)
                    {
                        if (!obj.transform.IsChildOf(Caster.transform))
                        {
                            GameObject tomato = ObjectPoolManager.Instance.GivePooledObject(pooltypeDescendingTomato);
                            if(tomato!=null)
                            {
                                tomato.transform.position = obj.transform.position + new Vector3(0, 300, 0);;
                                tomato.transform.rotation = Quaternion.Euler(0, 0, 0);
                                DescendingTomatoBehaviour descendingTomatoBehaviour = tomato.GetComponent<DescendingTomatoBehaviour>();
                                descendingTomatoBehaviour.speed = descendingTomatoSpeed;
                                descendingTomatoBehaviour.objective = obj;
                                descendingTomatoBehaviour.pooltype = pooltypeDescendingTomato;
                            }
                        }
                    }
                }
                ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
            });
        }

        void Update()
        {
            transform.position += transform.up * speed * Time.deltaTime;
            // Add a way to alert players in range that a tomato is coming, if the player manages to get out of the
            // range in time the alert disappears
        }
    }
}