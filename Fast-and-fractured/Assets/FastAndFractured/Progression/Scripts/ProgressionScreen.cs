using Assets.SimpleLocalization.Scripts;
using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Progression
{
    /// <summary>PROGRESSION screen (Multiplayer > Progress): level, XP to the next one and the reward track.</summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class ProgressionScreen : MonoBehaviour
    {
        private const float SCREEN_TRANSITION_SECONDS = 0.25f;
        private const float ROW_FONT_SIZE = 26f;
        private const float SMALL_FONT_SIZE = 22f;
        private const float BACK_BUTTON_WIDTH = 300f;
        private const float BACK_BUTTON_HEIGHT = 72f;
        private static readonly Color UNLOCKED_ROW_COLOR = new Color(0.49f, 0.88f, 0.54f, 0.12f);

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

            var title = MenuUiKit.Localized(MenuUiKit.Text(column, "", 56f, Color.white, TextAlignmentOptions.Center, _font), "MP.Hub.Progress");
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            var card = MenuUiKit.RowOf(column, "Level", 120f, 30f);
            MenuUiKit.Background(card, MenuUiKit.Row);
            _level = MenuUiKit.Fixed(MenuUiKit.Text(card, "", 60f, MenuUiKit.Accent, TextAlignmentOptions.MidlineLeft, _font), 300f);
            var barColumn = MenuUiKit.Flexible(MenuUiKit.Column(card, "Bar", 8f, new RectOffset(0, 0, 22, 22)));
            var barBack = MenuUiKit.Rect("BarBack", barColumn);
            barBack.gameObject.AddComponent<LayoutElement>().preferredHeight = 26f;
            MenuUiKit.Background(barBack, new Color(1f, 1f, 1f, 0.12f));
            _bar = MenuUiKit.Background(MenuUiKit.Stretch(MenuUiKit.Rect("Fill", barBack)), MenuUiKit.Good);
            _bar.type = Image.Type.Filled;
            _bar.fillMethod = Image.FillMethod.Horizontal;
            _bar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            _xp = MenuUiKit.Text(barColumn, "", ROW_FONT_SIZE, Color.white, TextAlignmentOptions.MidlineLeft, _font);
            _next = MenuUiKit.Text(barColumn, "", SMALL_FONT_SIZE, MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font);

            var hint = MenuUiKit.Text(column, "",
                SMALL_FONT_SIZE, MenuUiKit.Muted, TextAlignmentOptions.Center, _font);
            MenuUiKit.Localized(hint, "MP.Progress.Hint");
            hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            var trackTitle = MenuUiKit.Localized(MenuUiKit.Text(column, "", 30f, Color.white, TextAlignmentOptions.MidlineLeft, _font), "MP.Progress.Rewards");
            trackTitle.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            _track = MenuUiKit.ScrollList(column, "Track");
            var trackElement = _track.parent.parent.gameObject.AddComponent<LayoutElement>();
            trackElement.flexibleHeight = 1f;
            trackElement.minHeight = 200f;

            var buttons = MenuUiKit.RowOf(column, "Buttons", 80f, 24f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var back = MenuUiKit.Localized(MenuUiKit.Button(buttons, "", () => Go(ScreensType.MULTIPLAYER_HUB), _font, BACK_BUTTON_WIDTH, BACK_BUTTON_HEIGHT, MenuUiKit.ButtonDim), "MP.Back");
            var accessories = MenuUiKit.Button(buttons, "", () =>
            {
                AccessoriesScreen.ReturnTo = ScreensType.PROGRESSION;
                Go(ScreensType.ACCESSORIES);
            }, _font, 360f);

            MenuUiKit.Localized(accessories, "MP.Hub.Accessories");

            var screen = GetComponent<MenuScreen>();
            screen.backButton = back;
            screen.defaultInteractable = accessories;
        }

        private void Refresh()
        {
            if (_level == null) return;
            int level = PlayerProgression.Level;
            _level.text = LocalizationManager.Localize("MP.Progress.Level", level);
            if (PlayerProgression.IsMaxLevel)
            {
                _bar.fillAmount = 1f;
                _xp.text = LocalizationManager.Localize("MP.Progress.XpMax", PlayerProgression.TotalXp);
            }
            else
            {
                _bar.fillAmount = PlayerProgression.XpIntoLevel / (float)PlayerProgression.XpForThisLevel;
                _xp.text = LocalizationManager.Localize("MP.Progress.Xp", PlayerProgression.XpIntoLevel, PlayerProgression.XpForThisLevel);
            }

            ProgressionReward next = null;
            foreach (var reward in ProgressionRewards.Load().rewards)
                if (reward != null && reward.level > level && (next == null || reward.level < next.level)) next = reward;
            _next.text = next != null ? LocalizationManager.Localize("MP.Progress.NextRewardAt", next.level, next.displayName) : LocalizationManager.Localize("MP.Progress.AllUnlocked");

            MenuUiKit.Clear(_track);
            foreach (var reward in ProgressionRewards.Load().rewards)
            {
                if (reward == null) continue;
                bool unlocked = PlayerProgression.IsUnlocked(reward.id);
                var row = MenuUiKit.RowOf(_track, reward.id, 56f);
                MenuUiKit.Background(row, unlocked ? UNLOCKED_ROW_COLOR : MenuUiKit.Row);
                MenuUiKit.Fixed(MenuUiKit.Text(row, LocalizationManager.Localize("MP.Room.Level", reward.level), ROW_FONT_SIZE, unlocked ? MenuUiKit.Good : MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font), 110f);
                MenuUiKit.Flexible(MenuUiKit.Text(row, reward.displayName, ROW_FONT_SIZE, Color.white, TextAlignmentOptions.MidlineLeft, _font));
                string what = reward.type == RewardType.Title ? TypeName(reward.type)
                    : $"{TypeName(reward.type)} · {SlotName(reward.slot)}{(string.IsNullOrEmpty(reward.character) ? "" : " · " + reward.character)}";
                MenuUiKit.Fixed(MenuUiKit.Text(row, what, SMALL_FONT_SIZE, MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font), 360f);
                MenuUiKit.Fixed(MenuUiKit.Text(row, unlocked ? LocalizationManager.Localize("MP.Progress.Unlocked") : LocalizationManager.Localize("MP.Progress.Locked"), SMALL_FONT_SIZE, unlocked ? MenuUiKit.Good : MenuUiKit.Muted, TextAlignmentOptions.MidlineRight, _font), 150f);
            }
        }

        public static string TypeName(RewardType type) => LocalizationManager.Localize("MP.Progress.Type." + type);
        public static string SlotName(AccessorySlot slot) => LocalizationManager.Localize("MP.Progress.Slot." + slot);

        private static void Go(ScreensType screen)
        {
            if (MainMenuManager.HasInstance) MainMenuManager.Instance.TransitionBetweenScreens(screen, SCREEN_TRANSITION_SECONDS);
        }
    }
}
