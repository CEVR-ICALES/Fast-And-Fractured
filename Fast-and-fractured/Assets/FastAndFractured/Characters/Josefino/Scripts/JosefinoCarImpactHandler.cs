using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FastAndFractured
{
    public class JosefinoCarImpactHandler : CarImpactHandler
    {
        private JosefinoUniqueAbility _josefinoUniqueAbility;

        private void Awake()
        {
            _josefinoUniqueAbility = gameObject.GetComponent<JosefinoUniqueAbility>();
        }
    }

}

