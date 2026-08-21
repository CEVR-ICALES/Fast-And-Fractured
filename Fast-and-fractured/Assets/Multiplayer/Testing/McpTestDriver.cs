using System.Collections;
using System.Linq;
using System.Text;
using FishNet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Testing
{
    /// <summary>
    /// Runtime helpers for driving the game from MCP (execute_code) during smoke tests —
    /// navigate menus, click buttons, inject virtual keyboard input, and read game state
    /// without touching the real devices.
    ///
    /// Usage from MCP execute_code while in play mode:
    ///   return FastAndFractured.Multiplayer.Testing.McpTestDriver.Snapshot();
    ///   FastAndFractured.Multiplayer.Testing.McpTestDriver.GoToScreen("MULTIPLAYER_HUB");
    ///   return FastAndFractured.Multiplayer.Testing.McpTestDriver.ListButtons();
    ///   FastAndFractured.Multiplayer.Testing.McpTestDriver.ClickButton("HostButton");
    ///   FastAndFractured.Multiplayer.Testing.McpTestDriver.HoldKey("W", 3f);   // accelerate 3s
    ///   return FastAndFractured.Multiplayer.Testing.McpTestDriver.GetLocalCarState();
    /// </summary>
    public static class McpTestDriver
    {
        // ── Menu navigation ──────────────────────────────────────────────────────
        public static string GoToScreen(string screensTypeName)
        {
            if (!System.Enum.TryParse(screensTypeName, true, out Enums.ScreensType screen))
                return $"ERROR: unknown ScreensType '{screensTypeName}'. Valid: {string.Join(", ", System.Enum.GetNames(typeof(Enums.ScreensType)))}";
            if (!MainMenuManager.HasInstance)
                return "ERROR: no MainMenuManager (not in a menu scene?)";

            MainMenuManager.Instance.TransitionBetweenScreens(screen, 0.1f);
            return $"OK: transitioned to {screen}";
        }

        public static string ClickButton(string buttonName)
        {
            var button = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .FirstOrDefault(b => b.gameObject.name == buttonName && b.isActiveAndEnabled);
            if (button == null)
                return $"ERROR: no active Button named '{buttonName}'. Use ListButtons().";

            button.onClick.Invoke();
            return $"OK: clicked '{buttonName}'";
        }

        public static string ListButtons()
        {
            var buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b => b.isActiveAndEnabled)
                .Select(b => b.gameObject.name)
                .OrderBy(n => n);
            return "Active buttons:\n" + string.Join("\n", buttons);
        }

        // ── Virtual keyboard input ───────────────────────────────────────────────
        private static Keyboard _virtualKeyboard;
        private static Mouse _virtualMouse;

        private static Keyboard GetVirtualKeyboard()
        {
            if (_virtualKeyboard == null || !_virtualKeyboard.added)
                _virtualKeyboard = InputSystem.AddDevice<Keyboard>("McpVirtualKeyboard");
            return _virtualKeyboard;
        }

        /// <summary>Presses a key for the given duration then releases (async; returns at once).</summary>
        public static string HoldKey(string keyName, float seconds)
        {
            if (!System.Enum.TryParse(keyName, true, out Key key))
                return $"ERROR: unknown Key '{keyName}'";
            GetRunner().StartCoroutine(HoldKeyRoutine(key, seconds));
            return $"OK: holding {key} for {seconds:F1}s";
        }

        public static string TapKey(string keyName) => HoldKey(keyName, 0.15f);

        public static string ReleaseAllKeys()
        {
            var kb = GetVirtualKeyboard();
            InputSystem.QueueStateEvent(kb, default(KeyboardState));
            return "OK: released";
        }

        /// <summary>Presses a virtual mouse button across real rendered frames, then removes the test device.</summary>
        public static string HoldMouseButton(string buttonName, float seconds)
        {
            if (!System.Enum.TryParse(buttonName, true, out MouseButton button))
                return $"ERROR: unknown MouseButton '{buttonName}'";

            GetRunner().StartCoroutine(HoldMouseButtonRoutine(button, seconds));
            return $"OK: holding mouse {button} for {seconds:F2}s";
        }

        private static IEnumerator HoldMouseButtonRoutine(MouseButton button, float seconds)
        {
            if (_virtualMouse == null || !_virtualMouse.added)
                _virtualMouse = InputSystem.AddDevice<Mouse>("McpVirtualMouse");

            Mouse mouse = _virtualMouse;
            InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(button));
            yield return new WaitForSecondsRealtime(seconds);
            InputSystem.QueueStateEvent(mouse, default(MouseState));
            yield return null;

            if (mouse != null && mouse.added)
                InputSystem.RemoveDevice(mouse);
            if (_virtualMouse == mouse)
                _virtualMouse = null;
        }

        private static IEnumerator HoldKeyRoutine(Key key, float seconds)
        {
            var kb = GetVirtualKeyboard();
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(seconds);
            InputSystem.QueueStateEvent(kb, default(KeyboardState));
        }

        // ── State inspection ─────────────────────────────────────────────────────
        public static string Snapshot()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"scene: {UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
            sb.AppendLine($"screen: {(MainMenuManager.HasInstance && MainMenuManager.Instance.CurrentScreen != null ? MainMenuManager.Instance.CurrentScreen.screenType.ToString() : "n/a")}");
            sb.AppendLine($"serverStarted: {InstanceFinder.IsServerStarted}  clientStarted: {InstanceFinder.IsClientStarted}");

            var lobby = Object.FindAnyObjectByType<NetworkedLobbyManager>();
            if (lobby != null)
                sb.AppendLine($"lobby: players={lobby.PlayerCharacterSelections.Count} started={lobby.GameStarted.Value}");

            sb.Append(GetLocalCarState());
            return sb.ToString();
        }

        public static string GetLocalCarState()
        {
            var vehicles = Object.FindObjectsByType<NetworkedVehicle>(FindObjectsSortMode.None);
            if (vehicles.Length == 0)
                return "cars: none spawned";

            var sb = new StringBuilder();
            foreach (var vehicle in vehicles)
            {
                var rb = vehicle.GetComponent<Rigidbody>();
                float speedKmh = rb != null ? rb.linearVelocity.magnitude * 3.6f : -1f;
                sb.AppendLine($"car '{vehicle.transform.root.name}/{vehicle.name}' owner={vehicle.IsOwner} " +
                              $"speed={speedKmh:F1}km/h pos={vehicle.transform.position}");
            }
            return sb.ToString();
        }

        // ── Internal coroutine host ──────────────────────────────────────────────
        private sealed class Runner : MonoBehaviour { }

        private static Runner _runner;

        private static Runner GetRunner()
        {
            if (_runner == null)
            {
                var go = new GameObject("[McpTestDriver]");
                Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<Runner>();
            }
            return _runner;
        }
    }
}
