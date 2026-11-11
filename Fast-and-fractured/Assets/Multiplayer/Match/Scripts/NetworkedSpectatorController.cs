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
    /// Local spectator camera after the local player dies: cycles the FreeLook between alive cars, free flight,
    /// HUD toggle and leaving to the room. Controls: TAB/click cycle, F free camera, H HUD, ESC leave.
    /// </summary>
    public class NetworkedSpectatorController : MonoBehaviour
    {
        private static NetworkedSpectatorController _instance;
        public static NetworkedSpectatorController Instance => _instance;

        private CinemachineFreeLook _camera;
        private CinemachineBrain _brain;
        private Camera _mainCamera;
        private Transform _originalFollow;
        private Transform _originalLookAt;
        private CameraTargetSmoother _smoother;
        private Transform _originalSmootherTarget;
        private readonly List<Transform> _aliveTargets = new List<Transform>();
        private int _currentTargetIndex = -1;
        private bool _active;
        private bool _freeCam;
        private float _freeCamYaw;
        private float _freeCamPitch;

        // Our own dead car: never spectated, and its HUD is restored on deactivation.
        private Transform _localPlayerCar;
        private readonly List<Behaviour> _hiddenHudComponents = new List<Behaviour>();

        public bool IsActive => _active;

        private readonly SpectatedHud _hud = new SpectatedHud();

        private Canvas _uiCanvas;
        private TMP_Text _statusLabel;
        private TMP_Text _controlsLabel;

        private const float FREE_CAM_BASE_SPEED = 20f;
        private const float FREE_CAM_SHIFT_MULT = 3.5f;
        private const float FREE_CAM_MOUSE_SENS = 0.15f;
        private const float MAX_FREE_CAM_PITCH = 85f;
        private const float MIN_FREE_CAM_MOVE_SQR = 0.001f;
        private const int CAMERA_PRIORITY = 10000;
        private const int CANVAS_SORTING_ORDER = 32600;
        private const float STATUS_FONT_SIZE = 38f;
        private const float SKIP_FONT_SIZE = 30f;
        private const float SMALL_FONT_SIZE = 26f;
        private static readonly Vector2 REFERENCE_RESOLUTION = new Vector2(1920, 1080);
        private static readonly Vector2 LABEL_PADDING = new Vector2(30f, 5f);
        private static readonly Color BAR_COLOR = new Color(0f, 0f, 0f, 0.65f);
        private static readonly Color SKIP_BUTTON_COLOR = new Color(0.85f, 0.3f, 0.15f, 0.95f);
        private static readonly Color STATUS_TEXT_COLOR = new Color(1f, 0.9f, 0.5f, 1f);
        private static readonly Color CONTROLS_TEXT_COLOR = new Color(0.85f, 0.85f, 0.85f, 1f);

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
            // Destroyed mid-spectate (scene change): give the camera back to Cinemachine.
            if (_brain != null && !_brain.enabled) _brain.enabled = true;
            if (_instance == this) _instance = null;
            if (_uiCanvas != null) Destroy(_uiCanvas.gameObject);
        }

        private void Update()
        {
            if (!_active) return;
            UpdateSkipButton();
            if (!_freeCam) _hud.Tick();
            var kb = Keyboard.current;
            var pad = Gamepad.current;
            if (_skipButton != null && _skipButton.activeSelf &&
                (kb != null && kb.enterKey.wasPressedThisFrame || pad != null && pad.startButton.wasPressedThisFrame))
            {
                SkipAiOnlyMatch();
                return;
            }
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame)
            {
                LeaveMatch();
                return;
            }

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

            if (kb.hKey.wasPressedThisFrame)
            {
                ToggleHud();
                return;
            }

            // Cycle target (a click on the overlay's buttons is not a cycle).
            bool click = Mouse.current?.leftButton.wasPressedThisFrame == true &&
                         (UnityEngine.EventSystems.EventSystem.current == null ||
                          !UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject());
            if (kb.tabKey.wasPressedThisFrame || click)
            {
                CycleToNextTarget();
                UpdateUiText();
            }
        }

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
            _smoother = _camera.GetComponent<CameraTargetSmoother>();
            _originalSmootherTarget = _smoother != null ? _smoother.RawTarget : null;
            _currentTargetIndex = 0;
            ApplyCurrentTarget();
            _active = true;

            HideLocalPlayerHud();
            _hud.SetVisible(true);
            BindHudToCurrentTarget();
            EnsureUi();
            _uiCanvas.gameObject.SetActive(true);
            UpdateHudButton();
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
            if (_smoother != null && _originalSmootherTarget != null)
                _smoother.Retarget(_originalSmootherTarget);

            _aliveTargets.Clear();
            _currentTargetIndex = -1;

            _hud.Release();
            _hudTarget = null;
            RestoreLocalPlayerHud();
            if (_uiCanvas != null) _uiCanvas.gameObject.SetActive(false);
        }

        /// <summary>Player-specific HUD elements; while spectating the mirrored ones show the watched car, the rest hide.</summary>
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

        // Only what we turned off is restored.
        private readonly List<UIDynamicElementType> _disabledHudElements = new List<UIDynamicElementType>();

        /// <summary>Hides the local player's HUD elements (global ones stay) and stops PlayerHUD re-showing them.</summary>
        private void HideLocalPlayerHud()
        {
            _hiddenHudComponents.Clear();
            _disabledHudElements.Clear();

            if (HUDManager.HasInstance)
            {
                foreach (var t in PlayerHudElementsToHide)
                {
                    if (System.Array.IndexOf(SpectatedHud.Mirrored, t) >= 0) continue;   // shows the watched car
                    var el = HUDManager.Instance.GetUIElement(t);
                    if (el == null) continue;
                    if (!el.gameObject.activeSelf) continue; // already off — leave it
                    HUDManager.Instance.UpdateUIElement(t, false);
                    _disabledHudElements.Add(t);
                }
            }

            // Stop PlayerHUD's timer callbacks re-enabling the hidden bars.
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

            // Follow the interpolated Visuals through the smoother proxy: the car root only moves on ticks.
            Transform point = SmoothedPointOf(target);
            if (_active) BindHudToCurrentTarget();
            if (_smoother != null && _smoother.isActiveAndEnabled)
            {
                _smoother.Retarget(point);
                return;
            }
            _camera.Follow = point;
            _camera.LookAt = point;
        }

        private Transform _hudTarget;

        private void BindHudToCurrentTarget()
        {
            if (_currentTargetIndex < 0 || _currentTargetIndex >= _aliveTargets.Count) return;
            var target = _aliveTargets[_currentTargetIndex];
            if (target == null || target == _hudTarget) return;
            _hudTarget = target;
            _hud.Bind(target);
        }

        private void ToggleHud()
        {
            _hud.SetVisible(!_hud.Visible);
            UpdateHudButton();
        }

        private const string VISUALS_CHILD_NAME = "Visuals";

        /// <summary>The interpolated model of a car, or the car itself when it has none.</summary>
        public static Transform SmoothedPointOf(Transform car)
        {
            if (car == null) return null;
            Transform visuals = car.Find(VISUALS_CHILD_NAME);
            return visuals != null ? visuals : car;
        }

        private bool CacheCamera()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null)
            {
                Debug.LogError("[NetworkedSpectatorController] Camera.main is null.");
                return false;
            }
            if (_brain == null) _brain = _mainCamera.GetComponent<CinemachineBrain>();

            if (_camera != null) return true;

            // The local player's FreeLook: every NetworkedPlayer has one, and driving a remote one moves nothing visible.
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

            if (owned != null)
            {
                var nob = owned.GetComponentInParent<NetworkObject>(true);
                if (nob != null) _localPlayerCar = nob.transform;
            }

            // Win the priority race against whatever camera is left after our car died.
            if (!_camera.gameObject.activeSelf) _camera.gameObject.SetActive(true);
            if (!_camera.enabled) _camera.enabled = true;
            _camera.Priority = CAMERA_PRIORITY;

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
                // Remote players can be registered by their root; the stats live on the nested car, which is what to follow.
                var stats = character.GetComponent<StatsController>();
                if (stats == null) stats = character.GetComponentInChildren<StatsController>(true);
                if (stats == null) continue;
                if (stats.Endurance <= 0f) continue;

                Transform target = stats.transform;

                // Skip only our own car; server-owned AIs are valid targets.
                if (_localPlayerCar != null)
                {
                    if (target == _localPlayerCar) continue;
                    if (target.IsChildOf(_localPlayerCar)) continue;
                    if (_localPlayerCar.IsChildOf(target)) continue;
                }

                _aliveTargets.Add(target);
            }
        }

        // Free camera: the Brain is disabled and Camera.main driven directly, relative to its own forward/right.
        private void EnableFreeCam()
        {
            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera == null) return;

            Transform camT = _mainCamera.transform;
            var e = camT.rotation.eulerAngles;
            _freeCamYaw = e.y;
            _freeCamPitch = NormalizePitch(e.x);

            if (_brain != null) _brain.enabled = false;

            _freeCam = true;
        }

        private void DisableFreeCam()
        {
            _freeCam = false;
            if (_brain != null) _brain.enabled = true;
            ApplyCurrentTarget();
        }

        private void UpdateFreeCam(Keyboard kb)
        {
            if (_mainCamera == null) return;

            // Mouse look only with the right button held, so the cursor stays free for the UI.
            var mouse = Mouse.current;
            bool look = mouse != null && mouse.rightButton.isPressed;
            if (look)
            {
                Vector2 delta = mouse.delta.ReadValue();
                _freeCamYaw += delta.x * FREE_CAM_MOUSE_SENS;
                _freeCamPitch = Mathf.Clamp(_freeCamPitch - delta.y * FREE_CAM_MOUSE_SENS, -MAX_FREE_CAM_PITCH, MAX_FREE_CAM_PITCH);
            }
            Quaternion rot = Quaternion.Euler(_freeCamPitch, _freeCamYaw, 0f);
            _mainCamera.transform.rotation = rot;

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

            if (move.sqrMagnitude > MIN_FREE_CAM_MOVE_SQR)
                _mainCamera.transform.position += move.normalized * (speed * Time.deltaTime);
        }

        private static float NormalizePitch(float eulerX)
        {
            // Euler X comes back as 0..360.
            if (eulerX > 180f) eulerX -= 360f;
            return Mathf.Clamp(eulerX, -MAX_FREE_CAM_PITCH, MAX_FREE_CAM_PITCH);
        }

        private void LeaveMatch()
        {
            Debug.Log("[NetworkedSpectatorController] User pressed ESC — back to the room.");
            DeactivateSpectator();

            // The room outlives the match; the host cannot leave one running, so for the host this ends it for everyone.
            MultiplayerRoomReturn.LeaveMatchToRoom();
        }

        private static string LeaveHint => InstanceFinder.IsServerStarted ? LocalizationManager.Localize("MP.Spectator.EndMatch") : LocalizationManager.Localize("MP.Spectator.BackToRoom");

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
            rt.anchoredPosition = new Vector2(-40f, 190f);   // above the ability icons
            rt.sizeDelta = new Vector2(420f, 80f);
            var bg = _skipButton.AddComponent<Image>();
            bg.color = SKIP_BUTTON_COLOR;
            _skipButton.AddComponent<Button>().onClick.AddListener(SkipAiOnlyMatch);
            var label = MakeLabel(_skipButton.transform, SKIP_FONT_SIZE, TextAlignmentOptions.Center, Color.white);
            label.text = LocalizationManager.Localize("MP.Spectator.SkipAi");
            _skipButton.SetActive(false);
        }

        private void EnsureUi()
        {
            if (_uiCanvas != null) return;

            var rootGO = new GameObject("[SpectatorOverlay]");
            // Not DontDestroyOnLoad: dies with the scene.

            _uiCanvas = rootGO.AddComponent<Canvas>();
            _uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _uiCanvas.sortingOrder = CANVAS_SORTING_ORDER;
            var scaler = rootGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = REFERENCE_RESOLUTION;
            rootGO.AddComponent<GraphicRaycaster>();

            var topBar = MakeBar(rootGO.transform, anchorY: 1f, pivotY: 1f, anchoredY: -20f, height: 70f);
            _statusLabel = MakeLabel(topBar.transform, STATUS_FONT_SIZE, TextAlignmentOptions.Center, STATUS_TEXT_COLOR);

            var bottomBar = MakeBar(rootGO.transform, anchorY: 0f, pivotY: 0f, anchoredY: 20f, height: 50f);
            _controlsLabel = MakeLabel(bottomBar.transform, SMALL_FONT_SIZE, TextAlignmentOptions.Center, CONTROLS_TEXT_COLOR);

            BuildSkipButton(rootGO.transform);
            BuildHudButton(rootGO.transform);
        }

        private TMP_Text _hudButtonLabel;

        private void BuildHudButton(Transform parent)
        {
            var go = new GameObject("HudButton");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            // Top left, under the status bar: the minimap takes the top right corner.
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -110f);
            rt.sizeDelta = new Vector2(300f, 60f);
            var bg = go.AddComponent<Image>();
            bg.color = BAR_COLOR;
            go.AddComponent<Button>().onClick.AddListener(ToggleHud);
            _hudButtonLabel = MakeLabel(go.transform, SMALL_FONT_SIZE, TextAlignmentOptions.Center, Color.white);
            UpdateHudButton();
        }

        private void UpdateHudButton()
        {
            if (_hudButtonLabel == null) return;
            _hudButtonLabel.text = LocalizationManager.Localize(_hud.Visible ? "MP.Spectator.HideHud" : "MP.Spectator.ShowHud");
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
            bg.color = BAR_COLOR;
            return barGO;
        }

        private static TMP_Text MakeLabel(Transform parent, float fontSize, TextAlignmentOptions align, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = LABEL_PADDING;
            rt.offsetMax = -LABEL_PADDING;
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
                    targetName = MatchNames.Of(_aliveTargets[_currentTargetIndex].gameObject);
                }
                _statusLabel.text = LocalizationManager.Localize("MP.Spectator.Spectating", targetName, _aliveTargets.Count);
                _controlsLabel.text = LocalizationManager.Localize("MP.Spectator.Controls") + "   " + LeaveHint;
            }
        }
    }
}
