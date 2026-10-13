using System.Collections.Generic;
using Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Progression
{
    /// <summary>
    /// ACCESSORIES screen: per character, what is equipped in each slot (head, face, car), cycled
    /// through the accessories unlocked for that character. The choice is saved in the profile
    /// and AccessoryApplier puts it on every model of that character.
    ///
    /// For now a list: the 3D presentation is still to decide (zooming on the characters at the
    /// bar, or the other side of the character selector building with the characters posed as in
    /// the trailer). The test accessories have no model, so equipping shows nothing yet.
    /// Opened from the singleplayer modes, the multiplayer hub and Progress; Back returns to where it came from.
    /// </summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class AccessoriesScreen : MonoBehaviour
    {
        /// <summary>Screen the Back button returns to (set by whoever opens this one).</summary>
        public static ScreensType ReturnTo = ScreensType.PROGRESSION;

        private static readonly string[] Characters = { "Pepe", "Josefino", "MariaAntonia", "Carme" };
        private static readonly AccessorySlot[] Slots = { AccessorySlot.Head, AccessorySlot.Face, AccessorySlot.Car };

        private TMP_FontAsset _font;
        private string _character = Characters[0];
        private readonly List<Button> _tabs = new List<Button>();
        private RectTransform _slots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => ReturnTo = ScreensType.PROGRESSION;

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
            var root = MenuUiKit.Stretch(MenuUiKit.Rect("Accessories", transform), 140f, 70f, 140f, 60f);
            MenuUiKit.Background(root, MenuUiKit.Panel);
            var column = MenuUiKit.Stretch(MenuUiKit.Column(root, "Column", 18f, new RectOffset(40, 40, 30, 30)));

            var title = MenuUiKit.Text(column, "ACCESSORIES", 56f, Color.white, TextAlignmentOptions.Center, _font);
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            var tabs = MenuUiKit.RowOf(column, "Characters", 76f, 14f);
            tabs.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var character in Characters)
            {
                string c = character;
                _tabs.Add(MenuUiKit.Button(tabs, MatchResults.CharacterName(c + "_0"), () => { _character = c; Refresh(); }, _font, 300f, 68f));
            }

            _slots = MenuUiKit.Column(column, "Slots", 10f);
            _slots.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var note = MenuUiKit.Text(column, "What you equip shows on every model of the character. Test items: no model yet.",
                22f, MenuUiKit.Muted, TextAlignmentOptions.Center, _font);
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            var buttons = MenuUiKit.RowOf(column, "Buttons", 80f, 24f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var back = MenuUiKit.Button(buttons, "BACK", () =>
            {
                if (MainMenuManager.HasInstance) MainMenuManager.Instance.TransitionBetweenScreens(ReturnTo, 0.25f);
            }, _font, 300f, 72f, MenuUiKit.ButtonDim);

            var screen = GetComponent<MenuScreen>();
            screen.backButton = back;
            screen.defaultInteractable = _tabs[0];
        }

        private void Refresh()
        {
            if (_slots == null) return;
            for (int i = 0; i < _tabs.Count; i++)
                _tabs[i].targetGraphic.color = Characters[i] == _character ? MenuUiKit.ButtonBlue : MenuUiKit.ButtonDim;

            MenuUiKit.Clear(_slots);
            var catalog = ProgressionRewards.Load();
            foreach (var slot in Slots)
            {
                var options = new List<ProgressionReward> { null };   // null = nothing
                foreach (var reward in catalog.rewards)
                    if (reward != null && reward.type != RewardType.Title && reward.slot == slot &&
                        reward.FitsCharacter(_character) && PlayerProgression.IsUnlocked(reward.id))
                        options.Add(reward);

                var current = catalog.Find(PlayerProgression.Equipped(_character, slot));
                int index = Mathf.Max(0, options.IndexOf(current));

                var row = MenuUiKit.RowOf(_slots, slot.ToString(), 84f, 20f);
                MenuUiKit.Background(row, MenuUiKit.Row);
                MenuUiKit.Fixed(MenuUiKit.Text(row, slot.ToString().ToUpperInvariant(), 30f, MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font), 160f);
                AccessorySlot s = slot;
                MenuUiKit.Button(row, "<", () => Cycle(s, options, index - 1), _font, 72f, 64f, MenuUiKit.ButtonDim);
                string name = options[index] != null ? options[index].displayName : "None";
                MenuUiKit.Flexible(MenuUiKit.Text(row, name, 30f, options[index] != null ? MenuUiKit.Accent : Color.white, TextAlignmentOptions.Center, _font));
                MenuUiKit.Button(row, ">", () => Cycle(s, options, index + 1), _font, 72f, 64f, MenuUiKit.ButtonDim);
                MenuUiKit.Fixed(MenuUiKit.Text(row, options.Count > 1 ? $"{options.Count - 1} unlocked" : "none unlocked yet",
                    22f, MenuUiKit.Muted, TextAlignmentOptions.MidlineRight, _font), 220f);
            }
        }

        private void Cycle(AccessorySlot slot, List<ProgressionReward> options, int index)
        {
            if (options.Count == 0) return;
            index = ((index % options.Count) + options.Count) % options.Count;
            PlayerProgression.Equip(_character, slot, options[index]?.id);
        }
    }
}
