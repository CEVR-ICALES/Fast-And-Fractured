using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateMachine;
using Enums;
using Utilities;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    public class DescendingTomatoBehaviour : MonoBehaviour, IPooledObject, IPausable 
    {
        private bool initValues = true;
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => initValues;
        public GameObject objective;
        public float speed;
        public float effectTime = 3f;
        public Vector3 randomRotation;
        private bool _isPaused = false;
        [Tooltip("Percent between 0.0 and 1.0")]
        [SerializeField] private float percentDebuffSpeed = 0.5f;

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

        void Update()
        {
            if (_isPaused)
                return;

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
            if(objective == LevelControllerButBetter.Instance.playerReference)
            {
                IngameEventsManager.Instance.RemoveAlert();
            }

            
            if (other.gameObject.layer == LayerMask.NameToLayer("Characters"))
            {
                if (other.TryGetComponent(out CarImpactHandler otherCarImpactHandler))
                {
                    if(!otherCarImpactHandler.HandleIfTomatoEffect())
                    {
                        ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);

                    } else
                    {
                        EffectOnCharacter(other);
                    }
                }
            }
        }
        private void EffectOnCharacter(Collider other)
        {
            if (other.gameObject == LevelControllerButBetter.Instance.playerReference)
            {
                HUDManager.Instance.UpdateUIEffect(UIDynamicElementType.BAD_EFFECTS, ResourcesManager.Instance.GetResourcesSprite(ScreenEffects.TOMATO_EFFECT), effectTime);
            }
            else
            {
                StatsController stats = other.GetComponentInChildren<StatsController>();
                stats.TemporalProductStat(Stats.MAX_SPEED, percentDebuffSpeed, effectTime);
                //todo efect on IA
            }
            
            ObjectPoolManager.Instance.DesactivatePooledObject(this, gameObject);
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