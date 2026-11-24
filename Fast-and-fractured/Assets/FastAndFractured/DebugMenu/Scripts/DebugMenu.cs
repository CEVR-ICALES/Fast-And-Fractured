using System;
using System.Collections;
using System.IO;
using Enums;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured
{
    [DefaultExecutionOrder(EXECUTION_ORDER)]
    public sealed class DebugMenu : MonoBehaviour
    {
        public const Key TOGGLE_KEY = Key.F1;
        public const Key FREE_CAMERA_KEY = Key.F2;
        public const Key HUD_KEY = Key.F4;
        public const Key PREVIOUS_VOLUME_KEY = Key.F6;
        public const Key NEXT_VOLUME_KEY = Key.F7;
        public const Key SCREENSHOT_KEY = Key.F9;
        public const string SCREENSHOT_FOLDER = "Screenshots";
        public static readonly float[] TIME_SCALES = { 0f, 0.1f, 0.25f, 0.5f, 1f, 2f, 4f };
        public static readonly float[] STORM_SPEEDS = { -8f, -2f, 0f, 1f, 4f, 16f };
        public const int MAX_CUSTOM_RIVALS = 15;
        public const int MAX_CUSTOM_SPAWN = 15;

        private const int EXECUTION_ORDER = 32000;
        private const int WINDOW_ID = 0x0FAF;
        private const string TITLE = "Debug menu";
        private const string HOTKEYS = "F1 menu (or both sticks pressed)   F2 free camera   F4 HUD   F6 / F7 volume   F9 screenshot";
        private const string FREE_CAMERA_HELP =
            "WASD move, Q / E down / up, Shift fast, Ctrl slow, wheel changes the speed. With this menu open, hold the right mouse button to look around.\n" +
            "Gamepad: sticks move and look, triggers down / up, bumpers slow / fast.";
        private const string NO_MATCH = "Player and match options need a match.";
        private const string NETWORK_SESSION = "Time, player and match options are off in a network session.";
        private const float REFERENCE_HEIGHT = 1080f;
        private const float WIDTH = 400f;
        private const float MAX_HEIGHT = 900f;
        private const float MARGIN = 10f;
        private const float TITLE_HEIGHT = 20f;
        private const float LABEL_WIDTH = 130f;
        private const float SECTION_SPACE = 6f;
        private const float TOAST_WIDTH = 700f;
        private const float TOAST_TOP = 40f;
        private const float TOAST_HEIGHT = 26f;
        private const float TOAST_SECONDS = 2.5f;
        private const float REFRESH_SECONDS = 0.5f;
        private const float MOUSE_DEGREES_PER_PIXEL = 0.12f;
        private const float STICK_DEGREES_PER_SECOND = 120f;
        private const float MILLISECONDS = 1000f;
        private const int VOLUME_COLUMNS = 2;
        private const int QUALITY_COLUMNS = 3;
        private const int DEBUG_ENDURANCE = 100;
        private const int MAP_COLUMNS = 2;
        private const int DEFAULT_CUSTOM_RIVALS = 7;
        private static readonly string[] STORM_LABELS = { "<< x8", "< x2", "Stop", "x1", "x4 >", "x16 >>" };
        private static readonly Color BACKGROUND = new Color(0.07f, 0.07f, 0.09f, 0.93f);
        private static readonly Color HIGHLIGHT = new Color(1f, 0.85f, 0.2f);

        private readonly DebugFreeCamera _freeCamera = new DebugFreeCamera();
        private readonly DebugHud _hud = new DebugHud();
        private bool _open;
        private Rect _window = new Rect(MARGIN, MARGIN, WIDTH, MAX_HEIGHT);
        private Vector2 _scroll;
        private bool _showVolume = true;
        private bool _showOverrides;
        private bool _showCamera = true;
        private bool _showView = true;
        private bool _showTime = true;
        private bool _showPlayer = true;
        private bool _showMatch = true;
        private bool _showAi = true;
        private bool _showCustomMatch;
        private bool _aiPaused;
        private bool _aiDisarmed;
        private bool _aiShielded;
        private bool _noOverheat;
        private bool _noCooldown;
        private float _nextCheatApply;
        private SandstormController _storm;
        private string[] _mapNames = Array.Empty<string>();
        private int[] _mapBuildIndices = Array.Empty<int>();
        private int _customMap;
        private float _customRivals = DEFAULT_CUSTOM_RIVALS;
        private float _customSpawn = DebugCustomMatch.RANDOM_SPAWN;

        private global::VolumeManager _volumes;
        private LevelController _level;
        private StatsController _stats;
        private bool _networkSession;
        private string[] _volumeNames = Array.Empty<string>();
        private Volume[] _moodVolumes = Array.Empty<Volume>();
        private string[] _qualityNames = Array.Empty<string>();
        private string[] _timeLabels;
        private string _info = string.Empty;
        private float _nextRefresh;
        private int _frames;
        private float _elapsed;

        private bool _ownsCursor;
        private CursorLockMode _gameCursorLock;
        private bool _gameCursorVisible;
        private CursorLockMode _appliedCursorLock;
        private bool _appliedCursorVisible;
        private PlayerInputController _blockedInput;
        private InputBlockTypes _blockedType;

        private string _toast;
        private float _toastUntil;
        private bool _capturing;
        private bool _stepping;
        private string _lastScreenshot = string.Empty;
        private GUIStyle _headerStyle;
        private GUIStyle _wrapStyle;
        private GUIStyle _boldStyle;
        private GUIStyle _choiceStyle;
        private Texture2D _background;

        public static DebugMenu Current { get; private set; }
        public bool IsOpen => _open;
        public DebugFreeCamera FreeCamera => _freeCamera;
        public bool IsHudHidden => _hud.Hidden;
        public string LastScreenshot => _lastScreenshot;
        public static string ScreenshotFolder => Path.GetFullPath(Path.Combine(Application.persistentDataPath, SCREENSHOT_FOLDER));

        #region Start

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!DebugMenuSwitch.Available) return;
            var go = new GameObject("[DebugMenu]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<DebugMenu>();
        }

        private void Awake()
        {
            Current = this;
            _timeLabels = Array.ConvertAll(TIME_SCALES, scale => "x" + scale.ToString("0.##"));
            ListMaps();
        }

        private void ListMaps()
        {
            var names = new System.Collections.Generic.List<string>();
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (!DebugCustomMatch.IsPlayableMap(path)) continue;
                names.Add(Path.GetFileNameWithoutExtension(path));
                indices.Add(i);
            }
            _mapNames = names.ToArray();
            _mapBuildIndices = indices.ToArray();
        }

        private void OnEnable() => SceneManager.activeSceneChanged += OnSceneChanged;

        private void OnDisable()
        {
            SceneManager.activeSceneChanged -= OnSceneChanged;
            _open = false;
            _freeCamera.Exit();
            _hud.Show();
            ApplyInputBlock();
            ApplyCursor();
        }

        private void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_background != null) Destroy(_background);
        }

        private void OnSceneChanged(Scene previous, Scene next)
        {
            _hud.Show();
            _nextRefresh = 0f;
            _aiPaused = _aiDisarmed = _aiShielded = _noOverheat = _noCooldown = false;
        }

        #endregion

        #region Update

        private void Update()
        {
            if (_stepping)
            {
                _stepping = false;
                Time.timeScale = 0f;
            }
            ReadHotkeys();
            ApplyInputBlock();
            ApplyCursor();
            if ((_aiDisarmed || _aiShielded || _noOverheat || _noCooldown) && Time.unscaledTime >= _nextCheatApply) ApplyCheats();
            if (!_open) return;
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (Time.unscaledTime >= _nextRefresh) Refresh();
        }

        private void LateUpdate()
        {
            if (!_freeCamera.Active) return;
            Mouse mouse = Mouse.current;
            if (mouse != null && !_open)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (!Mathf.Approximately(wheel, 0f)) _freeCamera.ChangeSpeed(Mathf.Sign(wheel));
            }
            _freeCamera.Tick(ReadMove(), ReadLook(), ReadSpeedFactor(), Time.unscaledDeltaTime);
            if (!_freeCamera.Active) Say("Free camera off: its camera is gone");
        }

        private void Refresh()
        {
            _nextRefresh = Time.unscaledTime + REFRESH_SECONDS;
            _volumes = global::VolumeManager.ExistingInstance;
            _level = LevelController.ExistingInstance;
            _stats = DebugCheats.LocalStats(_level);
            _networkSession = DebugCheats.InNetworkSession;
            _storm = _level != null ? FindFirstObjectByType<SandstormController>() : null;

            int volumeCount = DebugVolumes.Count(_volumes);
            if (_volumeNames.Length != volumeCount) _volumeNames = new string[volumeCount];
            for (int i = 0; i < volumeCount; i++) _volumeNames[i] = DebugVolumes.NameOf(_volumes, i);
            _moodVolumes = DebugVolumes.VolumesOf(_volumes);
            _qualityNames = QualitySettings.names;

            float fps = _elapsed > 0f ? _frames / _elapsed : 0f;
            _frames = 0;
            _elapsed = 0f;
            _info = InfoLine(fps, _qualityNames, QualitySettings.GetQualityLevel(), Screen.width, Screen.height,
                SceneManager.GetActiveScene().name, Application.version);
        }

        public static string InfoLine(float fps, string[] qualityNames, int quality, int width, int height, string scene, string version)
        {
            string frame = fps > 0f ? fps.ToString("0") + " fps, " + (MILLISECONDS / fps).ToString("0.0") + " ms" : "-- fps";
            string level = quality >= 0 && quality < qualityNames.Length ? qualityNames[quality] : "?";
            return frame + "   " + level + "   " + width + "x" + height + "\n" + scene + "   v" + version;
        }

        #endregion

        #region Actions

        public void Toggle()
        {
            _open = !_open;
            if (_open) Refresh();
        }

        public void ToggleFreeCamera()
        {
            if (_freeCamera.Active)
            {
                _freeCamera.Exit();
                Say("Free camera off");
                return;
            }
            Say(_freeCamera.Enter(Camera.main) ? "Free camera on" : "There is no camera to fly");
        }

        public void ToggleHud()
        {
            if (_hud.Hidden) _hud.Show();
            else _hud.Hide();
            Say(_hud.Hidden ? "HUD hidden" : "HUD shown");
        }

        public void StepVolume(int step)
        {
            global::VolumeManager manager = global::VolumeManager.ExistingInstance;
            ShowVolume(DebugVolumes.Step(DebugVolumes.CurrentIndex(manager), DebugVolumes.Count(manager), step));
        }

        public void ShowVolume(int index)
        {
            global::VolumeManager manager = global::VolumeManager.ExistingInstance;
            Say(DebugVolumes.Show(manager, index) ? "Volume: " + DebugVolumes.NameOf(manager, index) : "This scene has no volume list");
            _nextRefresh = 0f;
        }

        private void ApplyCheats()
        {
            _nextCheatApply = Time.unscaledTime + REFRESH_SECONDS;
            LevelController level = LevelController.ExistingInstance;
            if (level == null || DebugCheats.InNetworkSession) return;
            if (_aiDisarmed) DebugCheats.DisarmRivals(level, true);
            if (_aiShielded) DebugCheats.ShieldRivals(level, true);
            if (_noOverheat || _noCooldown) DebugCheats.FreePlayerWeapons(level, _noOverheat, _noCooldown);
        }

        public void PauseRivals(bool paused)
        {
            LevelController level = LevelController.ExistingInstance;
            if (level == null || DebugCheats.InNetworkSession) return;
            _aiPaused = paused;
            DebugCheats.PauseRivals(level, paused);
            PauseManager pause = PauseManager.ExistingInstance;
            if (pause == null) return;
            pause.onGameResumed.RemoveListener(PauseRivalsAgain);
            if (paused) pause.onGameResumed.AddListener(PauseRivalsAgain);
        }

        private void PauseRivalsAgain()
        {
            if (_aiPaused) DebugCheats.PauseRivals(LevelController.ExistingInstance, true);
        }

        public void StartCustomMatch(int buildIndex, int rivals, int spawnPoint)
        {
            if (DebugCheats.InNetworkSession) return;
            DebugCustomMatch.Request(rivals, spawnPoint);
            _freeCamera.Exit();
            Time.timeScale = 1f;
            SceneManager.LoadScene(buildIndex);
        }

        public void SetTimeScale(float scale)
        {
            if (DebugCheats.InNetworkSession) return;
            _stepping = false;
            Time.timeScale = Mathf.Max(0f, scale);
        }

        public void StepOneFrame()
        {
            if (DebugCheats.InNetworkSession || Time.timeScale > 0f) return;
            Time.timeScale = 1f;
            _stepping = true;
        }

        public void TakeScreenshot(int superSize)
        {
            if (_capturing) return;
            StartCoroutine(_TakeScreenshot(Mathf.Max(1, superSize)));
        }

        private IEnumerator _TakeScreenshot(int superSize)
        {
            _capturing = true;
            yield return null;
            Directory.CreateDirectory(ScreenshotFolder);
            string path = Path.Combine(ScreenshotFolder, ScreenshotName(DateTime.Now));
            ScreenCapture.CaptureScreenshot(path, superSize);
            yield return null;
            yield return null;
            _capturing = false;
            _lastScreenshot = path;
            Say("Screenshot saved: " + path);
        }

        public static string ScreenshotName(DateTime moment) => "FAF_" + moment.ToString("yyyy-MM-dd_HH-mm-ss-fff") + ".png";

        private void Say(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + TOAST_SECONDS;
        }

        #endregion

        #region Input

        private void ReadHotkeys()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard[TOGGLE_KEY].wasPressedThisFrame) Toggle();
                if (keyboard[FREE_CAMERA_KEY].wasPressedThisFrame) ToggleFreeCamera();
                if (keyboard[HUD_KEY].wasPressedThisFrame) ToggleHud();
                if (keyboard[PREVIOUS_VOLUME_KEY].wasPressedThisFrame) StepVolume(-1);
                if (keyboard[NEXT_VOLUME_KEY].wasPressedThisFrame) StepVolume(1);
                if (keyboard[SCREENSHOT_KEY].wasPressedThisFrame) TakeScreenshot(1);
            }
            Gamepad pad = Gamepad.current;
            if (pad == null || !pad.leftStickButton.isPressed || !pad.rightStickButton.isPressed) return;
            if (pad.leftStickButton.wasPressedThisFrame || pad.rightStickButton.wasPressedThisFrame) Toggle();
        }

        private Vector3 ReadMove()
        {
            Vector3 move = Vector3.zero;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
                move += new Vector3(Axis(keyboard.dKey, keyboard.aKey), Axis(keyboard.eKey, keyboard.qKey), Axis(keyboard.wKey, keyboard.sKey));
            Gamepad pad = Gamepad.current;
            if (pad == null) return move;
            Vector2 stick = pad.leftStick.ReadValue();
            return move + new Vector3(stick.x, pad.rightTrigger.ReadValue() - pad.leftTrigger.ReadValue(), stick.y);
        }

        private Vector2 ReadLook()
        {
            Vector2 look = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse != null && (!_open || mouse.rightButton.isPressed)) look += mouse.delta.ReadValue() * MOUSE_DEGREES_PER_PIXEL;
            Gamepad pad = Gamepad.current;
            if (pad != null) look += pad.rightStick.ReadValue() * (STICK_DEGREES_PER_SECOND * Time.unscaledDeltaTime);
            return look;
        }

        private static float ReadSpeedFactor()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            bool fast = (keyboard != null && keyboard.shiftKey.isPressed) || (pad != null && pad.rightShoulder.isPressed);
            bool slow = (keyboard != null && keyboard.ctrlKey.isPressed) || (pad != null && pad.leftShoulder.isPressed);
            return DebugFreeCamera.SpeedFactor(fast, slow);
        }

        private static float Axis(ButtonControl positive, ButtonControl negative) =>
            (positive.isPressed ? 1f : 0f) - (negative.isPressed ? 1f : 0f);

        private void ApplyInputBlock()
        {
            bool wanted = _freeCamera.Active || _open;
            InputBlockTypes type = _freeCamera.Active ? InputBlockTypes.ALL_MECHANICS : InputBlockTypes.SHOOTING_MECHANICS;
            if (_blockedInput != null && wanted && _blockedType == type) return;
            if (_blockedInput != null) _blockedInput.EnableInput(_blockedType);
            _blockedInput = null;
            if (!wanted) return;
            PlayerInputController input = PlayerInputController.ExistingInstance;
            if (input == null || input.IsAllMechanicsInputsBlocked || (type == InputBlockTypes.SHOOTING_MECHANICS && input.IsShootingInputsBlocked)) return;
            input.BlockInput(type);
            _blockedInput = input;
            _blockedType = type;
        }

        private void ApplyCursor()
        {
            bool wanted = _open || _freeCamera.Active;
            if (!wanted)
            {
                if (!_ownsCursor) return;
                _ownsCursor = false;
                PauseManager pause = PauseManager.ExistingInstance;
                bool paused = pause != null && pause.IsGamePaused;
                Cursor.lockState = paused ? CursorLockMode.None : _gameCursorLock;
                Cursor.visible = paused || _gameCursorVisible;
                return;
            }
            if (!_ownsCursor || Cursor.lockState != _appliedCursorLock || Cursor.visible != _appliedCursorVisible)
            {
                _gameCursorLock = Cursor.lockState;
                _gameCursorVisible = Cursor.visible;
                _ownsCursor = true;
            }
            _appliedCursorLock = _open ? CursorLockMode.None : CursorLockMode.Locked;
            _appliedCursorVisible = _open;
            Cursor.lockState = _appliedCursorLock;
            Cursor.visible = _appliedCursorVisible;
        }

        #endregion

        #region Window

        public static float GuiScale(int screenHeight) => Mathf.Max(1f, screenHeight / REFERENCE_HEIGHT);

        private void OnGUI()
        {
            if (_capturing) return;
            float scale = GuiScale(Screen.height);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            BuildStyles();

            if (!_open)
            {
                if (_toast != null && Time.unscaledTime < _toastUntil) DrawToast();
                return;
            }
            float screenWidth = Screen.width / scale;
            float screenHeight = Screen.height / scale;
            _window.width = WIDTH;
            _window.height = Mathf.Min(MAX_HEIGHT, screenHeight - 2f * MARGIN);
            _window.x = Mathf.Clamp(_window.x, 0f, Mathf.Max(0f, screenWidth - _window.width));
            _window.y = Mathf.Clamp(_window.y, 0f, Mathf.Max(0f, screenHeight - _window.height));
            GUI.DrawTexture(_window, _background);
            _window = GUI.Window(WINDOW_ID, _window, DrawWindow, TITLE);
        }

        private void DrawToast()
        {
            var area = new Rect(MARGIN, TOAST_TOP, TOAST_WIDTH, TOAST_HEIGHT);
            GUI.DrawTexture(area, _background);
            GUI.Label(area, " " + _toast);
        }

        private void BuildStyles()
        {
            if (_background != null) return;
            _background = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _background.SetPixel(0, 0, BACKGROUND);
            _background.Apply();
            _headerStyle = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            _wrapStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
            _boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            _choiceStyle = new GUIStyle(GUI.skin.button);
            _choiceStyle.onNormal.textColor = HIGHLIGHT;
            _choiceStyle.onHover.textColor = HIGHLIGHT;
            _choiceStyle.onActive.textColor = HIGHLIGHT;
        }

        private void DrawWindow(int id)
        {
            GUILayout.Label(_info, _wrapStyle);
            if (_toast != null && Time.unscaledTime < _toastUntil) GUILayout.Label(_toast, _wrapStyle);
            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawVolumes();
            DrawCamera();
            DrawView();
            if (_networkSession) GUILayout.Label(NETWORK_SESSION, _wrapStyle);
            else
            {
                DrawTime();
                if (_level == null) GUILayout.Label(NO_MATCH, _wrapStyle);
                else
                {
                    DrawPlayer();
                    DrawAi();
                    DrawMatch();
                }
                DrawCustomMatch();
            }
            GUILayout.EndScrollView();
            GUILayout.Label(HOTKEYS, _wrapStyle);
            GUI.DragWindow(new Rect(0f, 0f, WIDTH, TITLE_HEIGHT));
        }

        private void DrawVolumes()
        {
            if (!Section("Volume", ref _showVolume)) return;
            if (_volumeNames.Length == 0)
            {
                GUILayout.Label("This scene has no volume list.");
                return;
            }
            int current = DebugVolumes.CurrentIndex(_volumes);
            int picked = GUILayout.SelectionGrid(current, _volumeNames, VOLUME_COLUMNS, _choiceStyle);
            if (picked != current) ShowVolume(picked);
            if (_networkSession) GUILayout.Label("Only changes what this machine shows.", _wrapStyle);

            if (_moodVolumes.Length == 0) return;
            _showOverrides = GUILayout.Toggle(_showOverrides, " Show its overrides");
            if (!_showOverrides) return;
            foreach (Volume volume in _moodVolumes)
                if (volume != null) DrawOverrides(volume);
        }

        private void DrawOverrides(Volume volume)
        {
            GUILayout.Label(volume.name + (volume.sharedProfile != null ? "  (" + volume.sharedProfile.name + ")" : string.Empty), _boldStyle);
            volume.weight = Slider("Weight", volume.weight, 0f, 1f, volume.weight.ToString("0.00"));
            foreach (VolumeComponent component in volume.profile.components)
                if (component != null) component.active = GUILayout.Toggle(component.active, " " + component.GetType().Name);
        }

        private void DrawCamera()
        {
            if (!Section("Camera", ref _showCamera)) return;
            if (GUILayout.Toggle(_freeCamera.Active, "Free camera (F2)", _choiceStyle) != _freeCamera.Active) ToggleFreeCamera();
            if (!_freeCamera.Active) return;
            float speed = Slider("Speed", Mathf.Log(_freeCamera.Speed), Mathf.Log(DebugFreeCamera.MIN_SPEED), Mathf.Log(DebugFreeCamera.MAX_SPEED),
                _freeCamera.Speed.ToString("0") + " m/s");
            _freeCamera.Speed = Mathf.Exp(speed);
            _freeCamera.FieldOfView = Slider("Field of view", _freeCamera.FieldOfView, DebugFreeCamera.MIN_FIELD_OF_VIEW,
                DebugFreeCamera.MAX_FIELD_OF_VIEW, _freeCamera.FieldOfView.ToString("0"));
            if (DebugCheats.Allowed(_level != null, _networkSession) && GUILayout.Button("Bring my car here"))
                DebugCheats.BringPlayerTo(_level, _freeCamera.Pose.position, _freeCamera.Pose.yaw);
            GUILayout.Label(FREE_CAMERA_HELP, _wrapStyle);
        }

        private void DrawView()
        {
            if (!Section("View", ref _showView)) return;
            if (GUILayout.Toggle(_hud.Hidden, "Hide the HUD (F4)", _choiceStyle) != _hud.Hidden) ToggleHud();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Screenshot (F9)")) TakeScreenshot(1);
            if (GUILayout.Button("x2")) TakeScreenshot(2);
            if (GUILayout.Button("x4")) TakeScreenshot(4);
            if (GUILayout.Button("Open folder"))
            {
                Directory.CreateDirectory(ScreenshotFolder);
                Application.OpenURL(ScreenshotFolder);
            }
            GUILayout.EndHorizontal();
            if (_lastScreenshot.Length > 0) GUILayout.Label(_lastScreenshot, _wrapStyle);
            GUILayout.Label("Graphics quality (not saved)");
            int current = QualitySettings.GetQualityLevel();
            int picked = GUILayout.SelectionGrid(current, _qualityNames, QUALITY_COLUMNS, _choiceStyle);
            if (picked != current) GraphicsQuality.Apply(picked);
        }

        private void DrawTime()
        {
            if (!Section("Time", ref _showTime)) return;
            int current = Array.FindIndex(TIME_SCALES, scale => Mathf.Approximately(scale, Time.timeScale));
            int picked = GUILayout.SelectionGrid(current, _timeLabels, TIME_SCALES.Length, _choiceStyle);
            if (picked != current && picked >= 0) SetTimeScale(TIME_SCALES[picked]);
            GUI.enabled = Mathf.Approximately(Time.timeScale, 0f);
            if (GUILayout.Button("Step one frame")) StepOneFrame();
            GUI.enabled = true;
        }

        private void DrawPlayer()
        {
            if (!Section("Player", ref _showPlayer)) return;
            if (_stats == null)
            {
                GUILayout.Label("There is no local car yet.");
                return;
            }
            _stats.DebugGodMode = GUILayout.Toggle(_stats.DebugGodMode, "God mode: no damage, no death", _choiceStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heal " + DEBUG_ENDURANCE)) _stats.DebugRecover100Endurance();
            if (GUILayout.Button("Take " + DEBUG_ENDURANCE)) _stats.DebugTake100Endurance();
            if (GUILayout.Button("Die"))
            {
                _stats.DebugGodMode = false;
                _stats.DebugDie();
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Damage " + _stats.DamagePercent.ToString("0") + " %");
            bool noOverheat = GUILayout.Toggle(_noOverheat, "Never overheat", _choiceStyle);
            bool noCooldown = GUILayout.Toggle(_noCooldown, "Ultimate without cooldown", _choiceStyle);
            if (noOverheat == _noOverheat && noCooldown == _noCooldown) return;
            _noOverheat = noOverheat;
            _noCooldown = noCooldown;
            DebugCheats.FreePlayerWeapons(_level, _noOverheat, _noCooldown);
        }

        private void DrawAi()
        {
            if (!Section("AI", ref _showAi)) return;
            if (GUILayout.Toggle(_aiPaused, "Pause every AI", _choiceStyle) != _aiPaused) PauseRivals(!_aiPaused);
            bool disarmed = GUILayout.Toggle(_aiDisarmed, "AIs cannot shoot", _choiceStyle);
            if (disarmed != _aiDisarmed)
            {
                _aiDisarmed = disarmed;
                DebugCheats.DisarmRivals(_level, disarmed);
            }
            bool shielded = GUILayout.Toggle(_aiShielded, "AIs take no damage", _choiceStyle);
            if (shielded != _aiShielded)
            {
                _aiShielded = shielded;
                DebugCheats.ShieldRivals(_level, shielded);
            }
            if (GUILayout.Button("Remove every car but mine (the match goes on)")) Say("Cars removed: " + DebugCheats.RemoveRivals(_level));
        }

        private void DrawCustomMatch()
        {
            if (!Section("Custom match", ref _showCustomMatch)) return;
            if (_mapNames.Length == 0)
            {
                GUILayout.Label("This build has no maps.");
                return;
            }
            _customMap = Mathf.Clamp(GUILayout.SelectionGrid(_customMap, _mapNames, MAP_COLUMNS, _choiceStyle), 0, _mapNames.Length - 1);
            _customRivals = Mathf.Round(Slider("Rivals", _customRivals, 0f, MAX_CUSTOM_RIVALS, _customRivals.ToString("0")));
            _customSpawn = Mathf.Round(Slider("Spawn point", _customSpawn, DebugCustomMatch.RANDOM_SPAWN, MAX_CUSTOM_SPAWN,
                _customSpawn < 0f ? "random" : (_customSpawn + 1f).ToString("0")));
            GUILayout.Label("Rivals and spawn point are cut down to what the map holds.", _wrapStyle);
            if (GUILayout.Button("Start")) StartCustomMatch(_mapBuildIndices[_customMap], (int)_customRivals, (int)_customSpawn);
        }

        private void DrawMatch()
        {
            if (!Section("Match", ref _showMatch)) return;
            float? storm = _level.StormSecondsLeft;
            GUILayout.BeginHorizontal();
            GUILayout.Label(storm.HasValue ? "Storm in " + storm.Value.ToString("0") + " s" : "No storm countdown running");
            GUI.enabled = storm.HasValue;
            if (GUILayout.Button("Call the storm now")) _level.CallStormNow();
            GUI.enabled = true;
            GUILayout.EndHorizontal();
            GUILayout.Label("Storm speed, once it has been called");
            GUI.enabled = _storm != null;
            int speed = _storm != null ? Array.FindIndex(STORM_SPEEDS, value => Mathf.Approximately(value, _storm.DebugSpeed)) : -1;
            int pickedSpeed = GUILayout.SelectionGrid(speed, STORM_LABELS, STORM_SPEEDS.Length, _choiceStyle);
            if (_storm != null && pickedSpeed != speed && pickedSpeed >= 0) _storm.DebugSpeed = STORM_SPEEDS[pickedSpeed];
            GUI.enabled = true;
            if (GUILayout.Button("Kill every rival (wins the match)")) Say("Rivals killed: " + DebugCheats.KillRivals(_level));
        }

        private bool Section(string title, ref bool open)
        {
            GUILayout.Space(SECTION_SPACE);
            if (GUILayout.Button((open ? "-  " : "+  ") + title, _headerStyle)) open = !open;
            return open;
        }

        private static float Slider(string label, float value, float min, float max, string shown)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label + " " + shown, GUILayout.Width(LABEL_WIDTH));
            value = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }

        #endregion
    }
}
