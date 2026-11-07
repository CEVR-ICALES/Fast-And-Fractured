using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utilities.Audio.Editor
{
    public sealed class AudioReferencesWindow : EditorWindow
    {
        public const string DEFAULT_CATALOG_PATH = "Assets/Resources/GameAudioCatalog.asset";
        private const string GAMEPLAY_CUE_PREFIX = "GameplayAudio/";
        private const double INDEX_BUDGET_SECONDS = 0.006;
        private const int MAX_DISPLAYED_ISSUES = 10;
        private const float CATEGORY_WIDTH = 115f;
        private const float REFRESH_BUTTON_WIDTH = 80f;
        private const float CANCEL_BUTTON_WIDTH = 70f;
        private const float CREATE_BUTTON_WIDTH = 150f;
        private const float PROGRESS_HEIGHT = 18f;
        private const float MINIMUM_LIST_WIDTH = 340f;
        private const float LIST_WIDTH_FRACTION = 0.52f;
        private const float MINIMUM_LIST_HEIGHT = 220f;
        private const float MAXIMUM_LIST_HEIGHT = 10000f;
        private const float ROW_HEIGHT = 48f;
        private const float SCROLLBAR_WIDTH = 18f;
        private const float ROW_RIGHT_INSET = 20f;
        private const float ROW_SPACING = 2f;
        private const float LABEL_PADDING = 5f;
        private const float TITLE_TOP_INSET = 2f;
        private const float TITLE_HEIGHT = 20f;
        private const float SUBTITLE_TOP_INSET = 23f;
        private const float SUBTITLE_HEIGHT = 18f;
        private const float ASSET_PATH_HEIGHT = 42f;
        private const float BACKEND_IDENTITY_HEIGHT = 32f;
        private static readonly Vector2 _minimumWindowSize = new Vector2(850f, 500f);
        private static readonly Color _selectionColor = new Color(0.2f, 0.45f, 0.65f, 0.28f);
        private static readonly string[] _categoryLabels = { "All", "Catalog", "References", "C# Usages" };
        private static readonly string[] _scopeLabels = { "Select a scope", "Create / change local override", "Edit shared source" };
        private enum ReferenceCategory { ALL, CATALOG, REFERENCES, CODE_USAGES }
        private enum EditScope { UNSELECTED, LOCAL_OVERRIDE, SHARED_SOURCE }

        [SerializeField] private AudioCueCatalog catalog;
        [SerializeField] private string search = "";
        private readonly AudioReferenceIndex _index = new AudioReferenceIndex();
        private AudioReferenceRecord _selected;
        private AudioReferenceValue _draft;
        private AudioPreviewSession _preview;
        private string _draftName;
        private string _message;
        private ReferenceCategory _category;
        private EditScope _inheritedMode;
        private Vector2 _scroll;
        private Vector2 _detailScroll;
        private string _lastFilter;
        private int _lastCount = -1;
        private int _lastCategory = -1;
        private List<AudioReferenceRecord> _filtered = new List<AudioReferenceRecord>();

        [MenuItem("Tools/Fast And Fractured/Audio References")]
        [MenuItem("Tools/Audio/Audio References")]
        public static void Open() => GetWindow<AudioReferencesWindow>("Audio References");

        private void OnEnable()
        {
            minSize = _minimumWindowSize;
            if (catalog == null) catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(DEFAULT_CATALOG_PATH);
            _preview = new AudioPreviewSession();
            _preview.Changed += Repaint;
            EditorApplication.update += UpdateIndex;
            Undo.undoRedoPerformed += OnUndo;
            BeginIndex();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateIndex;
            Undo.undoRedoPerformed -= OnUndo;
            _index.Cancel();
            if (_preview != null) { _preview.Changed -= Repaint; _preview.Dispose(); _preview = null; }
        }

        private void OnUndo()
        {
            if (catalog != null) catalog.Invalidate();
            BeginIndex();
        }

        private void BeginIndex()
        {
            _preview?.Stop();
            _index.Cancel();
            _selected = null;
            _index.Begin(catalog);
            _lastCount = -1;
            Repaint();
        }

        private void UpdateIndex()
        {
            if (!_index.Running || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode || AudioPreviewSession.AnyPlaying) return;
            double until = EditorApplication.timeSinceStartup + INDEX_BUDGET_SECONDS;
            do { _index.Step(); } while (_index.Running && EditorApplication.timeSinceStartup < until);
            Repaint();
        }

        public static AudioCueCatalog EnsureDefaultCatalog()
        {
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Update audio catalog");
            var result = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(DEFAULT_CATALOG_PATH);
            if (result == null)
            {
                if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
                result = CreateInstance<AudioCueCatalog>();
                AssetDatabase.CreateAsset(result, DEFAULT_CATALOG_PATH);
                Undo.RegisterCreatedObjectUndo(result, "Create audio catalog");
            }
            Undo.RecordObject(result, "Update audio catalog");
            bool changed = false;
            if (string.IsNullOrEmpty(result.BackendId) && !string.IsNullOrEmpty(AudioReferenceAdapters.BackendProviderId))
            {
                result.SetBackendId(AudioReferenceAdapters.BackendProviderId);
                changed = true;
            }
            if (AudioReferenceAdapters.SynchronizeCatalogBindings != null) changed |= AudioReferenceAdapters.SynchronizeCatalogBindings(result);
            foreach (string id in AudioReferenceAdapters.CueChoices)
            {
                var resource = AudioReferenceAdapters.ResolveBackendReference != null ? AudioReferenceAdapters.ResolveBackendReference(id) : new AudioBackendReference(null, id);
                if (!string.IsNullOrEmpty(resource.StableId) && result.Entries.Any(e => e.backendStableId == resource.StableId)) continue;
                string name = id == "GameplayAudio/Dash" ? "CommonDash" : id.StartsWith(GAMEPLAY_CUE_PREFIX, StringComparison.Ordinal) ? "Common" + id.Substring(GAMEPLAY_CUE_PREFIX.Length) : id;
                changed |= result.AddMissing(id, name, id);
            }
            if (AudioReferenceAdapters.SynchronizeCatalogBindings != null) changed |= AudioReferenceAdapters.SynchronizeCatalogBindings(result);
            changed |= AudioCueCodeGenerator.EnsureNames(result);
            if (changed) { EditorUtility.SetDirty(result); AssetDatabase.SaveAssetIfDirty(result); }
            AudioCueCodeGenerator.Generate(result);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(undoGroup);
            Undo.IncrementCurrentGroup();
            return result;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                search = GUILayout.TextField(search, GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField);
                _category = (ReferenceCategory)EditorGUILayout.Popup((int)_category, _categoryLabels, EditorStyles.toolbarPopup, GUILayout.Width(CATEGORY_WIDTH));
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(REFRESH_BUTTON_WIDTH))) BeginIndex();
                if (_index.Running && GUILayout.Button("Cancel", EditorStyles.toolbarButton, GUILayout.Width(CANCEL_BUTTON_WIDTH))) _index.Cancel();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                var next = (AudioCueCatalog)EditorGUILayout.ObjectField("Catalog", catalog, typeof(AudioCueCatalog), false);
                if (next != catalog) { catalog = next; BeginIndex(); }
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    if (GUILayout.Button("Create / Add Cues", GUILayout.Width(CREATE_BUTTON_WIDTH))) { catalog = EnsureDefaultCatalog(); BeginIndex(); }
            }
            if (catalog == null) EditorGUILayout.HelpBox("Create a catalog to edit cues such as CommonDash. Changes are saved in that asset and affect ID-based usages.", MessageType.Info);
            if (!string.IsNullOrEmpty(_message)) EditorGUILayout.HelpBox(_message, MessageType.Info);
            if (_index.Cancelled) EditorGUILayout.HelpBox("Index cancelled: results are partial. Click Refresh to complete the scan.", MessageType.Warning);
            if (_index.Running)
            {
                var progress = GUILayoutUtility.GetRect(0, PROGRESS_HEIGHT, GUILayout.ExpandWidth(true));
                EditorGUI.ProgressBar(progress, _index.Progress, _index.CurrentPath ?? "Preparing index");
            }
            if (_preview != null)
            {
                if (_preview.IsPlaying) EditorGUILayout.LabelField("Previewing", _preview.Label, EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(_preview.Error)) EditorGUILayout.HelpBox(_preview.Error, MessageType.Warning);
            }
            string filter = search.Trim().ToLowerInvariant();
            if (_lastCount != _index.Records.Count || _lastFilter != filter || _lastCategory != (int)_category)
            {
                _filtered = _index.Records.Where(r => (filter.Length == 0 || r.SearchText.Contains(filter)) && MatchesCategory(r)).ToList();
                _lastCount = _index.Records.Count; _lastFilter = filter; _lastCategory = (int)_category;
            }
            using (new EditorGUILayout.HorizontalScope()) { DrawList(); DrawDetails(); }
            EditorGUILayout.LabelField(_filtered.Count + " visible / " + _index.Records.Count + " references and usages. Issues: " + _index.Errors.Count, EditorStyles.miniLabel);
            EditorGUILayout.HelpBox("Project assets: clips, cues and serialized references, including Timeline subassets. C# usages are read-only. Dynamic IDs, internal bank samples, packages and generated caches are not edited here. Large scenes may take time to load; scanning can be cancelled between assets.", MessageType.None);
            if (_index.Errors.Count > 0 && GUILayout.Button("Show First Issues")) EditorUtility.DisplayDialog("Index Issues", string.Join("\n", _index.Errors.Take(MAX_DISPLAYED_ISSUES)), "Close");
        }

        private bool MatchesCategory(AudioReferenceRecord record)
        {
            switch (_category)
            {
                case ReferenceCategory.ALL: return true;
                case ReferenceCategory.CATALOG: return record.IsCatalog;
                case ReferenceCategory.REFERENCES: return !record.IsCatalog && !record.IsCode;
                case ReferenceCategory.CODE_USAGES: return record.IsCode;
                default: return false;
            }
        }

        private void DrawList()
        {
            float width = Mathf.Max(MINIMUM_LIST_WIDTH, position.width * LIST_WIDTH_FRACTION);
            Rect area = GUILayoutUtility.GetRect(width, width, MINIMUM_LIST_HEIGHT, MAXIMUM_LIST_HEIGHT, GUILayout.ExpandHeight(true));
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0, 0, width - SCROLLBAR_WIDTH, _filtered.Count * ROW_HEIGHT));
            int first = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / ROW_HEIGHT));
            int last = Mathf.Min(_filtered.Count, first + Mathf.CeilToInt(area.height / ROW_HEIGHT) + 1);
            for (int i = first; i < last; i++)
            {
                var record = _filtered[i];
                var rect = new Rect(0, i * ROW_HEIGHT, width - ROW_RIGHT_INSET, ROW_HEIGHT - ROW_SPACING);
                if (record == _selected) EditorGUI.DrawRect(rect, _selectionColor);
                if (GUI.Button(rect, GUIContent.none, GUIStyle.none)) Select(record);
                float labelWidth = rect.width - LABEL_PADDING * 2;
                GUI.Label(new Rect(LABEL_PADDING, rect.y + TITLE_TOP_INSET, labelWidth, TITLE_HEIGHT), (record.IsCatalog ? "[Global] " : record.IsCode ? "[C#] " : "") + record.DisplayName, EditorStyles.boldLabel);
                GUI.Label(new Rect(LABEL_PADDING, rect.y + SUBTITLE_TOP_INSET, labelWidth, SUBTITLE_HEIGHT), record.Location.AssetPath + (record.IsCode ? ":" + record.Line : " / " + record.Location.PropertyPath), EditorStyles.miniLabel);
            }
            GUI.EndScrollView();
        }

        private void Select(AudioReferenceRecord record)
        {
            _preview?.Stop();
            _selected = record;
            _draft = new AudioReferenceValue { Text = record.Value.Text, StableId = record.Value.StableId, Asset = record.AdapterId == "unity-clip" ? AudioPreviewSession.ResolveClip(record.Value) : record.Value.Asset };
            _draftName = record.DisplayName;
            _inheritedMode = EditScope.UNSELECTED;
        }

        private void DrawDetails()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
                if (_selected == null) EditorGUILayout.LabelField("Select an audio reference.", EditorStyles.wordWrappedLabel);
                else
                {
                    var record = _selected;
                    EditorGUILayout.LabelField(record.DisplayName, EditorStyles.boldLabel);
                    EditorGUILayout.SelectableLabel(record.Location.AssetPath, EditorStyles.wordWrappedLabel, GUILayout.Height(ASSET_PATH_HEIGHT));
                    EditorGUILayout.LabelField("Owner", record.Location.ObjectName);
                    EditorGUILayout.LabelField("Type", record.Location.ObjectType);
                    EditorGUILayout.LabelField("Property", record.Location.PropertyPath);
                    EditorGUILayout.LabelField("Assigned Audio", record.Value.Display, EditorStyles.wordWrappedLabel);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Play Assigned")) PlayAssigned(record);
                        using (new EditorGUI.DisabledScope(_preview == null || !_preview.IsPlaying))
                            if (GUILayout.Button("Stop")) _preview.Stop();
                    }
                    if (GUILayout.Button(record.IsCode ? "Open Code at Line" : "Locate Asset"))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(record.Location.AssetPath);
                        if (record.IsCode) AssetDatabase.OpenAsset(asset, record.Line);
                        else { Selection.activeObject = asset; EditorGUIUtility.PingObject(asset); }
                    }
                    if (record.IsCode)
                    {
                        EditorGUILayout.HelpBox("This code usage reads a cue ID. Edit its global catalog binding instead of changing the literal here.", MessageType.Info);
                        var entry = _index.Records.FirstOrDefault(r => r.IsCatalog && r.CueId == record.CueId);
                        if (entry != null && GUILayout.Button("Edit Global Cue")) Select(entry);
                    }
                    else DrawEditor(record);
                }
                EditorGUILayout.EndScrollView();
            }
        }

        private void PlayAssigned(AudioReferenceRecord record)
        {
            try
            {
                if (record.IsCode) { _preview.PlayCue(record.CueId, catalog); return; }
                var value = record.Value;
                var target = AudioReferenceObjects.Resolve(record.Location);
                if (target != null)
                {
                    using (var so = new SerializedObject(target))
                    {
                        var property = so.FindProperty(record.Location.PropertyPath);
                        var adapter = AudioReferenceAdapters.Find(record.AdapterId);
                        if (property == null || adapter == null || !adapter.Matches(property)) throw new InvalidOperationException("The reference changed. Refresh the index.");
                        value = adapter.Read(property);
                    }
                }
                else if (!string.IsNullOrEmpty(record.DependencyHash) && AssetDatabase.GetAssetDependencyHash(record.Location.AssetPath).ToString() != record.DependencyHash)
                    throw new InvalidOperationException("The asset changed. Refresh the index before previewing its assigned audio.");
                PreviewValue(record, value, true);
            }
            catch (Exception e) { _message = e.Message; }
        }

        private void PreviewValue(AudioReferenceRecord record, AudioReferenceValue value, bool assigned = false)
        {
            if (record.AdapterId == "unity-clip") _preview.PlayClip(AudioPreviewSession.ResolveClip(value));
            else if (record.IsCatalog && assigned) _preview.PlayResource(new AudioBackendReference(value.StableId, value.Text));
            else _preview.PlayCue(value.Text, catalog, record.IsCatalog || record.AdapterId == "fmod-event");
        }

        private void DrawEditor(AudioReferenceRecord record)
        {
            if (record.IsCatalog)
            {
                EditorGUILayout.LabelField("Stable ID", record.CueId);
                if (catalog != null && catalog.TryGetEntry(new AudioCueId(record.CueId), out var definition))
                {
                    EditorGUILayout.LabelField("Code Reference", "GameAudioCues." + definition.codeName);
                    EditorGUILayout.SelectableLabel("Backend Identity: " + definition.backendStableId, EditorStyles.wordWrappedMiniLabel, GUILayout.Height(BACKEND_IDENTITY_HEIGHT));
                }
                _draftName = EditorGUILayout.TextField("Searchable Name", _draftName);
                EditorGUILayout.HelpBox("Global change for all ID-based usages. Independent legacy FMOD fields are not replaced.", MessageType.Info);
            }
            if (record.AdapterId == "unity-clip")
            {
                _draft.Asset = EditorGUILayout.ObjectField("New Clip", _draft.Asset, typeof(AudioClip), false);
                if (GUILayout.Button("Choose Audio..."))
                {
                    _preview?.Stop();
                    AudioCuePicker.ShowClips(clip => { if (this != null && _selected == record) { _draft.Asset = clip; Repaint(); } });
                }
            }
            else
            {
                _draft.Text = EditorGUILayout.TextField(record.IsCatalog ? "Backend Binding" : "New Reference", _draft.Text ?? "");
                if (GUILayout.Button("Choose Audio..."))
                {
                    _preview?.Stop();
                    bool backend = record.IsCatalog || record.AdapterId == "fmod-event";
                    var options = backend ? AudioReferenceAdapters.CueChoices : catalog != null ? catalog.Entries.Where(e => e != null).Select(e => e.id).ToArray() : Array.Empty<string>();
                    AudioCuePicker.Show(options, value =>
                    {
                        if (this == null || _selected != record) return;
                        _draft.Text = record.AdapterId == "fmod-event" && !string.IsNullOrEmpty(value) ? "event:/" + value : value;
                        Repaint();
                    }, value => backend || catalog == null ? new AudioBackendReference(null, value) : catalog.ResolveBinding(new AudioCueId(value)));
                }
            }
            if (GUILayout.Button("Play Candidate")) PreviewValue(record, _draft);
            EditorGUILayout.LabelField("Previewing does not apply or save changes.", EditorStyles.miniLabel);
            if (record.InheritedOrigin != null)
            {
                EditorGUILayout.HelpBox("Inherited from " + record.InheritedOrigin.AssetPath + ". Select the editing scope explicitly.", MessageType.Warning);
                _inheritedMode = (EditScope)EditorGUILayout.Popup("Edit Scope", (int)_inheritedMode, _scopeLabels);
            }
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || record.InheritedOrigin != null && _inheritedMode == EditScope.UNSELECTED))
                if (GUILayout.Button("Apply to Source")) Apply(record);
        }

        private void Apply(AudioReferenceRecord record)
        {
            try
            {
                if (record.IsCatalog && AudioReferenceAdapters.BackendCueExists != null && !AudioReferenceAdapters.BackendCueExists(_draft.Text)) throw new InvalidOperationException("The binding does not match an imported event.");
                bool origin = _inheritedMode == EditScope.SHARED_SOURCE;
                string path = origin && record.InheritedOrigin != null ? record.InheritedOrigin.AssetPath : record.Location.AssetPath;
                bool closedScene = path.EndsWith(".unity", StringComparison.Ordinal) && !SceneManager.GetSceneByPath(path).isLoaded;
                if (closedScene && !EditorUtility.DisplayDialog("Edit Closed Scene", "Only " + path + " will be opened and saved. Other scenes will not be saved. Undo will not persist after unloading this scene.", "Apply and Save", "Cancel")) return;
                _preview?.Stop();
                _message = AudioReferenceWriter.Apply(record, _draft, origin, closedScene, record.IsCatalog ? _draftName : null);
                BeginIndex();
            }
            catch (Exception e) { _message = e.Message; }
        }
    }

    public sealed class AudioCuePicker : EditorWindow
    {
        private const float PREVIEW_BUTTON_WIDTH = 55f;
        private const float SELECT_BUTTON_WIDTH = 60f;
        private static readonly Vector2 _minimumWindowSize = new Vector2(560f, 380f);
        private IReadOnlyList<string> _values;
        private Action<string> _select;
        private Action<AudioClip> _selectClip;
        private bool _clips;
        private Func<string, AudioBackendReference> _resolve;
        private AudioPreviewSession _preview;
        private string _search = "";
        private string _previewValue;
        private Vector2 _scroll;

        private void OnEnable()
        {
            _preview = new AudioPreviewSession();
            _preview.Changed += Repaint;
        }

        private void OnDisable()
        {
            if (_preview != null) { _preview.Changed -= Repaint; _preview.Dispose(); _preview = null; }
        }

        public static void Show(IReadOnlyList<string> values, Action<string> select, Func<string, AudioBackendReference> resolve = null)
        {
            var window = CreateInstance<AudioCuePicker>();
            window._values = values;
            window._select = select;
            window._resolve = resolve;
            window.titleContent = new GUIContent("Choose Audio");
            window.ShowUtility();
            window.minSize = _minimumWindowSize;
        }

        public static void ShowClips(Action<AudioClip> select)
        {
            var window = CreateInstance<AudioCuePicker>();
            window._clips = true;
            window._selectClip = select;
            window._values = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToArray();
            window.titleContent = new GUIContent("Choose Audio");
            window.ShowUtility();
            window.minSize = _minimumWindowSize;
        }

        private void OnGUI()
        {
            if ((_select == null && _selectClip == null) || _values == null) { Close(); return; }
            _search = EditorGUILayout.TextField("Search", _search);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Unassigned")) Choose("");
                using (new EditorGUI.DisabledScope(_preview == null || !_preview.IsPlaying))
                    if (GUILayout.Button("Stop Preview")) _preview.Stop();
            }
            if (_preview != null && _preview.IsPlaying) EditorGUILayout.LabelField("Previewing", _preview.Label, EditorStyles.miniLabel);
            if (_preview != null && !string.IsNullOrEmpty(_preview.Error)) EditorGUILayout.HelpBox(_preview.Error, MessageType.Warning);
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (string value in _values)
            {
                if (string.IsNullOrEmpty(value) || value.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(value, GUILayout.ExpandWidth(true));
                    var binding = _resolve != null ? _resolve(value) : new AudioBackendReference(null, value);
                    bool playing = _preview != null && _preview.IsPlaying && _previewValue == value;
                    if (GUILayout.Button(playing ? "Stop" : "Play", GUILayout.Width(PREVIEW_BUTTON_WIDTH)))
                    {
                        if (playing) _preview.Stop();
                        else
                        {
                            _previewValue = value;
                            if (_clips) _preview.PlayClip(AssetDatabase.LoadAssetAtPath<AudioClip>(value));
                            else _preview.PlayResource(binding);
                        }
                    }
                    if (GUILayout.Button("Select", GUILayout.Width(SELECT_BUTTON_WIDTH))) Choose(value);
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.HelpBox("Play previews audio without selecting or applying it. A new preview stops the previous one. Close this window to stop preview playback.", MessageType.None);
        }

        private void Choose(string value)
        {
            if (_clips) _selectClip(string.IsNullOrEmpty(value) ? null : AssetDatabase.LoadAssetAtPath<AudioClip>(value));
            else _select(value);
            Close();
            GUIUtility.ExitGUI();
        }
    }
}
