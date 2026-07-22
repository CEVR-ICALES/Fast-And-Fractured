using UnityEngine;

public enum ScreenShakeProfileType
{
    None,
    NormalShoot,
    PushShoot,
    StartDash,
    EndDash
}
[CreateAssetMenu(menuName = "ScreenShake/New profile")]
public class ScreenShakeProfile : ScriptableObject
{
    public ScreenShakeProfileType profileType;
    [Header("Impulse Source Settings")]
    public float impactTime = 0.2f;
    public float impactForce = 1f;
    public Vector3 defaultVelocity = new Vector3(0,-1,0);
    public AnimationCurve customImpulseShape;

    [Header("Impulse Listener Settings")]
    public float listenerAmplitude = 1f;
    public float listenerFrequency = 1f;
    public float listenerDuration = 1f;
   
}
