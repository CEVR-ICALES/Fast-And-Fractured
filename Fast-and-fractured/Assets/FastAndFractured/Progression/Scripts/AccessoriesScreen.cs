using Assets.SimpleLocalization.Scripts;
using System.Collections.Generic;
using Enums;
using FastAndFractured.Multiplayer;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Progression
{
    /// <summary>
    /// ACCESSORIES screen: per character, the accessory equipped in each slot, cycled through the unlocked ones and
    /// saved in the profile. Back returns to the screen that opened it.
    /// </summary>
    [RequireComponent(typeof(MenuScreen))]
    public sealed class AccessoriesScreen : MonoBehaviour
    {
        private const float SCREEN_TRANSITION_SECONDS = 0.25f;
        private const float WIDE_BUTTON_WIDTH = 300f;
        private const float TAB_BUTTON_HEIGHT = 68f;
        private const float BACK_BUTTON_HEIGHT = 72f;
        private const float CYCLE_BUTTON_WIDTH = 72f;
        private const float CYCLE_BUTTON_HEIGHT = 64f;
        private const float SLOT_FONT_SIZE = 30f;
        private const float NOTE_FONT_SIZE = 22f;

        public static ScreensType ReturnTo = ScreensType.PROGRESSION;

        private const string CHARACTERS_RESOURCE = "CharactersSkins/ListOfCharactersData";
        private static List<string> _characters;
        private static List<string> Characters => _characters ??= LoadCharacters();
        private static readonly AccessorySlot[] Slots = { AccessorySlot.Head, AccessorySlot.Face, AccessorySlot.Car };

        private TMP_FontAsset _font;
        private string _character;
        private readonly List<Button> _tabs = new List<Button>();
        private RectTransform _slots;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ReturnTo = ScreensType.PROGRESSION;
            _characters = null;
        }

        private static List<string> LoadCharacters()
        {
            var names = new List<string>();
            var roster = Resources.Load<ListOfCharactersData>(CHARACTERS_RESOURCE);
            if (roster == null || roster.listOfCharactersData == null) return names;
            foreach (var data in roster.listOfCharactersData)
                if (data != null && !string.IsNullOrEmpty(data.CharacterName) && !names.Contains(data.CharacterName))
                    names.Add(data.CharacterName);
            return names;
        }

        private void Awake()
        {
            _font = MenuUiKit.FontNear(transform.root);
            _character = Characters.Count > 0 ? Characters[0] : string.Empty;
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

            var title = MenuUiKit.Localized(MenuUiKit.Text(column, "", 56f, Color.white, TextAlignmentOptions.Center, _font), "MP.Hub.Accessories");
            title.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;

            var tabs = MenuUiKit.RowOf(column, "Characters", 76f, 14f);
            tabs.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            foreach (var character in Characters)
            {
                string c = character;
                _tabs.Add(MenuUiKit.Button(tabs, MatchResults.CharacterName(c + "_0"), () => { _character = c; Refresh(); }, _font, WIDE_BUTTON_WIDTH, TAB_BUTTON_HEIGHT));
            }

            _slots = MenuUiKit.Column(column, "Slots", 10f);
            _slots.gameObject.AddComponent<LayoutElement>().flexibleHeight = 1f;

            var note = MenuUiKit.Text(column, "",
                NOTE_FONT_SIZE, MenuUiKit.Muted, TextAlignmentOptions.Center, _font);
            MenuUiKit.Localized(note, "MP.Progress.AccessoriesNote");
            note.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

            var buttons = MenuUiKit.RowOf(column, "Buttons", 80f, 24f);
            buttons.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
            var back = MenuUiKit.Button(buttons, "", () =>
            {
                if (MainMenuManager.HasInstance) MainMenuManager.Instance.TransitionBetweenScreens(ReturnTo, SCREEN_TRANSITION_SECONDS);
            }, _font, WIDE_BUTTON_WIDTH, BACK_BUTTON_HEIGHT, MenuUiKit.ButtonDim);

            MenuUiKit.Localized(back, "MP.Back");

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
                MenuUiKit.Fixed(MenuUiKit.Text(row, ProgressionScreen.SlotName(slot).ToUpperInvariant(), SLOT_FONT_SIZE, MenuUiKit.Muted, TextAlignmentOptions.MidlineLeft, _font), 160f);
                AccessorySlot s = slot;
                MenuUiKit.Button(row, "<", () => Cycle(s, options, index - 1), _font, CYCLE_BUTTON_WIDTH, CYCLE_BUTTON_HEIGHT, MenuUiKit.ButtonDim);
                string name = options[index] != null ? options[index].displayName : LocalizationManager.Localize("MP.Progress.None");
                MenuUiKit.Flexible(MenuUiKit.Text(row, name, SLOT_FONT_SIZE, options[index] != null ? MenuUiKit.Accent : Color.white, TextAlignmentOptions.Center, _font));
                MenuUiKit.Button(row, ">", () => Cycle(s, options, index + 1), _font, CYCLE_BUTTON_WIDTH, CYCLE_BUTTON_HEIGHT, MenuUiKit.ButtonDim);
                MenuUiKit.Fixed(MenuUiKit.Text(row, options.Count > 1 ? LocalizationManager.Localize("MP.Progress.UnlockedCount", options.Count - 1) : LocalizationManager.Localize("MP.Progress.NoneUnlocked"),
                    NOTE_FONT_SIZE, MenuUiKit.Muted, TextAlignmentOptions.MidlineRight, _font), 220f);
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
