using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using System.Collections.Generic;
using Utilities;
namespace FastAndFractured
{
    public class SandstormController : MonoBehaviour, IKillCharacters
    {
        public GameObject fogParent;
        public LocalVolumetricFog primaryFog;
        public LocalVolumetricFog secondaryFog;

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
        private Vector3 _initialVolumeSizeSecondary;

        [SerializeField]
        private Transform sphereCenter;
        [SerializeField]
        private float sphereRadius = 374.6f;
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
        public int killPriority { get => killCharacterPriority;}
        public float killTime { get => _currentCharacterKillTime;}
        private float _currentCharacterKillTime;
        private float _reductionKillTime;
        [SerializeField]
        public int killCharacterPriority = 1;
        [SerializeField]
        private int reduceQuantityPoints = 4;
        private float _timeToReduceKillCharacterTime;
        private ITimer _reduceKillTimeTimer;
        private void Start()
        {
            _stormCollider = GetComponent<BoxCollider>();
            _stormCollider.enabled = false;
            primaryFog.gameObject.SetActive(false);
            secondaryFog.gameObject.SetActive(false);
        }
        private void Update()
        {
            if (_moveSandStorm)
            {
                ExpandFogs();
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
            primaryFog.gameObject.SetActive(true);
            secondaryFog.gameObject.SetActive(true);
            _direction = (_mirrorPoint - _spawnPoint).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(_direction);
            fogParent.transform.rotation = targetRotation;

            _initialVolumeSizeMain = primaryFog.parameters.size;
            _initialVolumeSizeSecondary = secondaryFog.parameters.size;

            _growthSpeed = (_maxGrowth)/maxGrowthTime;
            _stormCollider.enabled = true;
            _stormCollider.size = new Vector3(_initialVolumeSizeMain.x, _initialVolumeSizeMain.y, _initialVolumeSizeMain.z);
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

                float newZSizeMain = _initialVolumeSizeMain.z + _currentGrowth;
                float newZSizeSecondary = _initialVolumeSizeSecondary.z + _currentGrowth;

                primaryFog.parameters.size = new Vector3(_initialVolumeSizeMain.x, _initialVolumeSizeMain.y, newZSizeMain);
                secondaryFog.parameters.size = new Vector3(_initialVolumeSizeSecondary.x, _initialVolumeSizeSecondary.y, newZSizeSecondary);
                _stormCollider.size = new Vector3(_initialVolumeSizeMain.x, _initialVolumeSizeMain.y, newZSizeMain);
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
    }
}
