using UnityEngine;

namespace FastAndFractured.Abstractions
{
    /// <summary>Anything that can start the shooting actions, so the state machine works with local or networked shooting.</summary>
    public interface IShootController
    {
        void TryNormalShoot();
        void TryPushShoot();
        void TryMineShoot();
    }
}
