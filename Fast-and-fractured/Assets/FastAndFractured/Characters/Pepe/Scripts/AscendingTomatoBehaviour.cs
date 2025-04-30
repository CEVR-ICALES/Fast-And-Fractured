using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities;
using StateMachine;
using Enums;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class AscendingTomatoBehaviour : MonoBehaviour, IPooledObject, IPausable
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

        public float effectTime = 5f;
        private List<GameObject> charactersList;

        private Vector3 _randomRotation;
        private bool _isPaused = false;

        public virtual void InitializeValues()
        {
            
        }

        void OnEnable()
        {
            PauseManager.Instance?.RegisterPausable(this);
        }

        void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
        }

        public void StartTimer()
        {
            charactersList = LevelController.Instance.InGameCharacters;
            _randomRotation = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
            TimerSystem.Instance.CreateTimer(ascendingTime, onTimerDecreaseComplete: () =>
            {
                if (!Caster)
                {
                    return;
                }
                foreach (GameObject obj in charactersList)
                {
                    if(obj!=null)
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
            if (_isPaused)
                return;
            GameObject player= LevelController.Instance.playerReference;

            if (Caster == null) return;
            if (player&&!player.transform.IsChildOf(Caster.transform))
            {
                float distance = Vector3.Distance(OriginPosition, LevelController.Instance.playerReference.transform.position);
                if (distance<=effectDistance)
                {
                    if(!IngameEventsManager.Instance.IsTomatoAlertActive)
                    {
                        IngameEventsManager.Instance.SetTomatoAlert();
                    }
                }
                else
                {
                    if(IngameEventsManager.Instance.IsTomatoAlertActive)
                    {
                        IngameEventsManager.Instance.RemoveTomatoAlert();
                    }
                }
            }
            transform.position += Vector3.up * speed * Time.deltaTime;
            transform.Rotate(_randomRotation * Time.deltaTime);
        }
        private void SetTomatoVariables(GameObject tomato, GameObject obj)
        {
            tomato.transform.position = transform.position;
            tomato.transform.rotation = Quaternion.Euler(0, 0, 0);
            DescendingTomatoBehaviour descendingTomatoBehaviour = tomato.GetComponent<DescendingTomatoBehaviour>();
            descendingTomatoBehaviour.speed = descendingTomatoSpeed;
            descendingTomatoBehaviour.objective = obj;
            descendingTomatoBehaviour.pooltype = pooltypeDescendingTomato;
            descendingTomatoBehaviour.effectTime = effectTime;
            descendingTomatoBehaviour.randomRotation = new Vector3(Random.Range(0, 360), Random.Range(0, 360), Random.Range(0, 360));
        }

        public void OnPause()
        {
            _isPaused = true;
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }
}