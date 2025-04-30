using UnityEngine;

namespace FastAndFractured
{
    public abstract class CarImpactHandler : MonoBehaviour
    {
        public void HandleOnCarImpact(bool isTheOneToPush, PhysicsBehaviour otherCarPhysicsBehaviour)
        {
            if (isTheOneToPush)
            {
                OnHasPushedOtherCar(otherCarPhysicsBehaviour);
            } else
            {                
                OnHasBeenPushed();                
            }
        }

        public virtual bool CheckForModifiedCarState()
        {
            return false; // default car doesnt have any kind of modified car state
        }

        public virtual void OnHasPushedOtherCar(PhysicsBehaviour otherCarPhysicsBehaviour)
        {
            otherCarPhysicsBehaviour.OnCarHasBeenPushed();
            // specific char logic to handle something if neccesary when pushing somebody
        }

        public virtual void OnHasBeenPushed()
        {
            // specific char logic to handle something if neccesary when getting pushed
        }

        public virtual float ApplyModifierToPushForce(float forceToApply) // override if is has a case where a modifier to the force can be applied
        {
            return forceToApply;
        }
    }
}

