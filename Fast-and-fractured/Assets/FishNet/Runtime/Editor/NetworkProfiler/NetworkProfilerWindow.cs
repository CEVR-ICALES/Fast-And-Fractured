// //PROSTART
//
// #if UNITY_EDITOR
// using System;
// using System.Collections.Generic;
// using System.Linq;
// using FishNet.Managing;
// using FishNet.Managing.Statistic;
// using GameKit.Dependencies.Utilities;
// using UnityEditor;
// using UnityEngine;
// using Random = UnityEngine.Random;
//
// namespace FishNet.Editing.NetworkProfiler
// {
//     public class NetworkProfilerWindow : EditorWindow
//     {
//         #region Internal.
//         /// <summary>
//         /// Current instances of this window.
//         /// </summary>
//         internal static readonly List<NetworkProfilerWindow> Instances = new();
//         #endregion
//
//         #region Private.
//         /// <summary>
//         /// The next time the window dimensions can be saved.
//         /// </summary>
//         private float _nextWindowSaveTime;
//         /// <summary>
//         /// Last saved window size.
//         /// </summary>
//         private Vector2 _lastSavedWindowSize;
//         /// <summary>
//         /// Last saved window position.
//         /// </summary>
//         private Vector2 _lastSavedWindowPosition;
//         /// <summary>
//         /// True if this instance should be running.
//         /// </summary>
//         private bool _isEnabled;
//         /// <summary>
//         /// True if on the server tab, false if on the client.
//         /// </summary>
//         private bool _onServerTab;
//         /// <summary>
//         /// Traffic statistics for this instance. 
//         /// </summary>
//         private NetworkTrafficStatistics _networkTrafficStatistics;
//         /// <summary>
//         /// Currently recorded statistics.
//         /// </summary>
//         private readonly Dictionary<uint, ProfiledTickData> _profiledTickData = new();
//         /// <summary>
//         /// Data which contains the largest bytes be it in or out.
//         /// </summary>
//         private ProfiledTickData _largestBytesData;
//         /// <summary>
//         /// True if subscribed to traffic events.
//         /// </summary>
//         private bool _subscribedToTrafficEvents;
//         /// <summary>
//         /// Toggle state of each graph packet type.
//         /// </summary>
//         private bool[] _packetTypeToggles = new bool[6];
//         /// <summary>
//         /// Starting positions of the bandwidth markers.
//         /// Index 0 is the lowest marker, while 1 is the largest marker.
//         /// </summary>
//         private Vector2[] _inboundBandwidthMarkers = new Vector2[2];
//         /// <summary>
//         /// Starting positions of the bandwidth markers.
//         /// Index 0 is the lowest marker, while 1 is the largest marker.
//         /// </summary>
//         private Vector2[] _outboundBandwidthMarkers = new Vector2[2];
//         /// <summary>
//         /// The current width of the graph.
//         /// </summary>
//         private float _graphWidth;
//         #endregion
//
//         #region Consts/readonly.
//         /// <summary>
//         /// Packe type box titles.
//         /// </summary>
//         private readonly string[] _packetTypeNames = { "Remote Procedure Call", "Broadcast", "SyncVar", "Replicate", "Reconcile", "Other" };
//         /// <summary>
//         /// Packet type box colors.
//         /// </summary>
//         private readonly Color[] _packetTypeBoxColors = { Color.red, Color.green, Color.blue, Color.yellow, Color.magenta, Color.cyan };
//         /// <summary>
//         /// Color to use for the window background.
//         /// </summary>
//         private static readonly Color32 BACKGROUND_COLOR = new(56, 56, 56, 56);
//         private const float INPUT_COLOR_MULTIPLIER = 0.65f;
//         private const float INPUT_OVERLAY_COLOR_MULTIPLIER = INPUT_COLOR_MULTIPLIER * 1.2f;
//         private const float SELECTED_COLOR_MULTIPLIER = 1.25f;
//         private static float SEPARATOR_COLOR_MULTIPLIER = Mathf.Lerp(1f, INPUT_COLOR_MULTIPLIER, 0.5f);
//         /// <summary>
//         /// Padding to use on any element to keep it away from edges.
//         /// </summary>
//         private const float NEAR_EDGE_PADDING = 8f;
//         /// <summary>
//         /// Default font size for labels.
//         /// </summary>
//         private const int LABEL_FONT_SIZE = 12;
//         /// <summary>
//         /// Name of this window.
//         /// </summary>
//         private const string WINDOW_NAME = "FishNet Network Profiler";
//         /// <summary>
//         /// EditorPrefs key to save last window size.
//         /// </summary>
//         private const string WINDOW_SIZE_PREFIX_PREF_NAME = "FishNet_NetworkProfilerWindowSize_";
//         /// <summary>
//         /// EditorPrefs key to save last window position.
//         /// </summary>
//         private const string WINDOW_POSITION_PREFIX_PREF_NAME = "FishNet_NetworkProfilerWindowPosition_";
//         /// <summary>
//         /// EditorPrefs float X name.
//         /// </summary>
//         private const string FLOAT_X_PREF_NAME = "X";
//         /// <summary>
//         /// EditorPrefs float Y name.
//         /// </summary>
//         private const string FLOAT_Y_PREF_NAME = "Y";
//         /// <summary>
//         /// Maximum size the window can be.
//         /// </summary>
//         private static readonly Vector2 DEFAULT_WINDOW_SIZE = new(850f, 700f);
//         /// <summary>
//         /// Minimum size the window must be.
//         /// </summary>
//         private static readonly Vector2 MINIMUM_WINDOW_SIZE = new(650f, 550f);
//         /// <summary>
//         /// X offset for the starting position on lines.
//         /// </summary>
//         private const float GRAPH_LINE_START_X_OFFSET = 65f;
//         /// <summary>
//         /// Allow saving window size at most this often.
//         /// </summary>
//         private const float WINDOW_SIZE_SAVE_INTERVAL = 0.5f;
//         private const float PADDING = 0f;
//         private const float LEGEND_WIDTH = 200f;
//         private const float GRAPH_BYTES_WIDTH = 0f;
//         private const float GRAPH_HEIGHT = 175f;
//         private const float SEPARATOR_HEIGHT = 2f;
//         private const float EMPTY_SPACE_HEIGHT = 300f;
//         #endregion
//
//         #region Initialize and deinitialize.
//         /// <summary>
//         /// Initializes Instances if an instance is open.
//         /// </summary>
//         internal static void InitializeInstances(NetworkManager manager)
//         {
//             if (Instances.Count == 0)
//                 return;
//
//             foreach (NetworkProfilerWindow window in Instances)
//                 window.InitializeIfNeeded(manager);
//         }
//
//         /// <summary>
//         /// Initializes if current traffic statistics is null.
//         /// </summary>
//         private void InitializeIfNeeded(NetworkManager manager)
//         {
//             if (_networkTrafficStatistics != null)
//                 return;
//
//             manager.StatisticsManager.TryGetNetworkTrafficStatistics(out _networkTrafficStatistics);
//
//             SubscribeToEvents(subscribe: true);
//         }
//
//         private void OnEnable()
//         {
//             Instances.Add(this);
//
//             LoadPacketTypeToggles();
//             LoadPositionAndSize();
//
//             SubscribeToEvents(subscribe: true);
//         }
//
//         private void OnDestroy() => OnDestroyOrDisable();
//         private void OnDisable() => OnDestroyOrDisable();
//
//         private void OnDestroyOrDisable()
//         {
//             Instances.Remove(this);
//
//             SaveWindowPositionAndSize(force: true);
//
//             SubscribeToEvents(subscribe: false);
//         }
//
//         /// <summary>
//         /// Changes subscription to eneded events.
//         /// </summary>
//         private void SubscribeToEvents(bool subscribe)
//         {
//             if (subscribe == _subscribedToTrafficEvents)
//                 return;
//
//             if (_networkTrafficStatistics == null)
//                 return;
//
//             if (subscribe)
//                 _networkTrafficStatistics.OnNetworkTraffic += NetworkTrafficStatistics_OnNetworkTraffic;
//             else
//                 _networkTrafficStatistics.OnNetworkTraffic -= NetworkTrafficStatistics_OnNetworkTraffic;
//         }
//         #endregion
//
//         #region Windows.
//         /// <summary>
//         /// Opens any current instance of the Network Profiler.
//         /// </summary>
//         [MenuItem("Tools/Fish-Networking/Utility/Network Profiler")]
//         public static void ShowNetworkProfiler() => ShowNetworkProfiler(newInstance: false);
//
//         /// <summary>
//         /// Opens a new instance of the Network Profiler.
//         /// </summary>
//         [MenuItem("Tools/Fish-Networking/Utility/New Network Profiler")]
//         public static void ShowNewNetworkProfiler() => ShowNetworkProfiler(newInstance: true);
//
//         /// <summary>
//         /// Shows the Network Profiler as a single or new instance.
//         /// </summary>
//         private static void ShowNetworkProfiler(bool newInstance)
//         {
//             /* If newInstance then always create, otherwise only create
//              * if there is currently not an instance. */
//             NetworkProfilerWindow window = newInstance || Instances.Count == 0 ? CreateInstance<NetworkProfilerWindow>() : Instances[0];
//
//             window.CompleteShow();
//         }
//
//         /// <summary>
//         /// Completes showing a window.
//         /// </summary>
//         private void CompleteShow()
//         {
//             titleContent = new(WINDOW_NAME, image: null, WINDOW_NAME);
//             minSize = MINIMUM_WINDOW_SIZE;
//
//             LoadPositionAndSize();
//
//             Show();
//         }
//
//         /// <summary>
//         /// Loads values for the current packet type toggles.
//         /// </summary>
//         private void LoadPacketTypeToggles()
//         {
//             for (int i = 0; i < _packetTypeToggles.Length; i++)
//             {
//                 string key = $"FishNet_PacketToggle_{_packetTypeNames[i]}";
//                 _packetTypeToggles[i] = EditorPrefs.GetBool(key, true);
//             }
//         }
//
//         /// <summary>
//         /// Saves values for the current packet type toggles.
//         /// </summary>
//         private void SavePacketTypeToggles(int? index = null)
//         {
//             if (index != null)
//             {
//                 string key = $"FishNet_PacketToggle_{_packetTypeNames[index.Value]}";
//                 EditorPrefs.SetBool(key, _packetTypeToggles[index.Value]);
//             }
//             else
//             {
//                 for (int i = 0; i < _packetTypeToggles.Length; i++)
//                 {
//                     string key = $"FishNet_PacketToggle_{_packetTypeNames[i]}";
//                     EditorPrefs.SetBool(key, _packetTypeToggles[i]);
//                 }
//             }
//         }
//
//         /// <summary>
//         /// Sets the initial position of the window when showing or enabling.
//         /// </summary>
//         private void LoadPositionAndSize()
//         {
//             //Set size for testing.
//             Vector2 lastSavedSize = GetEditorPrefs(WINDOW_SIZE_PREFIX_PREF_NAME, DEFAULT_WINDOW_SIZE);
//
//             /* Prefer settings but if nto available then use current. */
//             Vector2 lastSavedPosition = GetEditorPrefs(WINDOW_POSITION_PREFIX_PREF_NAME, Vector2.negativeInfinity);
//             if (lastSavedPosition == Vector2.negativeInfinity)
//                 lastSavedPosition = new(position.x, position.y);
//
//             //Becomes true if a correction was made.
//             bool correctedForOverflow = false;
//
//             //Take overflow off position if needed.
//             lastSavedPosition.x = CorrectOverflow(position.x, lastSavedPosition.x + lastSavedSize.x);
//             lastSavedPosition.y = CorrectOverflow(position.y, lastSavedPosition.y + lastSavedSize.y);
//
//             float CorrectOverflow(float lPosition, float overflow)
//             {
//                 const float edgePadding = 5f;
//                 lPosition -= edgePadding;
//
//                 if (overflow > lPosition)
//                 {
//                     lPosition -= overflow;
//                     correctedForOverflow = true;
//                 }
//
//                 return lPosition;
//             }
//
//             position = new(lastSavedPosition.x, lastSavedPosition.y, lastSavedSize.x, lastSavedSize.y);
//
//             if (correctedForOverflow)
//             {
//                 SaveWindowPositionAndSize(force: true);
//             }
//             else
//             {
//                 _lastSavedWindowSize = lastSavedSize;
//                 _lastSavedWindowPosition = lastSavedPosition;
//             }
//         }
//
//         /// <summary>
//         /// Returns a saved Vector2.
//         /// </summary>
//         private Vector2 GetEditorPrefs(string keyPrefix, Vector2 defaultValue)
//         {
//             Vector2 result = default;
//
//             result.x = EditorPrefs.GetFloat(keyPrefix + FLOAT_X_PREF_NAME, defaultValue.x);
//             result.y = EditorPrefs.GetFloat(keyPrefix + FLOAT_Y_PREF_NAME, defaultValue.y);
//
//             return result;
//         }
//
//         /// <summary>
//         /// Saves a vector2.
//         /// </summary>
//         private void SaveEditorPrefs(string keyPrefix, Vector2 value)
//         {
//             EditorPrefs.SetFloat(keyPrefix + FLOAT_X_PREF_NAME, value.x);
//             EditorPrefs.SetFloat(keyPrefix + FLOAT_Y_PREF_NAME, value.y);
//         }
//
//         /// <summary>
//         /// Saves current window size if it differs from last.
//         /// </summary>
//         /// <param name="force"></param>
//         private void SaveWindowPositionAndSize(bool force)
//         {
//             //Not enough time has passed.
//             if (!force && Time.realtimeSinceStartup < _nextWindowSaveTime)
//                 return;
//
//             _nextWindowSaveTime = Time.realtimeSinceStartup + WINDOW_SIZE_SAVE_INTERVAL;
//
//             Vector2 size = position.size;
//             //Really Unity ... use a decent naming conventions PLEASE.
//             Vector2 lPosition = position.position;
//
//             //Size is unchanged.
//             if (size != _lastSavedWindowSize)
//                 SaveEditorPrefs(WINDOW_SIZE_PREFIX_PREF_NAME, size);
//             if (lPosition != _lastSavedWindowPosition)
//                 SaveEditorPrefs(WINDOW_POSITION_PREFIX_PREF_NAME, lPosition);
//
//             //Update values.
//             _lastSavedWindowPosition = position.position;
//             _lastSavedWindowSize = position.size;
//         }
//
//         private void Update()
//         {
//             SaveWindowPositionAndSize(force: false);
//         }
//         #endregion
//
//         /// <summary>
//         /// Called when new traffic statistics are received.
//         /// </summary>
//         private void NetworkTrafficStatistics_OnNetworkTraffic(uint tick, BidirectionalNetworkTraffic serverTraffic, BidirectionalNetworkTraffic clientTraffic)
//         {
//             ProfiledTickData tickData = ResettableObjectCaches<ProfiledTickData>.Retrieve();
//
//             if (!tickData.TryInitialize(tick, serverTraffic, clientTraffic))
//             {
//                 ResettableObjectCaches<ProfiledTickData>.Store(tickData);
//                 return;
//             }
//
//             /* Make sure data is not already added. This should not be possible. */
//             if (!_profiledTickData.TryAdd(tick, tickData))
//             {
//                 NetworkManagerExtensions.LogError($"Tick [{tick}] has already been added to data.");
//                 StoreProfiledTickData(tickData);
//
//                 return;
//             }
//
//             Repaint();
//         }
//
//         #region GUI Rendering
//         private void OnGUI()
//         {
//             float windowWidth = position.width;
//             float windowHeight = position.height;
//
//             // Draw full window background
//             Rect windowRect = new(0f, 0f, windowWidth, windowHeight);
//             EditorGUI.DrawRect(windowRect, BACKGROUND_COLOR);
//
//             /* Rect to window size with padding from window edges. */
//             Rect paddedRect = new(PADDING, PADDING, windowWidth - PADDING * 2f, windowHeight - PADDING * 2f);
//
//             //Starting draw point.
//             Vector2 startingDrawOffset = new(paddedRect.x, paddedRect.y);
//             //Current draw offset.
//             Vector2 drawOffset = startingDrawOffset;
//
//             drawOffset += DrawHeader(paddedRect, drawOffset);
//             DrawFullWidthSeparator(paddedRect, drawOffset.y);
//
//             /* Reset the X on drawOffset since we are not starting a new 'row'. */
//             drawOffset.x = startingDrawOffset.x;
//
//             drawOffset += DrawLegend(paddedRect, drawOffset);
//
//             drawOffset += DrawGraph(paddedRect, drawOffset);
//
//             /* Reset the X on drawOffset since we are not starting a new 'row'. */
//             drawOffset.x = startingDrawOffset.x;
//
//             // Separator.
//             drawOffset += DrawFullWidthSeparator(paddedRect, drawOffset.y);
//         }
//
//         /// <summary>
//         /// Draws a separator across the remaining width of a rect.
//         /// </summary>
//         /// <remarks>Only Y value is increased in the return.</remarks>
//         private Vector2 DrawRemainingWidthSeparator(Rect rect, Vector2 offset, float? colorMultiplierOverride = null, float? heightOverride = null)
//         {
//             float seperatorY = offset.y + PADDING;
//             float height = heightOverride ?? SEPARATOR_HEIGHT;
//             Rect seperatorRect = new(rect.x, seperatorY, rect.width + offset.x + PADDING, height);
//
//             float colorMultiplier = colorMultiplierOverride ?? SEPARATOR_COLOR_MULTIPLIER;
//             EditorGUI.DrawRect(seperatorRect, GetMultipliedBackgroundColor(colorMultiplier));
//
//             return new(0f, seperatorY);
//         }
//
//         /// <summary>
//         /// Draws a separator across the entire rect.
//         /// </summary>
//         /// <remarks>Only Y value is increased in the return.</remarks>
//         private Vector2 DrawFullWidthSeparator(Rect rect, float y, float? colorMultiplierOverride = null, float? heightOverride = null)
//         {
//             float seperatorY = y + PADDING;
//             float height = heightOverride ?? SEPARATOR_HEIGHT;
//             Rect seperatorRect = new(rect.x, seperatorY, rect.width, height);
//
//             float colorMultiplier = colorMultiplierOverride ?? SEPARATOR_COLOR_MULTIPLIER;
//             EditorGUI.DrawRect(seperatorRect, GetMultipliedBackgroundColor(colorMultiplier));
//
//             return new(0f, seperatorY);
//         }
//
//         private Vector2 DrawHeader(Rect paddedRect, Vector2 drawOffset)
//         {
//             const float width = LEGEND_WIDTH;
//             const float height = 16f;
//
//             Rect popupRect = new(paddedRect.x + drawOffset.x, paddedRect.y + drawOffset.y, width, height);
//
//             string[] options = new[] { "  Server", "  Client" };
//             int index = EditorGUI.Popup(popupRect, 0, options);
//
//             // Use the selected value
//             string selectedValue = options[0];
//
//             return new(width, height);
//         }
//
//         private Vector2 DrawLegend(Rect paddedRect, Vector2 drawOffset)
//         {
//             //To accomodate for two graphs.
//             float leftColumnHeight = GetCombinedGraphHeight();
//
//             Rect legendRect = new(paddedRect.x + drawOffset.x, paddedRect.y + drawOffset.y, LEGEND_WIDTH, leftColumnHeight);
//             EditorGUI.DrawRect(legendRect, BACKGROUND_COLOR);
//
//             /* This is code to test the layout. */
//
//             Vector2 boxOutlineSize = new(12f, 12f);
//             Vector2 boxInnerSize = new(10f, 10f);
//
//             Color labelColor = Color.white * 0.75f;
//
//             //Draw each packetId.
//             for (int i = 0; i < _packetTypeNames.Length; i++)
//             {
//                 float y = legendRect.y + NEAR_EDGE_PADDING;
//                 y += i * NEAR_EDGE_PADDING + i * boxOutlineSize.y;
//
//                 /* Make a colored box. */
//                 //Outside.
//                 Rect outsideColorRect = new(legendRect.x + NEAR_EDGE_PADDING, y, boxOutlineSize.x, boxOutlineSize.y);
//                 EditorGUI.DrawRect(outsideColorRect, GetMultipliedBackgroundColor(0.15f));
//
//                 //Inside.
//                 Rect insideColorRect = new(legendRect.x + NEAR_EDGE_PADDING + 1f, y + 1f, boxInnerSize.x, boxInnerSize.y);
//                 Color insideColor = _packetTypeToggles[i] ? _packetTypeBoxColors[i] : Color.black;
//                 EditorGUI.DrawRect(insideColorRect, new(insideColor.r * 0.65f, insideColor.g * 0.65f, insideColor.b * 0.65f));
//
//                 //Invisible toggle button.
//                 Rect toggleSelectorRect = insideColorRect;
//                 toggleSelectorRect.width += 50f;
//                 bool toggleState = GUI.Toggle(toggleSelectorRect, _packetTypeToggles[i], "", GUIStyle.none);
//                 if (toggleState != _packetTypeToggles[i])
//                 {
//                     _packetTypeToggles[i] = toggleState;
//                     SavePacketTypeToggles(i);
//                     Repaint();
//                 }
//
//                 // Label to the right of colored box.
//
//                 const float labelWidth = 200f;
//                 Rect labelRect = new(insideColorRect.x + boxOutlineSize.x + NEAR_EDGE_PADDING, insideColorRect.y, labelWidth, LABEL_FONT_SIZE);
//                 GUI.Label(labelRect, _packetTypeNames[i], GetDefaultLabelStyle(LABEL_FONT_SIZE, labelColor));
//             }
//
//
//             return new(LEGEND_WIDTH, 0f);
//         }
//
//         private Vector2 DrawGraph(Rect paddedRect, Vector2 drawOffset)
//         {
//             drawOffset.x += PADDING;
//             _graphWidth = paddedRect.width - (LEGEND_WIDTH + GRAPH_BYTES_WIDTH + PADDING);
//
//             Rect outboundGraphRect = new(drawOffset.x, drawOffset.y, _graphWidth, GRAPH_HEIGHT);
//             EditorGUI.DrawRect(outboundGraphRect, GetMultipliedBackgroundColor(INPUT_COLOR_MULTIPLIER));
//
//             DrawLines(outboundGraphRect, out _outboundBandwidthMarkers[0], out _outboundBandwidthMarkers[1]);
//             DrawTitle(outboundGraphRect, "Outbound");
//
//             Rect inboundGraphRect = new(drawOffset.x, drawOffset.y + GRAPH_HEIGHT + PADDING, _graphWidth, GRAPH_HEIGHT);
//             EditorGUI.DrawRect(inboundGraphRect, GetMultipliedBackgroundColor(INPUT_COLOR_MULTIPLIER));
//
//             DrawLines(inboundGraphRect, out _inboundBandwidthMarkers[0], out _inboundBandwidthMarkers[1]);
//             DrawTitle(inboundGraphRect, "Inbound");
//
//             SimulateLines();
//
//             DrawRemainingWidthSeparator(outboundGraphRect, new(0f, GRAPH_HEIGHT + 16f), 0.5f, 3f);
//
//             void SimulateLines()
//             {
//                 const float barWidth = 8f;
//                 const float barHeight = 2f;
//
//                 float bandwidthYRange = _inboundBandwidthMarkers[1].y - _inboundBandwidthMarkers[0].y;
//                 Vector2 currentPosition = _inboundBandwidthMarkers[1];
//                 currentPosition.x += GRAPH_LINE_START_X_OFFSET + 5f;
//
//                 float valueMinimum = 0f;
//                 float valuePeak = bandwidthYRange;
//
//                 List<float> values = new();
//                 for (int i = 0; i < _packetTypeBoxColors.Length; i++)
//                     values.Add(Random.Range(valueMinimum, valuePeak));
//
//                 List<float> lastValues = new();
//                 for (int i = 0; i < values.Count; i++)
//                     lastValues.Add(values[i]);
//
//                 while (currentPosition.x < _graphWidth - barWidth)
//                 {
//                     //Randomize the value slightly.
//                     for (int i = 0; i < values.Count; i++)
//                     {
//                         float next = values[i] + Random.Range(-5f, 5f);
//                         next = Mathf.Clamp(next, valueMinimum, valuePeak);
//                         values[i] = next;
//                     }
//
//                     //Draw smallest first so larger valeus draw over.
//                     values.Sort();
//
//                     for (int i = 0; i < values.Count; i++)
//                     {
//                         Vector3 start = new(currentPosition.x, currentPosition.y - lastValues[i]);
//                         Vector3 end = new(currentPosition.x + barWidth, currentPosition.y - values[i]);
//                         
//                         Color originalColor = Handles.color;
//                         Handles.color = _packetTypeBoxColors[i];
//                         
//                         Handles.DrawAAPolyLine(3f, new[] { start, end });
//
//                         Handles.color = originalColor;
//                     }
//
//                     currentPosition.x += barWidth;
//
//                     for (int i = 0; i < values.Count; i++)
//                         lastValues[i] = values[i];
//                 }
//             }
//
//             void DrawLines(Rect lRect, out Vector2 firstStartingPosition, out Vector2 lastStartingPosition)
//             {
//                 firstStartingPosition = Vector2.zero;
//                 lastStartingPosition = Vector2.zero;
//
//                 const int lineCount = 5;
//
//                 Color color = GetMultipliedBackgroundColor(INPUT_OVERLAY_COLOR_MULTIPLIER);
//
//                 float spacing = lRect.height / (lineCount + 1);
//
//                 /* Only used to test layout. */
//                 int randomMaxBytes = UnityEngine.Random.Range(800, 12000);
//                 int lineIndex;
//                 /* Only used to test layout. */
//
//                 for (int i = 1; i <= lineCount; i++)
//                 {
//                     float y = lRect.y + spacing * i;
//                     Rect lineRect = new(lRect.x, y, lRect.width, SEPARATOR_HEIGHT);
//
//                     EditorGUI.DrawRect(lineRect, color);
//
//                     lineIndex = i;
//
//                     if (i == 1)
//                         firstStartingPosition = new(lRect.x, y);
//                     else if (i == lineCount)
//                         lastStartingPosition = new(lRect.x, y);
//
//                     DrawBytes(lRect.x, y);
//                 }
//
//                 void DrawBytes(float x, float y)
//                 {
//                     /* The label should not need to be larger than this width.
//                      * If there is ever a chance the label could overflow the window
//                      * this needs to be reconsidered. */
//                     const float labelWidth = 150f;
//                     //Places the text roughly center of the y.
//                     const float verticalOffset = LABEL_FONT_SIZE / 2f;
//
//                     Rect labelRect = new(x + NEAR_EDGE_PADDING, y - verticalOffset, labelWidth, LABEL_FONT_SIZE);
//                     GUI.Label(labelRect, GetBytesText(), GetDefaultLabelStyle(LABEL_FONT_SIZE));
//
//                     string GetBytesText()
//                     {
//                         float perLinePercent = 1f / lineCount;
//                         float percent = 1f + perLinePercent - (float)lineIndex / lineCount;
//                         int bytes = (int)(randomMaxBytes * percent);
//                         return $"{NetworkTrafficStatistics.FormatBytesToLargest(bytes)}";
//                     }
//                 }
//             }
//
//             void DrawTitle(Rect rect, string text)
//             {
//                 GUIStyle labelStyle = new(EditorStyles.label)
//                 {
//                     alignment = TextAnchor.LowerRight,
//                     fontSize = LABEL_FONT_SIZE,
//                     fontStyle = FontStyle.Bold,
//                     normal =
//                     {
//                         textColor = Color.white * 0.75f
//                     }
//                 };
//
//                 float padding = 5f;
//                 Rect labelRect = new(rect.x, rect.y, rect.width - padding, rect.height - padding);
//
//                 GUI.Label(labelRect, text, labelStyle);
//             }
//
//             return new(_graphWidth, GetCombinedGraphHeight());
//         }
//
//         private GUIStyle GetDefaultLabelStyle(float fontSize, Color? color = null)
//         {
//             return new(GUI.skin.label)
//             {
//                 alignment = TextAnchor.MiddleLeft,
//                 fontSize = (int)fontSize,
//                 normal =
//                 {
//                     textColor = color ?? GetMultipliedBackgroundColor(1.8f)
//                 }
//             };
//         }
//
//         // /// <summary>
//         // /// Draws a text label.
//         // /// </summary>
//         // private void DrawLabel(string text, Vector2 lPosition, Vector2 size, Color? colorOverride = null, GUIStyle styleOverride = null)
//         // {
//         //     GUIStyle labelStyle;
//         //     if (styleOverride == null)
//         //     {
//         //         labelStyle = new(GUI.skin.label)
//         //         {
//         //             alignment = TextAnchor.MiddleLeft,
//         //             fontSize = (int)size.y,
//         //             normal =
//         //             {
//         //                 textColor = colorOverride ?? GetMultipliedBackgroundColor(1.8f)
//         //             }
//         //         };
//         //     }
//         //     else
//         //     {
//         //         labelStyle = styleOverride;
//         //     }
//         //
//         //
//         //     Rect labelRect = new(lPosition.x, lPosition.y, size.x, size.y);
//         //
//         //     GUI.Label(labelRect, text, labelStyle);
//         // }
//
//         /// <summary>
//         /// The height of both graphs combined with padding.
//         /// </summary>
//         private float GetCombinedGraphHeight() => GRAPH_HEIGHT * 2f + PADDING;
//
//         /// <summary>
//         /// Applies a multiplier percentage to background color and returns value.
//         /// </summary>
//         private Color GetMultipliedBackgroundColor(float multiplier)
//         {
//             float r = (float)BACKGROUND_COLOR.r / byte.MaxValue * multiplier;
//             float g = (float)BACKGROUND_COLOR.g / byte.MaxValue * multiplier;
//             float b = (float)BACKGROUND_COLOR.b / byte.MaxValue * multiplier;
//
//             return new(r, g, b, 1f);
//         }
//         #endregion
//
//         /// <summary>
//         /// Clears all stored profile ticks.
//         /// </summary>
//         private void ClearProfiledTickData(uint retainedMinimumTick)
//         {
//             List<uint> keysToRemove = CollectionCaches<uint>.RetrieveList();
//
//             //Remove any entries before tick.
//             foreach (KeyValuePair<uint, ProfiledTickData> kvp in _profiledTickData)
//             {
//                 uint tick = kvp.Value.Tick;
//
//                 if (tick >= retainedMinimumTick)
//                     continue;
//
//                 keysToRemove.Add(tick);
//
//                 StoreProfiledTickData(kvp.Value);
//             }
//
//             //Quick clear if to remove all.
//             if (keysToRemove.Count == _profiledTickData.Count)
//             {
//                 _profiledTickData.Clear();
//             }
//             else
//             {
//                 foreach (uint v in keysToRemove)
//                     _profiledTickData.Remove(v);
//             }
//
//             CollectionCaches<uint>.Store(keysToRemove);
//         }
//
//         /// <summary>
//         /// Clears a ProfiledTickData.
//         /// </summary>
//         private void StoreProfiledTickData(ProfiledTickData value) => ResettableObjectCaches<ProfiledTickData>.Store(value);
//     }
// }
// #endif
// //PROEND