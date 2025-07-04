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
                OnHasBeenPushed(otherCarPhysicsBehaviour);                
            }
        }

        public virtual ModifiedCarState CheckForModifiedCarState() => ModifiedCarState.DEFAULT;

        public virtual void OnHasPushedOtherCar(PhysicsBehaviour carPhysicsBehaviour)
        {
            // specific char logic to handle something if neccesary when pushing somebody (visuals, consuming something...)
        }

        public virtual void OnHasBeenPushed(PhysicsBehaviour carPhysicsBehaviour)
        {
            carPhysicsBehaviour.OnCarHasBeenPushed();
            // specific char logic to handle something if neccesary when getting pushed (visuals, consuming something....)
        }

        public virtual bool HandleIfTomatoEffect() 
        {
            return true;
            // specific char logic to handle getting hit by a tomatoe
        }

        public virtual float ApplyModifierToPushForceAsAttacker(float forceToApply, ModifiedCarState otherCarModifiedState, bool isFrontalHit, bool isOtherCarDahsing) => forceToApply; // override if is has a case where a modifier to the force can be applied

        public virtual float ApplyModifierToPushForceAsPushed(float forceToApply, ModifiedCarState otherCarModifiedState, bool isFrontalHit, bool isOtherCarDashing) => forceToApply;
    }
}

