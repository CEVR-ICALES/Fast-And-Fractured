using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>
    /// Multiplayer part of the win/lose screen: your placement ("#2 / 5", who eliminated you)
    /// and the table of the whole match (placement, player, character, kills, damage), as in
    /// other battle royales (playtest feedback 2026-09-30, point 8).
    ///
    /// Built at runtime on the menu's WIN_LOSE screen, only when a multiplayer match just ended
    /// (MatchResults.Latest), so the menu scene is not edited and singleplayer is unchanged.
    /// Reuses that screen's fonts and colours.
    /// </summary>
    public sealed class MultiplayerResultsPanel : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.68f);
        private static readonly Color RowColor = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color LocalRowColor = new Color(0.811f, 0.576f, 0.409f, 0.7f);
        private static readonly Color Accent = new Color(1f, 0.78f, 0.35f, 1f);
        private static readonly Color Muted = new Color(1f, 1f, 1f, 0.6f);

        private const float WIDTH = 760f;
        private const float ROW_HEIGHT = 58f;

        private TMP_FontAsset _displayFont, _bodyFont;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (MatchResults.Latest == null || MatchResults.Latest.Length == 0) return;
            var screen = FindWinLoseScreen(scene);
            if (screen == null || screen.GetComponentInChildren<MultiplayerResultsPanel>(true) != null) return;
            var go = new GameObject("MultiplayerResults", typeof(RectTransform));
            go.transform.SetParent(screen.transform, false);
            go.AddComponent<MultiplayerResultsPanel>().Build(screen.transform, MatchResults.Latest);
            MatchResults.Clear();   // shown once; a later singleplayer game must not bring it back
        }

        private static MenuScreen FindWinLoseScreen(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
                foreach (var screen in root.GetComponentsInChildren<MenuScreen>(true))
                    if (screen.screenType == ScreensType.WIN_LOSE) return screen;
            return null;
        }

        // Visibility over the cutscene's camera: WinLoseScreenCameraFix (singleplayer too).

        // ── Layout ─────────────────────────────────────────────────────────────────────
        private void Build(Transform screen, MatchResultEntry[] entries)
        {
            ResolveFonts(screen);
            MatchResults.TryGetLocal(out var mine);
            bool haveMine = MatchResults.LocalClientId >= 0 && mine.IsHuman;

            var rt = (RectTransform)transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-30f, -30f);
            rt.sizeDelta = new Vector2(WIDTH, 0f);
            var bg = gameObject.AddComponent<Image>();
            bg.color = PanelColor;
            var column = gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(28, 28, 22, 26);
            column.spacing = 6f;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (haveMine) BuildPlacementHeader(mine, entries.Length);
            BuildTableHeader();
            foreach (var entry in entries)
                BuildRow(entry, haveMine && entry.IsHuman && entry.OwnerClientId == MatchResults.LocalClientId);
        }

        private void BuildPlacementHeader(MatchResultEntry mine, int total)
        {
            var row = NewRow(transform, "Placement", 118f, null);
            var place = Text(row, $"#{mine.Placement}", _displayFont, 104f, mine.Placement == 1 ? Accent : Color.white, TextAlignmentOptions.MidlineLeft);
            Fit(place, 190f);
            Text(row, $"<size=80%><color=#FFFFFFB0>of {total} racers</color></size>\n{Headline(mine)}", _bodyFont, 32f, Color.white, TextAlignmentOptions.MidlineLeft);
            BuildXpLine();
        }

        /// <summary>The XP this match gave, with the level bar filling up (Progression).</summary>
        private void BuildXpLine()
        {
            var award = Progression.PlayerProgression.LastAward;
            if (!award.HasValue) return;
            Progression.XpGainBar.Create(transform, award.Value, _bodyFont);
        }

        private static string Headline(MatchResultEntry mine)
        {
            if (mine.Placement == 1) return "Winner!";
            if (!string.IsNullOrEmpty(mine.EliminatedBy)) return $"Eliminated by <color=#FFC759>{mine.EliminatedBy}</color>";
            return mine.Survived ? "Still racing" : "Eliminated";
        }

        private void BuildTableHeader()
        {
            var row = NewRow(transform, "Header", 40f, null);
            Fit(Text(row, "#", _displayFont, 26f, Muted, TextAlignmentOptions.MidlineLeft), 56f);
            Fit(Text(row, "", _bodyFont, 22f, Muted, TextAlignmentOptions.Midline), 52f);
            Text(row, "Player", _displayFont, 26f, Muted, TextAlignmentOptions.MidlineLeft);
            Fit(Text(row, "Kills", _displayFont, 26f, Muted, TextAlignmentOptions.MidlineRight), 90f);
            Fit(Text(row, "Damage", _displayFont, 26f, Muted, TextAlignmentOptions.MidlineRight), 130f);
        }

        private void BuildRow(MatchResultEntry entry, bool isLocal)
        {
            var row = NewRow(transform, $"Row {entry.Placement}", ROW_HEIGHT, isLocal ? LocalRowColor : RowColor);
            Fit(Text(row, entry.Placement.ToString(), _displayFont, 34f, entry.Placement == 1 ? Accent : Color.white, TextAlignmentOptions.MidlineLeft), 56f);
            Fit(Icon(row, entry.CharacterCode, entry.Survived), 52f);

            string sub = entry.Survived
                ? (entry.Placement == 1 ? "Winner" : "Survived")
                : string.IsNullOrEmpty(entry.EliminatedBy) ? "Eliminated" : $"Eliminated by {entry.EliminatedBy}";
            string name = entry.DisplayName + (isLocal ? " <color=#FFC759>(you)</color>" : string.Empty);
            Text(row, $"{name}\n<size=72%><color=#FFFFFFB0>{MatchResults.CharacterName(entry.CharacterCode)} · {sub}</color></size>",
                _bodyFont, 24f, Color.white, TextAlignmentOptions.MidlineLeft);
            Fit(Text(row, entry.Kills.ToString(), _bodyFont, 26f, Color.white, TextAlignmentOptions.MidlineRight), 90f);
            Fit(Text(row, entry.DamageDealt.ToString(), _bodyFont, 26f, Color.white, TextAlignmentOptions.MidlineRight), 130f);
        }

        // ── Pieces ─────────────────────────────────────────────────────────────────────
        private static Transform NewRow(Transform parent, string name, float height, Color? background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            if (background.HasValue) go.AddComponent<Image>().color = background.Value;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 14, 0, 0);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var element = go.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            return go.transform;
        }

        private static TextMeshProUGUI Text(Transform parent, string text, TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = alignment;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            tmp.raycastTarget = false;
            go.AddComponent<LayoutElement>().flexibleWidth = 1f;
            return tmp;
        }

        private static Graphic Icon(Transform parent, string characterCode, bool alive)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            Sprite sprite = null;
            try { if (ResourcesManager.HasInstance) sprite = ResourcesManager.Instance.GetResourcesSprite(characterCode); }
            catch { /* a missing icon must not hide the table */ }
            image.sprite = sprite;
            image.color = sprite == null ? new Color(1f, 1f, 1f, 0f) : alive ? Color.white : new Color(0.55f, 0.55f, 0.55f, 1f);
            return image;
        }

        private static T Fit<T>(T graphic, float width) where T : Graphic
        {
            var element = graphic.GetComponent<LayoutElement>() ?? graphic.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 0f;
            element.minWidth = element.preferredWidth = width;
            return graphic;
        }

        private void ResolveFonts(Transform screen)
        {
            foreach (var text in screen.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (text.font == null) continue;
                if (_displayFont == null && text.font.name.StartsWith("HyperSuperFast")) _displayFont = text.font;
                if (_bodyFont == null && text.font.name.StartsWith("AlfaSlabOne")) _bodyFont = text.font;
            }
            _displayFont ??= _bodyFont;
            _bodyFont ??= _displayFont;
        }
    }
}
