using UnityEngine;
using Enums;

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

        public virtual ModifiedCarState CheckForModifiedCarState() => ModifiedCarState.DEFAULT;

        public virtual void OnHasPushedOtherCar(PhysicsBehaviour otherCarPhysicsBehaviour)
        {
            otherCarPhysicsBehaviour.OnCarHasBeenPushed();
            // specific char logic to handle something if neccesary when pushing somebody (visuals, consuming something...)
        }

        public virtual void OnHasBeenPushed()
        {
            // specific char logic to handle something if neccesary when getting pushed (visuals, consuming something....)
        }

        public virtual float ApplyModifierToPushForceAsAttacker(float forceToApply, ModifiedCarState otherCarModifiedState, bool isFrontalHit, bool isOtherCarDahsing) => forceToApply; // override if is has a case where a modifier to the force can be applied

        public virtual float ApplyModifierToPushForceAsPushed(float forceToApply, ModifiedCarState otherCarModifiedState, bool isFrontalHit, bool isOtherCarDashing) => forceToApply;
    }
}

