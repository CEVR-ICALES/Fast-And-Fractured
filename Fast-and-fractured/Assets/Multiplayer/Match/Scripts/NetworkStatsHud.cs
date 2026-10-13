using System.Collections.Generic;
using FishNet;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Utilities.Managers.PauseSystem;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Match overlay with every player's connection: Wi-Fi / cable, signal bars and ping to the
    /// host, input delay and rollback frames (see <see cref="NetworkQuality"/> for what the
    /// numbers mean). The local player's row comes first. F3 hides / shows it; it also hides
    /// while the pause menu is open.
    ///
    /// Created at runtime in networked map scenes; no scene wiring.
    /// </summary>
    public sealed class NetworkStatsHud : MonoBehaviour
    {
        private const float ROW_HEIGHT = 20f;
        private const float REFRESH_SECONDS = 0.25f;

        private static NetworkStatsHud _instance;
        private static bool _hiddenByUser;

        private Canvas _canvas;
        private RectTransform _list;
        private readonly List<Row> _rows = new List<Row>();
        private float _nextRefresh;

        private sealed class Row
        {
            public GameObject Root;
            public TMP_Text Name;
            public NetworkSignalIndicator Signal;
            public TMP_Text Frames;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _hiddenByUser = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[NetworkStatsHud]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<NetworkStatsHud>();
        }

        private void Awake() => Build();

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) _hiddenByUser = !_hiddenByUser;

            bool show = !_hiddenByUser && InMatch() && !Paused();
            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show || Time.unscaledTime < _nextRefresh) return;
            _nextRefresh = Time.unscaledTime + REFRESH_SECONDS;
            Refresh();
        }

        private static bool InMatch()
        {
            var nm = InstanceFinder.NetworkManager;
            if (nm == null || !nm.IsClientStarted) return false;
            return SceneManager.GetActiveScene().name.StartsWith("Networked");
        }

        private static bool Paused()
        {
            var pause = PauseManager.ExistingInstance;
            return pause != null && pause.IsGamePaused;
        }

        // ── Content ────────────────────────────────────────────────────────────────────
        private void Refresh()
        {
            var nm = InstanceFinder.NetworkManager;
            int localId = nm.ClientManager.Connection != null ? nm.ClientManager.Connection.ClientId : -1;

            var entries = new List<KeyValuePair<int, NetworkQualityReport>>();
            // The local row uses this peer's own numbers straight away (the table lags a second).
            entries.Add(new KeyValuePair<int, NetworkQualityReport>(localId, NetworkQuality.Local));
            foreach (var pair in NetworkQuality.Players)
                if (pair.Key != localId) entries.Add(pair);
            entries.Sort((a, b) => a.Key == localId ? -1 : b.Key == localId ? 1 : a.Key.CompareTo(b.Key));

            while (_rows.Count < entries.Count) _rows.Add(NewRow());
            for (int i = 0; i < _rows.Count; i++)
            {
                bool used = i < entries.Count;
                _rows[i].Root.SetActive(used);
                if (used) Fill(_rows[i], entries[i].Value, entries[i].Key == localId, entries[i].Key);
            }
        }

        private static void Fill(Row row, NetworkQualityReport report, bool local, int clientId)
        {
            string name = string.IsNullOrEmpty(report.Name) ? $"Player {clientId}" : report.Name;
            row.Name.text = local ? $"<b>{name}</b>" : name;
            row.Name.color = local ? Color.white : new Color(1f, 1f, 1f, 0.8f);
            row.Signal.Set((LinkKind)report.Link, report.PingMs, report.IsHost ? "HOST" : null);

            if (report.IsHost)
                row.Frames.text = "<color=#9aa0a6>delay 0f · rollback 0f</color>";
            else
                row.Frames.text =
                    $"delay {Colored(report.DelayFrames, 3, 6)}f · rollback {Colored(report.RollbackFrames, 4, 8)}f" +
                    $" <color=#9aa0a6>(máx {report.RollbackMaxFrames})</color>";
        }

        private static string Colored(int value, int warn, int bad)
        {
            string color = value >= bad ? "#ff6b5e" : value >= warn ? "#f5b041" : "#7ee08a";
            return $"<color={color}>{value}</color>";
        }

        // ── Building ───────────────────────────────────────────────────────────────────
        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 40;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(transform, false);
            var background = panel.AddComponent<Image>();
            background.color = new Color(0.04f, 0.05f, 0.07f, 0.55f);
            background.raycastTarget = false;
            _list = (RectTransform)panel.transform;
            // Top-right, under the minimap and the match timer (the bottom-left is the portrait
            // and damage bar, the bottom-right the abilities).
            _list.anchorMin = _list.anchorMax = _list.pivot = new Vector2(1f, 1f);
            _list.anchoredPosition = new Vector2(-16f, -315f);
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 3f;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = panel.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _canvas.enabled = false;
        }

        private Row NewRow()
        {
            var root = new GameObject("Row", typeof(RectTransform));
            root.transform.SetParent(_list, false);
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;

            var row = new Row { Root = root };
            row.Name = NewText(root.transform, 130f);
            row.Signal = NetworkSignalIndicator.Create(root.transform, row.Name, ROW_HEIGHT);
            row.Frames = NewText(root.transform, -1f);
            return row;
        }

        private static TMP_Text NewText(Transform parent, float width)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = ROW_HEIGHT * 0.85f;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.richText = true;
            if (width > 0f)
            {
                var element = go.AddComponent<LayoutElement>();
                element.preferredWidth = element.minWidth = width;
            }
            return text;
        }
    }
}
