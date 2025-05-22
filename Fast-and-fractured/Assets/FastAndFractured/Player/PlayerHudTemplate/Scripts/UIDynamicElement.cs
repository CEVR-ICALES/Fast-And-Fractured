using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Enums;

namespace FastAndFractured
{
    public class UIDynamicElement : MonoBehaviour
    {
        public UIDynamicElementType elementType;
        public Image imageReference;
        public TextMeshProUGUI textReference;

        private void Awake()
        {
            // Auto-assign components if not manually assigned
            if (imageReference == null) imageReference = GetComponent<Image>();
            if (textReference == null) textReference = GetComponent<TextMeshProUGUI>();
        }
    }
}