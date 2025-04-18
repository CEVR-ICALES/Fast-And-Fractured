using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Utilities;
using Utilities.Managers.PauseSystem;
namespace FastAndFractured
{
    public class SandstormController : MonoBehaviour, IKillCharacters, IPausable
    {
        public GameObject fogParent;
        public LocalVolumetricFog primaryFog;

        public float maxGrowthTime = 1.0f;

        private float _maxGrowth;
        private float _growthSpeed;

        [SerializeField]
        private GameObject gameObjectSpawnPoint;
        [SerializeField]
        private GameObject gameObjectMirroPoint;

        private BoxCollider _stormCollider;

        private Vector3 _spawnPoint;
        private Vector3 _mirrorPoint;
        private Vector3 _direction;

        private float _currentGrowth = 0f;

        private Vector3 _initialVolumeSizeMain;
        private Vector3 _initialColliderSize;

        [SerializeField]
        private Transform sphereCenter;
        [SerializeField]
        private float sphereRadius = 500f;
        [SerializeField]
        private float maxAngleExcluded = 360f;
        [SerializeField]
        private int numberOfPoints = 18;
        public bool MoveSandStorm { get => _moveSandStorm; set => _moveSandStorm = value; }
        private bool _moveSandStorm = false;

        public bool StormSpawnPointsSetted { get => _spawnPointsSet; }

        private bool _spawnPointsSet = false;
        [SerializeField]
        private float maxCharacterKillTime = 10;
        [SerializeField]
        private float minCharacterKillTime = 3;
        public int KillPriority { get => killCharacterPriority;}
        public float KillTime { get => _currentCharacterKillTime;}
        private float _currentCharacterKillTime;
        private float _reductionKillTime;
        [SerializeField]
        public int killCharacterPriority = 1;
        [SerializeField]
        private int reduceQuantityPoints = 4;
        private float _timeToReduceKillCharacterTime;
        private ITimer _reduceKillTimeTimer;

        private bool _isPaused = false;

        [SerializeField] private GameObject minimapSandstormDirection;

        const float HALF_FRONT_ANGLE = 90;
        private void Start()
        {
            _stormCollider = GetComponent<BoxCollider>();
            _stormCollider.enabled = false;
            primaryFog?.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            PauseManager.Instance.RegisterPausable(this);
        }

        private void OnDisable()
        {
            PauseManager.Instance.UnregisterPausable(this);
        }
        private void Update()
        {
            if (!_isPaused)
            {
                if (_moveSandStorm)
                {
                    ExpandFogs();
                }
            }
        }

        public void SetSpawnPoints(bool debug)
        {
            float[] possibleAngels = new float[numberOfPoints];
            float nextAngleFactor = maxAngleExcluded / numberOfPoints;
            float currentAngle = 0;
            for (int countAngle = 0; countAngle < numberOfPoints; countAngle++)
            {
                possibleAngels[countAngle] = currentAngle;
                currentAngle += nextAngleFactor;
            }
            Vector3 spawnVector = sphereCenter.forward;
            //Probably change for better aplication. Like an utility
            LevelController.Instance.ShuffleList(possibleAngels);

            float spawnAngle = possibleAngels[0];

            Debug.Log("Spawn Point Angle : " + spawnAngle);
            Quaternion vectorRotation = Quaternion.AngleAxis(spawnAngle, Vector3.up);
            Vector3 rotatedSpawnVector = vectorRotation * sphereCenter.forward;

            _spawnPoint = sphereCenter.position + rotatedSpawnVector * sphereRadius;
            _mirrorPoint = sphereCenter.position - rotatedSpawnVector * sphereRadius;
            _spawnPointsSet = true;

            _maxGrowth = (_mirrorPoint - _spawnPoint).magnitude;

            _timeToReduceKillCharacterTime = maxGrowthTime / reduceQuantityPoints;
            _reductionKillTime = (maxCharacterKillTime - minCharacterKillTime) / reduceQuantityPoints;

            if (debug)
            {
                gameObjectSpawnPoint.transform.position = _spawnPoint;
                gameObjectMirroPoint.transform.position = _mirrorPoint;
                Debug.DrawLine(sphereCenter.position, _spawnPoint, Color.green, 100);
                Debug.DrawLine(sphereCenter.position, _mirrorPoint, Color.red, 100);
                Debug.DrawLine(_spawnPoint, _mirrorPoint, Color.red, 100);
            }
        }
        /// <summary>
        /// Spawns the Fogs at a random point (hardcoded values for now)
        /// </summary>
        public void SpawnFogs()
        {
            _currentCharacterKillTime = maxCharacterKillTime;
            fogParent.transform.position = _spawnPoint;
            primaryFog?.gameObject.SetActive(true);
            _direction = (_mirrorPoint - _spawnPoint).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(_direction);
            fogParent.transform.rotation = targetRotation;
            _stormCollider.enabled = true;
            if (primaryFog != null)
            {
                _initialVolumeSizeMain = primaryFog.parameters.size;
                _stormCollider.size = new Vector3(_initialVolumeSizeMain.x, _initialVolumeSizeMain.y, _initialVolumeSizeMain.z);
            }
            _initialColliderSize = _stormCollider.size;
            _growthSpeed = (_maxGrowth) / maxGrowthTime;

            minimapSandstormDirection.GetComponent<SandstormDirectionMinimap>().SetSandstormDirection(_direction);
        }

        /// <summary>
        /// Expands the scale and movement of the fog to cover up all the map
        /// </summary>
        private void ExpandFogs()
        {

            if (_currentGrowth < _maxGrowth)
            {
                _currentGrowth += _growthSpeed * Time.deltaTime;

                if (_currentGrowth > _maxGrowth)
                    _currentGrowth = _maxGrowth;
                if (primaryFog != null)
                {
                    float newZSizeMain = _initialVolumeSizeMain.z + _currentGrowth;

                    primaryFog.parameters.size = new Vector3(_initialVolumeSizeMain.x, _initialVolumeSizeMain.y, newZSizeMain);
                }
                float newZSizeCollider = _initialColliderSize.z + _currentGrowth;
                _stormCollider.size = new Vector3(_initialColliderSize.x, _initialColliderSize.y, newZSizeCollider);
                Vector3 offset = _direction * _growthSpeed*0.5f * Time.deltaTime;

                if ((fogParent.transform.position - _spawnPoint).magnitude < _maxGrowth / 2)
                {
                    fogParent.transform.position += offset;
                }
                if (_reduceKillTimeTimer==null)
                {
                 _reduceKillTimeTimer = TimerSystem.Instance.CreateTimer(_timeToReduceKillCharacterTime, Enums.TimerDirection.INCREASE, onTimerIncreaseComplete: () =>
                   {
                       _currentCharacterKillTime -= _reductionKillTime;

                       _reduceKillTimeTimer = null;
                   });
                }
            }
        }

        public bool IsInsideStormCollider(Transform target)
        {
            Vector3 directionToTarget = target.position - (transform.position +  _stormCollider.size.z/2 * transform.forward);
            directionToTarget.Normalize();
            float dotProduct = Vector3.Dot(transform.forward, directionToTarget);
            float angleToTarget = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
            return !(angleToTarget < (HALF_FRONT_ANGLE));
        }

        private void OnTriggerEnter(Collider other)
        {
            if(other.TryGetComponent(out StatsController statsController))
            {
               StartKillNotify(statsController);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if(other.TryGetComponent(out StatsController statsController))
            {
               CharacterEscapedDead(statsController);
            }
        }

       public void StartKillNotify(StatsController statsController)
       {
         statsController.GetKilledNotify(this,false);
       }

      public void CharacterEscapedDead(StatsController statsController)
      {
            statsController.GetKilledNotify(this, true);  
      }

        public void OnPause()
        {
            _isPaused = true;
            
        }

        public void OnResume()
        {
            _isPaused = false;
        }
    }
}
