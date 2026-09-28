using UnityEngine;
using Enums;
using Utilities;

namespace FastAndFractured
{
    public class RecoverSelectedMenuController : MonoBehaviour
    {   
        [System.Serializable]
        public class DefaultSelectedButton
        {
            public ScreensType screenType;
            public GameObject defaultSelectedButton;
        }

        [SerializeField] private DefaultSelectedButton[] defaultSelectedButtons;
        void Start()
        {
            
        }

        void Update()
        {
            
        }
    }
}