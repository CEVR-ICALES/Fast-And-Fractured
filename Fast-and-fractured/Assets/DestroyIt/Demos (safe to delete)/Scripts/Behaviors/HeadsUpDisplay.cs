using UnityEngine;
using UnityEngine.UI;

namespace DestroyIt
{
    /// <summary>This script provides performance and basic control info for the demo scene.</summary>
    public class HeadsUpDisplay : MonoBehaviour
    {
        public RectTransform hud;
        [Tooltip("The number of times per second the UI updates.")]
        public float updateRate = 15f;
        public Text destroyedPrefabsText;
        public Text destroyedParticlesText;
        public Text debrisCountText;
        public Image reticleImage;
        public Image selectedWeapon;
        public Sprite unknownWeapon;
        public Sprite assaultRifle;
        public Sprite rocketLauncher;
        public Sprite fireAxe;
        public Sprite cannon;
        public Sprite nuke;
        public Sprite wrench;

        private float nextUpdate;

        private void Start()
        {
            if (DestructionManager.Instance != null)
            {
                DestructionManager.Instance.DestroyedPrefabCounterChangedEvent += OnDestroyedPrefabCounterChanged;
                DestructionManager.Instance.ActiveDebrisCounterChangedEvent += OnActiveDebrisCounterChanged;
            }
            if (ParticleManager.Instance != null)
                ParticleManager.Instance.ActiveParticlesCounterChangedEvent += OnActiveParticlesCounterChanged;

            OnDestroyedPrefabCounterChanged();
            OnActiveDebrisCounterChanged();
            OnActiveParticlesCounterChanged();
        }

        private void OnDisable()
        {
            // Unregister the event listeners when disabled/destroyed. Very important to prevent memory leaks due to orphaned event listeners!
            if (DestructionManager.Instance != null)
            {
                DestructionManager.Instance.DestroyedPrefabCounterChangedEvent -= OnDestroyedPrefabCounterChanged;
                DestructionManager.Instance.ActiveDebrisCounterChangedEvent -= OnActiveDebrisCounterChanged;
            }
            if (ParticleManager.Instance != null)
                ParticleManager.Instance.ActiveParticlesCounterChangedEvent -= OnActiveParticlesCounterChanged;
        }

        private void OnDestroyedPrefabCounterChanged()
        {
            // Destroyed Prefab Updates
            destroyedPrefabsText.text = "Destroyed Prefabs (last " + DestructionManager.Instance.withinSeconds + "s): " + DestructionManager.Instance.DestroyedPrefabCounter.Count;
        }

        private void OnActiveParticlesCounterChanged()
        {
            // Destroyed Particles Updates
            destroyedParticlesText.text = "Destroyed Particles (last " + ParticleManager.Instance.withinSeconds + "s): " + ParticleManager.Instance.ActiveParticles.Length;
        }

        private void OnActiveDebrisCounterChanged()
        {
            // Debris Count Updates
            debrisCountText.text = "Debris Count: " + DestructionManager.Instance.ActiveDebrisCount;
        }

        private void Update()
        {
            if (!(Time.time > nextUpdate)) return;

            nextUpdate = Time.time + (1.0f / updateRate);

            // HUD Visibility Updates
            int showHud = PlayerPrefs.GetInt("ShowHud", -1);
            hud.gameObject.SetActive(showHud == -1);

            // Reticle HUD Updates
            int showReticle = PlayerPrefs.GetInt("ShowReticle", -1);
            reticleImage.gameObject.SetActive(showReticle == -1);

            if (InputManager.Instance != null)
            {
                // Selected Weapon Silhouette Update
                switch (InputManager.Instance.SelectedWeapon)
                {
                    case WeaponType.Gun:
                        selectedWeapon.sprite = assaultRifle;
                        break;
                    case WeaponType.Rocket:
                        selectedWeapon.sprite = rocketLauncher;
                        break;
                    case WeaponType.Melee:
                        selectedWeapon.sprite = fireAxe;
                        break;
                    case WeaponType.Cannonball:
                        selectedWeapon.sprite = cannon;
                        break;
                    case WeaponType.Nuke:
                        selectedWeapon.sprite = nuke;
                        break;
                    case WeaponType.RepairWrench:
                        selectedWeapon.sprite = wrench;
                        break;
                    default: // This should never happen.
                        selectedWeapon.sprite = unknownWeapon;
                        break;
                }
            }
        }
    }
}