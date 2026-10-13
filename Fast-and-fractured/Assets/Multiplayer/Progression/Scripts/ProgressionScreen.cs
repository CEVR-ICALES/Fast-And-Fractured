using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Progression
{
    /// <summary>
    /// PROGRESSION screen (Multiplayer ▸ Progress): level, XP towards the next one and the reward
    /// track - what every level unlocks, already earned or still to come - plus the way into the
    /// accessories. XP is earned in multiplayer matches (shown on the results screen).
    ///
    /// The tool (MainMenuPlayModesTool) creates the screen object; the content is built here.
    /// </summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class ProgressionScreen : MonoBehaviour
    {
        private TMP_FontAsset _font;
        private TextMeshProUGUI _level, _xp, _next;
        private Image _bar;
        private RectTransform _track;

        private void Awake()
        {
            _font = MenuUiKit.FontNear(transform.root);
            Build();
        }

        private void OnEnable()
        {
            PlayerProgression.OnChanged += Refresh;
            Refresh();
        }

        private void OnDisable() => PlayerProgression.OnChanged -= Refresh;

        private void Build()
        {
            var root = MenuUiKit.Stretch(MenuUiKit.Rect("Progression", transform), 140f, 70f, 140f, 60f);
            MenuUiKit.Background(root, MenuUiKit.Panel);
            var column = MenuUiKit.Stretch(MenuUiKit.Column(root, "Column", 18f, new RectOffset(40, 40, 30, 30)));

            var title = MenuUiKit.Text(column, "PROGRESS", 56f, Color.white, TextAlignmentOptions.Center, _font);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            // Level card: level, bar, XP numbers, next reward.
            var card = MenuUiKit.RowOf(column, "Level", 120f, 30f);
            MenuUiKit.Background(card, MenuUiKit.Row);
            _level = MenuUiKit.Fixed(MenuUiKit.Text(card, "LEVEL 1", 60f, MenuUiKit.Accent, TextAlignmentOptions.MidlineLeft, _font), 300f);
            var barColumn = MenuUiKit.Flexible(MenuUiKit.Column(card, "Bar", 8f, new RectOffset(0, 0, 22, 22)));
            var barBack = MenuUiKit.Rect("BarBack", barColumn);
            barBack.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            MenuUiKit.Background(barBack, new Color(1f, 1f, 1f, 0.12f));
            _bar = MenuUiKit.Background(MenuUiKit.Stretch(MenuUiKit.Rect("Fill", barBack)), MenuUiKit.Good);
            _bar.type = Image.Type.Filled;
            _bar.fillMethod = Image.FillMethod.Horizontal;
            _bar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            _xp = MenuUiKit.Text(barColumn, "0 / 200 XP", 26f, Color.white, TextAlignmentOptions.MidlineLeft, _font);
            _next = MenuUiKit.Text(barColumn, "", 22f, MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font);

            var hint = MenuUiKit.Text(column, "Play multiplayer matches to earn XP: placement, eliminations and damage all count.",
                22f, MenuUiKit.Muted, TextAlignmentOptions.Center, _font);
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            var trackTitle = MenuUiKit.Text(column, "REWARDS", 30f, Color.white, TextAlignmentOptions.MidlineLeft, _font);
            trackTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _track = MenuUiKit.ScrollList(column, "Track");
            var trackElement = _track.parent.parent.gameObject.AddComponent<LayoutElement>();
            trackElement.flexibleHeight = 1f;
            trackElement.minHeight = 200f;

            var buttons = MenuUiKit.RowOf(column, "Buttons", 80f, 24f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var back = MenuUiKit.Button(buttons, "BACK", () => Go(ScreensType.MULTIPLAYER_HUB), _font, 300f, 72f, MenuUiKit.ButtonDim);
            var accessories = MenuUiKit.Button(buttons, "ACCESSORIES", () =>
            {
                AccessoriesScreen.ReturnTo = ScreensType.PROGRESSION;
                Go(ScreensType.ACCESSORIES);
            }, _font, 360f);

            var screen = GetComponent<MenuScreen>();
            screen.backButton = back;
            screen.defaultInteractable = accessories;
        }

        private void Refresh()
        {
            if (_level == null) return;
            int level = PlayerProgression.Level;
            _level.text = $"LEVEL {level}";
            if (PlayerProgression.IsMaxLevel)
            {
                _bar.fillAmount = 1f;
                _xp.text = $"{PlayerProgression.TotalXp} XP · max level";
            }
            else
            {
                _bar.fillAmount = PlayerProgression.XpIntoLevel / (float)PlayerProgression.XpForThisLevel;
                _xp.text = $"{PlayerProgression.XpIntoLevel} / {PlayerProgression.XpForThisLevel} XP";
            }

            ProgressionReward next = null;
            foreach (var reward in ProgressionRewards.Load().rewards)
                if (reward != null && reward.level > level && (next == null || reward.level < next.level)) next = reward;
            _next.text = next != null ? $"Next reward at level {next.level}: {next.displayName}" : "Every reward unlocked";

            MenuUiKit.Clear(_track);
            foreach (var reward in ProgressionRewards.Load().rewards)
            {
                if (reward == null) continue;
                bool unlocked = PlayerProgression.IsUnlocked(reward.id);
                var row = MenuUiKit.RowOf(_track, reward.id, 56f);
                MenuUiKit.Background(row, unlocked ? new Color(0.49f, 0.88f, 0.54f, 0.12f) : MenuUiKit.Row);
                MenuUiKit.Fixed(MenuUiKit.Text(row, $"Lv {reward.level}", 26f, unlocked ? MenuUiKit.Good : MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font), 110f);
                MenuUiKit.Flexible(MenuUiKit.Text(row, reward.displayName, 26f, Color.white, TextAlignmentOptions.MidlineLeft, _font));
                string what = reward.type == RewardType.Title ? "Title"
                    : $"{reward.type} · {reward.slot}{(string.IsNullOrEmpty(reward.character) ? "" : " · " + reward.character)}";
                MenuUiKit.Fixed(MenuUiKit.Text(row, what, 22f, MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font), 360f);
                MenuUiKit.Fixed(MenuUiKit.Text(row, unlocked ? "UNLOCKED" : "LOCKED", 22f, unlocked ? MenuUiKit.Good : MenuUiKit.Muted, TextAlignmentOptions.MidlineRight, _font), 150f);
            }
        }

        private static void Go(ScreensType screen)
        {
            if (MainMenuManager.HasInstance) MainMenuManager.Instance.TransitionBetweenScreens(screen, 0.25f);
        }
    }
}
