using UnityEngine;

namespace DestroyIt
{
    public class PoweredTag : MonoBehaviour
    {
        public PowerSource powerSource;

        void Update()
        {
            if (powerSource == null || !powerSource.hasPower)
                gameObject.RemoveTag(Tag.Powered);
            else 
                gameObject.AddTag(Tag.Powered);
        }
    }
}