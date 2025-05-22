using FMODUnity;
using Utilities;

public class CustomStudioEventEmitter : StudioEventEmitter
{
    protected override void PlayInstance()
    {
        base.PlayInstance();
        SoundManager.Instance.AddEventInstance(instance);
    }
}
