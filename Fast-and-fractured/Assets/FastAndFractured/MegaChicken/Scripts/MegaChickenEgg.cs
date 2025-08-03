using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Utilities;
using StateMachine;
using Enums;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class MegaChickenEgg : MonoBehaviour, IPooledObject
    {
        private bool initValues = true;
        public Pooltype pooltype;
        public Pooltype Pooltype { get => pooltype; set => pooltype = value; }
        public bool InitValues => initValues;
        public virtual void InitializeValues()
        {
            
        }
        void OnEnable()
        {
            
        }
        void Start()
        {
            
        }
        
        void Update()
        {
            
        }
    }
}