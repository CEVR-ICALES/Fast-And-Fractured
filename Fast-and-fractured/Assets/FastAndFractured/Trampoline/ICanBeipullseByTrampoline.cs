using FastAndFractured.Utilities;
using UnityEngine;

public interface ICanBeImpulseByTrampoline
{
    public float GetMassReference();
    public ICustomRigidbody GetRigidbody();
}
