using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace FastAndFractured
{
    public class SandstormController : MonoBehaviour
    {
        public GameObject fogParent;
        public LocalVolumetricFog primaryFog;
        public LocalVolumetricFog secondaryFog;

        public float growthSpeed = 1.0f;
        [SerializeField]
        private GameObject spawnPoint;
        [SerializeField]
        private GameObject mirroPoint;

        private Vector3 _spawnPoint;
        private Vector3 _mirrorPoint;
        private Vector3 _direction;

        private float _currentGrowth = 0f;

        private Vector3 _initialVolumeSizeMain;
        private Vector3 _initialVolumeSizeSecondary;

        private Vector3 _initialPositionMain;
        private Vector3 _initialPositionSecondary;

        [SerializeField]
        private Transform sphereCenter;
        [SerializeField]
        private float sphereRadius = 374.6f;
        public bool MoveSandStorm { get => _moveSandStorm; set => _moveSandStorm = value; }
        private bool _moveSandStorm = false;
        private void Start()
        {
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
        /// <summary>
        /// Spawns the Fogs at a random point (hardcoded values for now)
        /// </summary>
        public void SpawnFogs()
        {
            Vector3 spawnVector = sphereCenter.position + sphereCenter.forward;
            float angle = Random.Range(0, 360);
            Quaternion vectorRotation = Quaternion.AngleAxis(angle, Vector3.up);
            Vector3 rotatedSpawnVector = vectorRotation * spawnVector;
            _spawnPoint = new Vector3(rotatedSpawnVector.normalized.x * sphereRadius, fogParent.transform.position.y, rotatedSpawnVector.normalized.z * sphereRadius);

            fogParent.transform.position = _spawnPoint;
            Vector3 mirrorVector = (sphereCenter.position - _spawnPoint) * 2;
            _mirrorPoint = new Vector3(mirrorVector.x, fogParent.transform.position.y, mirrorVector.z);
            spawnPoint.transform.position = _spawnPoint;
            mirroPoint.transform.position = _mirrorPoint;
            _direction = (_mirrorPoint - _spawnPoint).normalized;

            primaryFog.gameObject.SetActive(true);
            secondaryFog.gameObject.SetActive(true);

            Quaternion targetRotation = Quaternion.LookRotation(_direction);
            fogParent.transform.rotation = targetRotation;

            _initialVolumeSizeMain = primaryFog.parameters.size;
            _initialVolumeSizeSecondary = secondaryFog.parameters.size;

            _initialPositionMain = primaryFog.transform.position;
            _initialPositionSecondary = secondaryFog.transform.position;
        }

        /// <summary>
        /// Expands the scale and movement of the fog to cover up all the map
        /// </summary>
        private void ExpandFogs()
        {
            float maxGrowth = (_mirrorPoint - _spawnPoint).magnitude;

            if (_currentGrowth < maxGrowth)
            {
                _currentGrowth += growthSpeed * Time.deltaTime;

                if (_currentGrowth > maxGrowth)
                    _currentGrowth = maxGrowth;
            }

            float newZSizeMain = _initialVolumeSizeMain.z + _currentGrowth;
            float newZSizeSecondary = _initialVolumeSizeSecondary.z + _currentGrowth;

            primaryFog.parameters.size = new Vector3(_initialVolumeSizeMain.x, _initialVolumeSizeMain.y, newZSizeMain);
            secondaryFog.parameters.size = new Vector3(_initialPositionSecondary.x, _initialPositionSecondary.y, newZSizeSecondary);

            Vector3 offset = _direction * (_currentGrowth / 2f);

            primaryFog.transform.position = _spawnPoint + offset;
            secondaryFog.transform.position = _spawnPoint + offset;
        }
    }
}
