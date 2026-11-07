using UnityEngine;
using Utilities.Audio;

namespace FastAndFractured
{
    [DisallowMultipleComponent]
    public sealed class VehicleAudioController : MonoBehaviour
    {
        private const string ENGINE_STATE_PARAMETER = "CarState";
        private const string ENGINE_STARTING_LABEL = "Starting";
        private const string ENGINE_RUNNING_LABEL = "Running";
        private enum EngineState { UNKNOWN, STARTING, RUNNING }

        [SerializeField] private string engineCue;

        private VehicleAudioSettings _settings;
        private StatsController _stats;
        private CarMovementController _movement;
        private NormalShootHandle _shoot;
        private Rigidbody _body;
        private IAudioVoice _engine;
        private IAudioVoice _frying;
        private IAudioVoice _overheatFire;
        private IAudioVoice _fallingWind;
        private bool _started;
        private bool _dead;
        private bool _dashing;
        private bool _overheated;
        private bool _invulnerable;
        private bool _falling;
        private float _airTime;
        private float _endurance;
        private EngineState _engineState = EngineState.UNKNOWN;
        private float _nextCollision;
        private float _nextHurt;
        private bool _engineHasState;

        #region Lifecycle
        private void OnEnable()
        {
            _settings = AudioPresentationProfile.Load().Vehicle;
            _stats = GetComponent<StatsController>();
            _movement = GetComponent<CarMovementController>();
            _body = GetComponent<Rigidbody>();
            _shoot = GetComponentInChildren<NormalShootHandle>(true);
            _started = _dead = false;
            if (_stats != null) _stats.onDead?.AddListener(OnDeathEvent);
            _engineState = EngineState.UNKNOWN;
            _engineHasState = true;
            _airTime = 0;
            _falling = _dashing = _overheated = _invulnerable = false;
        }

        private void LateUpdate()
        {
            if (_dead || !GameplayAudio.CanPlay || _stats == null || _stats.CharacterData == null || _movement == null || _body == null) return;
            var level = LevelController.ExistingInstance;
            if (level == null || !level.HasGameStarted) return;
            if (!_started)
            {
                if (!GameplayAudio.IsReady) return;
                _endurance = _stats.Endurance;
                AudioCueId cue = string.IsNullOrEmpty(engineCue) ? GameplayAudio.EngineCueFor(_stats.CharacterData.CharacterName) : new AudioCueId(engineCue);
                _engine = GameplayAudio.StartAttached(cue, gameObject, _settings.InitialEngineVolume,
                    new[] { new AudioParameter { Name = ENGINE_STATE_PARAMETER, Label = ENGINE_STARTING_LABEL } });
                _started = true;
            }
            float health = _stats.Endurance;
            if (health <= 0) { StopLoops(); return; }
            if (health < _endurance && Time.time >= _nextHurt && _stats.CharacterData.CharacterName.ToLowerInvariant().Contains("josefino"))
            {
                GameplayAudio.Play(GameAudioCues.CommonJosefinoHurt, transform.position);
                _nextHurt = Time.time + _settings.HurtCooldownSeconds;
            }
            if (health > _endurance) GameplayAudio.PlayPersonal(GameAudioCues.CommonHeal, _stats);
            _endurance = health;

            if (_movement.IsDashing && !_dashing) GameplayAudio.Play(GameAudioCues.CommonDash, transform.position);
            _dashing = _movement.IsDashing;
            bool grounded = _movement.IsGrounded();
            _airTime = grounded ? 0f : _airTime + Time.deltaTime;
            if (!_falling && _airTime > _settings.FallingDelaySeconds && _body.linearVelocity.y < _settings.FallingVerticalSpeed)
            {
                _fallingWind = GameplayAudio.StartAttached(GameAudioCues.CommonFallingWind, gameObject);
                _falling = true;
            }
            if (grounded && _falling) { GameplayAudio.Stop(ref _fallingWind); _falling = false; }

            if (_engine != null && _engine.IsAlive)
            {
                float speed = _body.linearVelocity.magnitude;
                _engine.SetPitch(_settings.CalculateEnginePitch(speed));
                EngineState state = speed > _settings.EngineRunningSpeed ? EngineState.RUNNING : EngineState.STARTING;
                if (state != _engineState && _engineHasState)
                    _engineHasState = _engine.TrySetParameterLabel(ENGINE_STATE_PARAMETER, state == EngineState.RUNNING ? ENGINE_RUNNING_LABEL : ENGINE_STARTING_LABEL);
                _engineState = state;
                _engine.SetVolume(GameplayAudio.IsLocal(_stats) ? _settings.LocalEngineVolume : _settings.RemoteEngineVolume);
            }

            bool hot = _shoot != null && _shoot.IsOverHeat;
            if (hot != _overheated && GameplayAudio.IsLocal(_stats))
            {
                GameplayAudio.Play(hot ? GameAudioCues.CommonOverheat : GameAudioCues.CommonCoolingComplete, transform.position);
                if (hot) _overheatFire = GameplayAudio.StartAttached(GameAudioCues.CommonOverheatFire, gameObject);
                else GameplayAudio.Stop(ref _overheatFire);
                if (hot && _stats.CharacterData.CharacterName.ToLowerInvariant().Contains("maria"))
                    _frying = GameplayAudio.StartAttached(GameAudioCues.CommonFryingOverheat, gameObject);
                else GameplayAudio.Stop(ref _frying);
            }
            _overheated = hot;
            bool shield = _stats.IsInvulnerable && _stats.CharacterData.CharacterName.ToLowerInvariant().Contains("josefino");
            if (shield != _invulnerable)
                GameplayAudio.Play(shield ? GameAudioCues.CommonShieldActivate : GameAudioCues.CommonShieldEnd, transform.position);
            _invulnerable = shield;
        }

        private void OnDisable()
        {
            if (_stats != null) _stats.onDead?.RemoveListener(OnDeathEvent);
            StopLoops();
        }
        #endregion

        #region Collision Feedback
        private void OnCollisionEnter(Collision collision)
        {
            if (!_started || !GameplayAudio.CanPlay || _stats == null || _stats.Endurance <= 0 || Time.time < _nextCollision) return;
            float speed = collision.relativeVelocity.magnitude;
            if (speed < _settings.MinimumCollisionSpeed) return;
            var other = collision.collider.GetComponentInParent<VehicleAudioController>();
            if (other != null && other != this && GetInstanceID() > other.GetInstanceID()) return;
            float mass = collision.rigidbody != null ? Mathf.Min(_body.mass, collision.rigidbody.mass) : _body.mass;
            float volume = _settings.CalculateCollisionVolume(speed, mass);
            GameplayAudio.Play(other != null ? GameAudioCues.CommonCarCollision : GameAudioCues.CommonStaticCollision, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position, volume);
            _nextCollision = Time.time + _settings.CollisionCooldownSeconds;
        }
        #endregion

        #region Voice Lifetime
        private void StopLoops()
        {
            GameplayAudio.Stop(ref _engine);
            GameplayAudio.Stop(ref _frying);
            GameplayAudio.Stop(ref _fallingWind);
            GameplayAudio.Stop(ref _overheatFire);
        }

        public void OnDeath()
        {
            if (_dead) return;
            if (_falling) GameplayAudio.Play(GameAudioCues.CommonFalling, transform.position);
            _dead = true;
            StopLoops();
            GetComponent<MariaAntoniaUniqueAbility>()?.StopAbilityAudio();
        }

        private void OnDeathEvent(float delay, GameObject victim, bool isPlayer) => OnDeath();
        #endregion
    }
}
