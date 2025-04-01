using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public interface IInteractable
    {
        void OnInteract(GameObject interactionFrom, GameObject intearactionTo);

    }
}

