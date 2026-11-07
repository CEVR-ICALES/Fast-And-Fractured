using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Windmill : MonoBehaviour
{
    [SerializeField] float maxSpeed =10f;
    [SerializeField] float windmillInitialSpeed;
    [SerializeField] float speedIncreasePerSecond = 1;
    private float _currentSpeed;
    [SerializeField, Min(0f)] private float loopVolume = 0.7f;
    private Utilities.Audio.IAudioVoice _loop;
    [SerializeField] GameObject transformVisual;
    [SerializeField] Vector3 rotationDirection = Vector3.forward;

    private void Start()
    {
        _currentSpeed = windmillInitialSpeed;
    }
    private void OnDisable()
    {
        FastAndFractured.GameplayAudio.Stop(ref _loop);
    }

    void Update()
    {
        if (_loop == null || !_loop.IsAlive) _loop = FastAndFractured.GameplayAudio.StartAttached(FastAndFractured.GameAudioCues.OtherSFXsWindmillSFX, gameObject, loopVolume);
        _currentSpeed = Mathf.Min(_currentSpeed + Time.deltaTime, maxSpeed);
        transformVisual.transform.Rotate(rotationDirection, _currentSpeed,Space.Self);

    }
}
