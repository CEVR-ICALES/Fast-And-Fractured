using Assets.SimpleLocalization.Scripts;
using System.Collections.Generic;
using Enums;
using FastAndFractured;
using FishNet;
using FishNet.Object;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Spectator camera controller activated when the local player dies. Cycles the existing
    /// CinemachineFreeLook camera between alive vehicles. Also supports free-camera flight
    /// (F key) and explicit "leave match" (ESC). Deactivates automatically when the match
    /// ends (EndGameRpc) or when only one player remains.
    ///
    /// Lives on the local client only; never spawned on remote/server unless host. Driven by
    /// NetworkedStatsController.OnPlayerDeathRpc and NetworkedLevelControllerManager.EndGameRpc.
    ///
    /// Controls (when active):
    ///   TAB / Click  - cycle to next alive player
    ///   F            - toggle free camera flight
    ///   ESC          - back to the room (the host: end the match for everyone)
    ///   (free-cam)   - WASD/Arrows: pan | E/Q: up/down | Shift: faster
    /// </summary>
    public class NetworkedSpectatorController : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        private static NetworkedSpectatorController _instance;
        public static NetworkedSpectatorController Instance => _instance;

        // ── State ────────────────────────────────────────────────────────────────
        private CinemachineFreeLook _camera;
        private CinemachineBrain _brain;
        private Camera _mainCamera;
        private Transform _originalFollow;
        private Transform _originalLookAt;
        private readonly List<Transform> _aliveTargets = new List<Transform>();
        private int _currentTargetIndex = -1;
        private bool _active;
        private bool _freeCam;
        private float _freeCamYaw;
        private float _freeCamPitch;

        // Track the local player's car (the one that just died) so we never spectate ourselves
        // and so we can re-enable HUD on it after deactivating. AIs and other players are valid
        // spectate targets.
        private Transform _localPlayerCar;
        // Disabled HUDs to restore on deactivation
        private readonly List<Behaviour> _hiddenHudComponents = new List<Behaviour>();

        public bool IsActive => _active;

        // ── UI ───────────────────────────────────────────────────────────────────
        private Canvas _uiCanvas;
        private TMP_Text _statusLabel;
        private TMP_Text _controlsLabel;

        // ── Tuning ───────────────────────────────────────────────────────────────
        private const float FREE_CAM_BASE_SPEED = 20f;
        private const float FREE_CAM_SHIFT_MULT = 3.5f;
        private const float FREE_CAM_MOUSE_SENS = 0.15f;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            // If we're being destroyed mid-spectator (e.g. scene change), make sure Cinemachine
            // gets control of the camera back so the next scene doesn't show a frozen black view.
            if (_brain != null && !_brain.enabled) _brain.enabled = true;
            if (_instance == this) _instance = null;
            if (_uiCanvas != null) Destroy(_uiCanvas.gameObject);
        }

        private void Update()
        {
            if (!_active) return;
            UpdateSkipButton();
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (_skipButton != null && _skipButton.activeSelf &&
                (kb != null && kb.enterKey.wasPressedThisFrame || pad != null && pad.startButton.wasPressedThisFrame))
            {
                SkipAiOnlyMatch();
                return;
            }
            if (kb == null) return;

            // Exit key — leave the match cleanly.
            if (kb.escapeKey.wasPressedThisFrame)
            {
                LeaveMatch();
                return;
            }

            // Toggle free-cam mode.
            if (kb.fKey.wasPressedThisFrame)
            {
                if (_freeCam) DisableFreeCam();
                else EnableFreeCam();
                UpdateUiText();
                return;
            }

            if (_freeCam)
            {
                UpdateFreeCam(kb);
                return;
            }

            // Cycle target.
            if (kb.tabKey.wasPressedThisFrame || Mouse.current?.leftButton.wasPressedThisFrame == true)
            {
                CycleToNextTarget();
                UpdateUiText();
            }
        }

        // ── Activation ───────────────────────────────────────────────────────────
        public void ActivateSpectator()
        {
            if (_active) return;

            if (!CacheCamera()) return;
            RebuildAliveTargets();

            if (_aliveTargets.Count == 0)
            {
                Debug.LogWarning("[NetworkedSpectatorController] No alive targets to spectate.");
                return;
            }

            _originalFollow = _camera.Follow;
            _originalLookAt = _camera.LookAt;
            _currentTargetIndex = 0;
            ApplyCurrentTarget();
            _active = true;

            HideLocalPlayerHud();
            EnsureUi();
            _uiCanvas.gameObject.SetActive(true);
            UpdateUiText();
        }

        public void DeactivateSpectator()
        {
            if (!_active) return;
            _active = false;

            if (_freeCam) DisableFreeCam();

            if (_camera != null)
            {
                _camera.Follow = _originalFollow;
                _camera.LookAt = _originalLookAt;
            }

            _aliveTargets.Clear();
            _currentTargetIndex = -1;

            RestoreLocalPlayerHud();
            if (_uiCanvas != null) _uiCanvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// HUD elements that are player-specific and should disappear while spectating. The rest
        /// (EVENT_TEXT, TIMER_TEXT, PLAYER_ICONS) are global and must stay visible so the user
        /// can still see kill feed, match timer and "X alive" indicator.
        /// </summary>
        private static readonly UIDynamicElementType[] PlayerHudElementsToHide = new[]
        {
            UIDynamicElementType.HEALTH_BAR,
            UIDynamicElementType.DASH_COOLDOWN,
            UIDynamicElementType.ULT_COOLDOWN,
            UIDynamicElementType.PUSH_COOLDOWN,
            UIDynamicElementType.SHOOT_COOLDOWN,
            UIDynamicElementType.DASH_ICON,
            UIDynamicElementType.ULT_ICON,
            UIDynamicElementType.PUSH_ICON,
            UIDynamicElementType.SHOOT_ICON,
            UIDynamicElementType.DASH_BINDING,
            UIDynamicElementType.ULT_BINDING,
            UIDynamicElementType.PUSH_BINDING,
            UIDynamicElementType.SHOOT_BINDING,
            UIDynamicElementType.SHOOTING_CROSSHAIR,
            UIDynamicElementType.SELECTED_PLAYER_ICON,
            UIDynamicElementType.SPEED_INDICATOR,
            UIDynamicElementType.BULLET_EFFECT,
            UIDynamicElementType.BAD_EFFECTS,
            UIDynamicElementType.NORMAL_EFFECTS,
            UIDynamicElementType.GOOD_EFFECTS,
            UIDynamicElementType.EFFECT_ICONS_CONTAINER,
        };

        // Track which elements WE turned off so we restore exactly those (and don't accidentally
        // re-enable something the game had legitimately set inactive).
        private readonly List<UIDynamicElementType> _disabledHudElements = new List<UIDynamicElementType>();

        /// <summary>
        /// Hide ONLY the local-player-specific HUD elements (endurance, ability cooldowns,
        /// crosshair, speed, etc.) while keeping global UI alive (kill feed, timer, player icons
        /// strip — the "X alive" indicator). Also disables the PlayerHUD MonoBehaviour so it
        /// stops re-pushing values that we just hid.
        /// </summary>
        private void HideLocalPlayerHud()
        {
            _hiddenHudComponents.Clear();
            _disabledHudElements.Clear();

            // Disable the per-element GameObjects via HUDManager's existing toggle API.
            if (HUDManager.HasInstance)
            {
                foreach (var t in PlayerHudElementsToHide)
                {
                    var el = HUDManager.Instance.GetUIElement(t);
                    if (el == null) continue;
                    if (!el.gameObject.activeSelf) continue; // already off — leave it
                    HUDManager.Instance.UpdateUIElement(t, false);
                    _disabledHudElements.Add(t);
                }
            }

            // Disable the PlayerHUD component(s) under the owned player root so the cooldown /
            // health timer callbacks stop firing UI updates (which would re-enable hidden bars).
            if (_localPlayerCar != null)
            {
                var rootNob = _localPlayerCar.GetComponentInParent<NetworkObject>(true);
                Transform searchRoot = rootNob != null ? rootNob.transform : _localPlayerCar;
                foreach (var hud in searchRoot.GetComponentsInChildren<PlayerHUD>(true))
                {
                    if (hud != null && hud.enabled)
                    {
                        hud.enabled = false;
                        _hiddenHudComponents.Add(hud);
                    }
                }
            }
        }

        private void RestoreLocalPlayerHud()
        {
            if (HUDManager.HasInstance)
            {
                foreach (var t in _disabledHudElements)
                    HUDManager.Instance.UpdateUIElement(t, true);
            }
            _disabledHudElements.Clear();

            foreach (var c in _hiddenHudComponents)
                if (c != null) c.enabled = true;
            _hiddenHudComponents.Clear();
        }

        // ── Death propagation ────────────────────────────────────────────────────
        public void OnAnyCharacterDied(NetworkObject victim)
        {
            if (!_active || victim == null || _camera == null) return;
            if (_freeCam) return; // free-cam ignores target changes

            Transform victimTransform = victim.transform;
            if (_currentTargetIndex >= 0 && _currentTargetIndex < _aliveTargets.Count)
            {
                var current = _aliveTargets[_currentTargetIndex];
                if (current == victimTransform || current == null)
                {
                    RebuildAliveTargets();
                    if (_aliveTargets.Count == 0)
                    {
                        DeactivateSpectator();
                        return;
                    }
                    _currentTargetIndex %= _aliveTargets.Count;
                    ApplyCurrentTarget();
                    UpdateUiText();
                    return;
                }
            }
            RebuildAliveTargets();
            UpdateUiText();
        }

        // ── Target cycling ───────────────────────────────────────────────────────
        private void CycleToNextTarget()
        {
            RebuildAliveTargets();
            if (_aliveTargets.Count == 0)
            {
                DeactivateSpectator();
                return;
            }
            _currentTargetIndex = (_currentTargetIndex + 1) % _aliveTargets.Count;
            ApplyCurrentTarget();
        }

        private void ApplyCurrentTarget()
        {
            if (_camera == null) return;
            if (_currentTargetIndex < 0 || _currentTargetIndex >= _aliveTargets.Count) return;
            var target = _aliveTargets[_currentTargetIndex];
            if (target == null) return;
            _camera.Follow = target;
            _camera.LookAt = target;
        }

        private bool CacheCamera()
        {
            // Always (re)find Camera.main and its Brain — Camera.main can change between scenes.
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[NetworkedSpectatorController] Camera.main is null.");
                return false;
            }
            if (_brain == null) _brain = _mainCamera.GetComponent<CinemachineBrain>();

            if (_camera != null) return true;

            // We want the LOCAL PLAYER's CinemachineFreeLook (the one whose Brain on Camera.main
            // is actually being driven by us). Each NetworkedPlayer instance has its own FreeLook
            // child; if we just pick the first one found, we may end up driving a remote player's
            // camera and our visible camera (which is following our dead car's vchcam) never moves.
            var allCams = FindObjectsByType<CinemachineFreeLook>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            CinemachineFreeLook owned = null;
            CinemachineFreeLook anyAlive = null;
            foreach (var cam in allCams)
            {
                if (cam == null) continue;
                var nob = cam.GetComponentInParent<NetworkObject>(true);
                if (nob != null && nob.IsOwner)
                {
                    owned = cam;
                    break;
                }
                if (anyAlive == null) anyAlive = cam;
            }

            _camera = owned ?? anyAlive;
            if (_camera == null)
            {
                Debug.LogError("[NetworkedSpectatorController] No CinemachineFreeLook found in scene.");
                return false;
            }

            // Cache our owned car so we can skip it (and only it) when listing targets, and so we
            // know which HUD to hide / re-show on deactivation.
            if (owned != null)
            {
                var nob = owned.GetComponentInParent<NetworkObject>(true);
                if (nob != null) _localPlayerCar = nob.transform;
            }

            // Defensive: make sure the camera is enabled and has high priority. When our car was
            // destroyed, the cinemachine brain may have switched to whatever vchcam is left; we
            // need this one to win the priority race so the visible camera is the one we drive.
            if (!_camera.gameObject.activeSelf) _camera.gameObject.SetActive(true);
            if (!_camera.enabled) _camera.enabled = true;
            _camera.Priority = 10000;

            Debug.Log($"[NetworkedSpectatorController] Cached camera '{_camera.gameObject.name}' " +
                      $"(owned={owned != null}). Priority forced to {_camera.Priority}. " +
                      $"Brain={(_brain != null ? "found" : "MISSING")}");
            return true;
        }

        private void RebuildAliveTargets()
        {
            _aliveTargets.Clear();
            if (LevelController.Instance == null || LevelController.Instance.InGameCharacters == null) return;

            foreach (var character in LevelController.Instance.InGameCharacters)
            {
                if (character == null) continue;
                // InGameCharacters entries are not uniform: AI and host cars register the car
                // GameObject itself, but remote players can be registered as their NetworkedPlayer
                // ROOT (whose StatsController lives on the nested car). GetComponent on the root
                // returned null and silently dropped every remote player from the spectate list —
                // that's why spectators could only cycle through AIs. Search the children too and
                // follow the car (the stats' transform), not the list entry.
                var stats = character.GetComponent<StatsController>();
                if (stats == null) stats = character.GetComponentInChildren<StatsController>(true);
                if (stats == null) continue;
                if (stats.Endurance <= 0f) continue;

                Transform target = stats.transform;

                // Skip ONLY our own dead car. Server-owned AIs and other player vehicles are
                // valid spectate targets — the old IsOwner check filtered out AIs on the host
                // (server owns them) which is exactly the opposite of what we want.
                if (_localPlayerCar != null)
                {
                    if (target == _localPlayerCar) continue;
                    if (target.IsChildOf(_localPlayerCar)) continue;
                    if (_localPlayerCar.IsChildOf(target)) continue;
                }

                _aliveTargets.Add(target);
            }
        }

        // ── Free camera ──────────────────────────────────────────────────────────
        // Strategy: disable the CinemachineBrain so we get direct control of Camera.main's
        // transform, and drive position+rotation each frame. Movement vectors come from the
        // camera's own forward/right, so input feels exactly relative to what you see (no
        // cinemachine orbit offset getting in the way).
        private void EnableFreeCam()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            // Snapshot current pose
            Transform camT = _mainCamera.transform;
            var e = camT.rotation.eulerAngles;
            _freeCamYaw = e.y;
            _freeCamPitch = NormalizePitch(e.x);

            // Take over: disable the Brain. Cinemachine stops driving Camera.main; we drive it.
            if (_brain != null) _brain.enabled = false;

            _freeCam = true;
        }

        private void DisableFreeCam()
        {
            _freeCam = false;
            // Hand control back to Cinemachine.
            if (_brain != null) _brain.enabled = true;
            // Snap back to whatever target we were spectating last.
            ApplyCurrentTarget();
        }

        private void UpdateFreeCam(Keyboard kb)
        {
            if (_mainCamera == null) return;

            // Mouse look (only when right mouse held — keeps cursor free for other UI work)
            var mouse = Mouse.current;
            bool look = mouse != null && mouse.rightButton.isPressed;
            if (look)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _freeCamYaw += delta.x * FREE_CAM_MOUSE_SENS;
                _freeCamPitch = Mathf.Clamp(_freeCamPitch - delta.y * FREE_CAM_MOUSE_SENS, -85f, 85f);
            }
            Quaternion rot = Quaternion.Euler(_freeCamPitch, _freeCamYaw, 0f);
            _mainCamera.transform.rotation = rot;

            // WASD/QE movement relative to camera forward/right (what you see is what you move along)
            float speed = FREE_CAM_BASE_SPEED * (kb.shiftKey.isPressed ? FREE_CAM_SHIFT_MULT : 1f);
            Vector3 fwd = _mainCamera.transform.forward;
            Vector3 right = _mainCamera.transform.right;
            Vector3 move = Vector3.zero;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += fwd;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= fwd;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move -= right;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move += right;
            if (kb.eKey.isPressed || kb.spaceKey.isPressed) move += Vector3.up;
            if (kb.qKey.isPressed || kb.leftCtrlKey.isPressed) move -= Vector3.up;

            if (move.sqrMagnitude > 0.001f)
                _mainCamera.transform.position += move.normalized * (speed * Time.deltaTime);
        }

        private static float NormalizePitch(float eulerX)
        {
            // Unity euler X comes back as 0..360; convert to -180..180
            if (eulerX > 180f) eulerX -= 360f;
            return Mathf.Clamp(eulerX, -85f, 85f);
        }

        // ── Exit ─────────────────────────────────────────────────────────────────
        private void LeaveMatch()
        {
            Debug.Log("[NetworkedSpectatorController] User pressed ESC — back to the room.");
            DeactivateSpectator();

            // The room outlives the match: a spectating player goes back to it and waits for the
            // next match. The host cannot leave a match running on it, so for the host this
            // ends the match for everyone, who all go back to the room.
            MultiplayerRoomReturn.LeaveMatchToRoom();
        }

        private static string LeaveHint => InstanceFinder.IsServerStarted ? LocalizationManager.Localize("MP.Spectator.EndMatch") : LocalizationManager.Localize("MP.Spectator.BackToRoom");

        // ── Skip (host, only AIs left) ───────────────────────────────────────────
        private GameObject _skipButton;
        private NetworkedLevelControllerManager _manager;

        private void UpdateSkipButton()
        {
            if (_skipButton == null) return;
            if (_manager == null) _manager = FindFirstObjectByType<NetworkedLevelControllerManager>();
            bool show = InstanceFinder.IsServerStarted && _manager != null && _manager.OnlyAiAlive;
            if (_skipButton.activeSelf != show) _skipButton.SetActive(show);
        }

        private void SkipAiOnlyMatch()
        {
            if (_manager == null || !_manager.OnlyAiAlive) return;
            _skipButton.SetActive(false);
            _manager.ServerSkipAiOnlyMatch();
        }

        private void BuildSkipButton(Transform parent)
        {
            _skipButton = new GameObject("SkipButton");
            _skipButton.transform.SetParent(parent, false);
            var rt = _skipButton.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-40f, 90f);
            rt.sizeDelta = new Vector2(420f, 80f);
            var bg = _skipButton.AddComponent<Image>();
            bg.color = new Color(0.85f, 0.3f, 0.15f, 0.95f);
            _skipButton.AddComponent<Button>().onClick.AddListener(SkipAiOnlyMatch);
            var label = MakeLabel(_skipButton.transform, 30, TextAlignmentOptions.Center, Color.white);
            label.text = LocalizationManager.Localize("MP.Spectator.SkipAi");
            _skipButton.SetActive(false);
        }

        // ── UI (built in code, zero scene wiring) ────────────────────────────────
        private void EnsureUi()
        {
            if (_uiCanvas != null) return;

            var rootGO = new GameObject("[SpectatorOverlay]");
            // Lifetime is tied to this component; do NOT DontDestroyOnLoad so it dies with the scene.

            _uiCanvas = rootGO.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = 32600;
            var scaler = rootGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            rootGO.AddComponent<GraphicRaycaster>();

            // Top status bar
            var topBar = MakeBar(rootGO.transform, anchorY: 1f, pivotY: 1f, anchoredY: -20f, height: 70f);
            _statusLabel = MakeLabel(topBar.transform, 38, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.5f, 1f));

            // Bottom controls bar
            var bottomBar = MakeBar(rootGO.transform, anchorY: 0f, pivotY: 0f, anchoredY: 20f, height: 50f);
            _controlsLabel = MakeLabel(bottomBar.transform, 26, TextAlignmentOptions.Center, new Color(0.85f, 0.85f, 0.85f, 1f));

            BuildSkipButton(rootGO.transform);
        }

        private static GameObject MakeBar(Transform parent, float anchorY, float pivotY, float anchoredY, float height)
        {
            var barGO = new GameObject("Bar");
            barGO.transform.SetParent(parent, false);
            var rt = barGO.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, anchorY);
            rt.anchorMax = new Vector2(1f, anchorY);
            rt.pivot = new Vector2(0.5f, pivotY);
            rt.anchoredPosition = new Vector2(0f, anchoredY);
            rt.sizeDelta = new Vector2(0f, height);
            var bg = barGO.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            return barGO;
        }

        private static TMP_Text MakeLabel(Transform parent, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(30f, 5f);
            rt.offsetMax = new Vector2(-30f, -5f);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.fontSize = fontSize;
            txt.alignment = align;
            txt.color = color;
            txt.text = string.Empty;
            return txt;
        }

        private void UpdateUiText()
        {
            if (_statusLabel == null || _controlsLabel == null) return;

            if (_freeCam)
            {
                _statusLabel.text = LocalizationManager.Localize("MP.Spectator.FreeCamera");
                _controlsLabel.text = LocalizationManager.Localize("MP.Spectator.FreeControls") + "   " + LeaveHint;
            }
            else
            {
                string targetName = "—";
                if (_currentTargetIndex >= 0 && _currentTargetIndex < _aliveTargets.Count && _aliveTargets[_currentTargetIndex] != null)
                {
                    var nob = _aliveTargets[_currentTargetIndex].GetComponentInParent<NetworkObject>(true);
                    string ownerLabel;
                    if (nob == null || nob.Owner == null || !nob.Owner.IsValid || nob.Owner.ClientId < 0)
                        ownerLabel = " (AI)";
                    else
                        ownerLabel = $" #{nob.Owner.ClientId}";
                    targetName = $"{_aliveTargets[_currentTargetIndex].name}{ownerLabel}";
                }
                _statusLabel.text = LocalizationManager.Localize("MP.Spectator.Spectating", targetName, _aliveTargets.Count);
                _controlsLabel.text = LocalizationManager.Localize("MP.Spectator.Controls") + "   " + LeaveHint;
            }
        }
    }
}
