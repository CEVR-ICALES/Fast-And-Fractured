using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Utilities;
using Utilities.Managers.PauseSystem;
using System.Net.NetworkInformation;
namespace FastAndFractured
{
    public class SandstormController : MonoBehaviour, IKillCharacters, IPausable
    {
        public GameObject fogParent;
        public LocalVolumetricFog primaryFog;

        public float maxGrowthTime = 1.0f;
        [SerializeField]
        [Range(0.05f,0.9f)]
        private float colliderLessGrowMultiplicator = 0.7f;
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

        [SerializeField]
        private float fogDistancePlayerInsideSandstorm = 30f;
        [SerializeField]
        private float fogDistancePlayerOutsideSandstorm = 10f;
        [SerializeField]
        private float atenuationTime = 1f;

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
        public List<GameObject> ItemsInsideSandstorm =>_itemsInsideSandstorm;
        private List<GameObject> _itemsInsideSandstorm;
        public List<GameObject>  CharactersInsideSandstorm => _charactersInsideSandstorm;
        private List<GameObject> _charactersInsideSandstorm;
        public List<GameObject> SafeZonesInsideSandstorm =>_safeZonesInsideSandstorm;
        private List<GameObject> _safeZonesInsideSandstorm;

        [SerializeField] private GameObject minimapSandstormDirection;

        const float HALF_FRONT_ANGLE = 90;
        private const float MIN_VALUE_PER_SANDSTORM_DETECTION = 0.00000000001f;
        private void Start()
        {
            _stormCollider = GetComponent<BoxCollider>();
            _stormCollider.enabled = false;
            primaryFog?.gameObject.SetActive(false);
            _itemsInsideSandstorm = new List<GameObject>();
            _charactersInsideSandstorm = new List<GameObject>();
            _safeZonesInsideSandstorm = new List<GameObject>();
            primaryFog.parameters.meanFreePath = fogDistancePlayerOutsideSandstorm;
        }

        private void OnEnable()
        {
            PauseManager.Instance.RegisterPausable(this);
        }

        private void OnDisable()
        {
            PauseManager.Instance?.UnregisterPausable(this);
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
            fogParent.transform.position = new Vector3(_spawnPoint.x, fogParent.transform.position.y, _spawnPoint.z);;
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
            IngameEventsManager.Instance.CreateEvent("La tormenta de arena ha comenzado", 5f);

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
                float newZSizeCollider = _initialColliderSize.z + _currentGrowth * colliderLessGrowMultiplicator;
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

        public bool IsInsideStormCollider(GameObject target,float marginError)
        {
            if (marginError > 0)
            {
                Vector3 directionToTarget = target.transform.position - (transform.position  + (_stormCollider.size.z / 2 + marginError + _stormCollider.center.z) * transform.forward);
                directionToTarget.Normalize();
                float dotProduct = Vector3.Dot(transform.forward, directionToTarget);
                float angleToTarget = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
                return !(angleToTarget < HALF_FRONT_ANGLE);
            }
            else
            {
                return _charactersInsideSandstorm.Contains(target)||_itemsInsideSandstorm.Contains(target);
            }
        }

        private void ChangeSandstormVisuals(bool playerInside)
        {
            if (playerInside)
            {
                float progress = 1/(atenuationTime / Time.deltaTime);
                float actualFogDistance = primaryFog.parameters.meanFreePath;
                TimerSystem.Instance.CreateTimer(atenuationTime,onTimerDecreaseUpdate : (float time) => {
                    primaryFog.parameters.meanFreePath = Mathf.Lerp(actualFogDistance, fogDistancePlayerInsideSandstorm,progress);
                    progress += progress;
                });
            }
            else
            {
                primaryFog.parameters.meanFreePath = fogDistancePlayerOutsideSandstorm;
            }
        }

        private void OnTriggerEnter(Collider other)
        {   
            if (other.TryGetComponent(out StatsController statsController))
            {
                if (!other.GetComponent<Rigidbody>().isKinematic)
                {
                    StartKillNotify(statsController);
                    _charactersInsideSandstorm.Add(other.gameObject);
                    if (statsController.IsPlayer)
                    {
                        ChangeSandstormVisuals(true);
                    }
                }
            }
            else
            {
                if (other.TryGetComponent(out StatsBoostInteractable statsBoostInteractable))
                {
                    _itemsInsideSandstorm.Add(other.gameObject);
                }
                else
                {
                    _safeZonesInsideSandstorm.Add(other.gameObject);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.TryGetComponent(out StatsController statsController))
            {
                if (!other.GetComponent<Rigidbody>().isKinematic&&!_isPaused)
                {
                    if (other.GetComponentInParent<StatsBoostInteractable>() != null)
                    {
                        CharacterEscapedDead(statsController);
                        _charactersInsideSandstorm.Remove(other.gameObject);
                    }
                }
            }
            else
            {
                if (other.GetComponentInParent<StatsBoostInteractable>()!=null)
                {
                    _itemsInsideSandstorm.Remove(other.gameObject);
                }
                else
                {
                    _safeZonesInsideSandstorm.Remove(other.gameObject);
                }
            }
        }

        public void StartKillNotify(StatsController statsController)
       {
         float damageXFrame = statsController.Endurance/_currentCharacterKillTime;
         statsController.GetKilledNotify(this,false,damageXFrame);
       }

      public void CharacterEscapedDead(StatsController statsController)
      { 
        statsController.GetKilledNotify(this, true,0);  
      }

      public GameObject GetKillerGameObject()
      {
         return this.gameObject;
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
