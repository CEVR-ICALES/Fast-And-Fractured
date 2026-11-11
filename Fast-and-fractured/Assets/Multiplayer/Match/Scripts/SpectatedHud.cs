using Enums;
using UnityEngine;
using UnityEngine.Events;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// While spectating, the match HUD shows the watched car (endurance, speed, portrait, abilities, cooldowns seen
    /// here) through the same HUDManager elements. Own-car-only elements stay hidden.
    /// </summary>
    public sealed class SpectatedHud
    {
        private const float SPEED_TO_KMH = 3.6f;

        /// <summary>Elements that show the watched car.</summary>
        public static readonly UIDynamicElementType[] Mirrored =
        {
            UIDynamicElementType.HEALTH_BAR,
            UIDynamicElementType.SPEED_INDICATOR,
            UIDynamicElementType.SELECTED_PLAYER_ICON,
            UIDynamicElementType.DASH_COOLDOWN,
            UIDynamicElementType.ULT_COOLDOWN,
            UIDynamicElementType.PUSH_COOLDOWN,
            UIDynamicElementType.SHOOT_COOLDOWN,
            UIDynamicElementType.DASH_ICON,
            UIDynamicElementType.ULT_ICON,
            UIDynamicElementType.PUSH_ICON,
            UIDynamicElementType.SHOOT_ICON,
        };

        private StatsController _stats;
        private PhysicsBehaviour _physics;
        private NormalShootHandle _shoot;
        private PushShootHandle _push;
        private CarMovementController _movement;
        private BaseUniqueAbility _ability;
        private bool _visible = true;
        private string _lastSpeed;

        public bool Visible => _visible;

        /// <summary>Watch <paramref name="car"/> (the car's StatsController transform).</summary>
        public void Bind(Transform car)
        {
            Unbind();
            if (car == null) return;
            Transform root = car.GetComponentInParent<FishNet.Object.NetworkObject>(true)?.transform ?? car;
            _stats = car.GetComponent<StatsController>() ?? root.GetComponentInChildren<StatsController>(true);
            _physics = car.GetComponent<PhysicsBehaviour>() ?? root.GetComponentInChildren<PhysicsBehaviour>(true);
            _shoot = root.GetComponentInChildren<NormalShootHandle>(true);
            _push = root.GetComponentInChildren<PushShootHandle>(true);
            _movement = root.GetComponentInChildren<CarMovementController>(true);
            _ability = root.GetComponentInChildren<BaseUniqueAbility>(true);

            Listen(_shoot != null ? _shoot.onOverheatUpdate : null, OnShoot);
            Listen(_push != null ? _push.onCooldownUpdate : null, OnPush);
            Listen(_movement != null ? _movement.onDashCooldownUpdate : null, OnDash);
            Listen(_ability != null ? _ability.onCooldownUpdate : null, OnAbility);

            if (!HUDManager.HasInstance) return;
            var hud = HUDManager.Instance;
            // Ready until this peer sees the car use something (0 = ready for every cooldown).
            hud.UpdateUIElement(UIDynamicElementType.DASH_COOLDOWN, 0f, 1f);
            hud.UpdateUIElement(UIDynamicElementType.ULT_COOLDOWN, 0f, 1f);
            hud.UpdateUIElement(UIDynamicElementType.PUSH_COOLDOWN, 0f, 1f);
            hud.UpdateUIElement(UIDynamicElementType.SHOOT_COOLDOWN, 0f, 1f);
            SetSprites(CharacterCodeOf(root, _stats));
            _lastSpeed = null;
            Tick();
        }

        public void Unbind()
        {
            Unlisten(_shoot != null ? _shoot.onOverheatUpdate : null, OnShoot);
            Unlisten(_push != null ? _push.onCooldownUpdate : null, OnPush);
            Unlisten(_movement != null ? _movement.onDashCooldownUpdate : null, OnDash);
            Unlisten(_ability != null ? _ability.onCooldownUpdate : null, OnAbility);
            _stats = null;
            _physics = null;
            _shoot = null;
            _push = null;
            _movement = null;
            _ability = null;
        }

        /// <summary>Per frame: endurance and speed (both replicated for every car).</summary>
        public void Tick()
        {
            if (!_visible || !HUDManager.HasInstance) return;
            var hud = HUDManager.Instance;
            if (_stats != null && _stats.MaxEndurance > 0f)
                hud.UpdateUIElement(UIDynamicElementType.HEALTH_BAR, _stats.Endurance, _stats.MaxEndurance);
            if (_physics != null && _physics.Rb != null)
            {
                string speed = (_physics.Rb.linearVelocity.magnitude * SPEED_TO_KMH).ToString("F1");
                if (speed != _lastSpeed)
                {
                    hud.UpdateUIElement(UIDynamicElementType.SPEED_INDICATOR, speed);
                    _lastSpeed = speed;
                }
            }
        }

        /// <summary>Shows or hides the watched car's HUD (H / the overlay button).</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (!HUDManager.HasInstance) return;
            var hud = HUDManager.Instance;
            foreach (var type in Mirrored)
                if (hud.GetUIElement(type) != null) hud.UpdateUIElement(type, visible);
            if (!visible) return;
            _lastSpeed = null;
            Tick();
        }

        /// <summary>Back to the player's own HUD: undo what <see cref="SetVisible"/> switched on.</summary>
        public void Release()
        {
            Unbind();
            if (HUDManager.HasInstance && !_visible)
                foreach (var type in Mirrored)
                    if (HUDManager.Instance.GetUIElement(type) != null) HUDManager.Instance.UpdateUIElement(type, true);
            _visible = true;
        }

        /// <summary>The car's character code ("Pepe_0"): its player-strip icon, or the default skin.</summary>
        public static string CharacterCodeOf(Transform root, StatsController stats)
        {
            foreach (var icon in Object.FindObjectsByType<CharacterIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (icon == null || icon.Character == null) continue;
                Transform character = icon.Character.transform;
                bool same = character == root || character.IsChildOf(root) || root.IsChildOf(character);
                if (!same) continue;
                var image = icon.GetComponent<UnityEngine.UI.Image>();
                if (image != null && image.sprite != null) return image.sprite.name;
            }
            string name = stats != null && stats.CharacterData != null ? stats.CharacterData.CharacterName : null;
            return string.IsNullOrEmpty(name) ? null : name + "_0";
        }

        private static void SetSprites(string code)
        {
            if (string.IsNullOrEmpty(code) || !ResourcesManager.HasInstance || !HUDManager.HasInstance) return;
            string baseName = code.Split('_')[0];
            var hud = HUDManager.Instance;
            SetSprite(hud, UIDynamicElementType.SHOOT_ICON, baseName + "_SHOOT");
            SetSprite(hud, UIDynamicElementType.PUSH_ICON, baseName + "_PUSH");
            SetSprite(hud, UIDynamicElementType.ULT_ICON, baseName + "_UA");
            SetSprite(hud, UIDynamicElementType.SELECTED_PLAYER_ICON, code + "_HALFBODY");
        }

        private static void SetSprite(HUDManager hud, UIDynamicElementType type, string key)
        {
            Sprite sprite = null;
            try { sprite = ResourcesManager.Instance.GetResourcesSprite(key); }
            catch { /* a missing sprite keeps the previous one */ }
            if (sprite != null) hud.UpdateUIElement(type, sprite);
        }

        private void OnShoot(float current, float max) => Fill(UIDynamicElementType.SHOOT_COOLDOWN, current, max);
        private void OnPush(float current, float max) => Fill(UIDynamicElementType.PUSH_COOLDOWN, current, max);
        private void OnDash(float current, float max) => Fill(UIDynamicElementType.DASH_COOLDOWN, current, max);
        private void OnAbility(float current, float max) => Fill(UIDynamicElementType.ULT_COOLDOWN, current, max);

        private void Fill(UIDynamicElementType type, float current, float max)
        {
            if (_visible && HUDManager.HasInstance && max > 0f) HUDManager.Instance.UpdateUIElement(type, current, max);
        }

        private static void Listen(UnityEvent<float, float> evt, UnityAction<float, float> call)
        {
            if (evt != null) evt.AddListener(call);
        }

        private static void Unlisten(UnityEvent<float, float> evt, UnityAction<float, float> call)
        {
            if (evt != null) evt.RemoveListener(call);
        }
    }
}
