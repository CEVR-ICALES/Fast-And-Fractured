using System.Collections.Generic;
using Assets.SimpleLocalization.Scripts;
using DG.Tweening;
using FastAndFractured.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    /// <summary>
    /// The map pick before a match, the multiplayer take on singleplayer's random map button
    /// (MapSelection.RandomMap, which hops the highlight from map to map until it stops).
    ///
    /// A strip with the candidate maps and a highlight that hops between them, fast at first and
    /// slowing down, while the big picture shows the map under it; it stops on the chosen one.
    /// - Votes cast: the candidates are the voted maps (with their votes) and the highlight goes
    ///   round them in order, like a wheel.
    /// - Nobody voted: every map is a candidate and the highlight jumps between them at random.
    ///
    /// The server decides the whole sequence (NetworkedLobbyManager.BuildMapPickSequence) and
    /// sends it, so every peer shows the same hops; it waits <see cref="DurationFor"/> before
    /// loading the map. The panel is built in code over the overlay the Lobby Setup Wizard
    /// places in the room screen.
    /// </summary>
    public sealed class MapVoteAnimationUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _root;            // panel that gets toggled on
        [SerializeField] private CanvasGroup _canvasGroup;    // for fade in/out

        [Header("Wizard-built parts (the big picture and its name are reused)")]
        [SerializeField] private Image      _mapImage;
        [SerializeField] private TMP_Text   _mapNameLabel;
        [SerializeField] private TMP_Text   _winnerLabel;     // replaced by the title

        // Timing - shared with the server through DurationFor, keep it deterministic.
        private const float FADE_SECONDS = 0.35f;
        private const float FIRST_HOP = 0.06f;
        private const float LAST_HOP = 0.45f;
        private const float REVEAL_HOLD = 1.8f;

        private const float CARD_WIDTH = 300f;
        private const float CARD_HEIGHT = 200f;

        private Sequence _activeSeq;
        private TMP_Text _title;
        private RectTransform _strip;
        private readonly List<Card> _cards = new List<Card>();

        private sealed class Card
        {
            public RectTransform Rect;
            public Image Frame;
            public Outline Glow;
        }

        private void Awake()
        {
            if (_canvasGroup == null && _root != null) _canvasGroup = _root.GetComponent<CanvasGroup>();
            // The overlay starts inactive and this component sits on it, so Awake only runs when
            // Play activates it - hiding it here used to switch the animation off on its first
            // (and only) run.
            if (_root != null && _root != gameObject) _root.SetActive(false);
        }

        /// <summary>Hop delay before hop <paramref name="index"/> of <paramref name="hops"/>: fast to slow.</summary>
        public static float HopInterval(int index, int hops)
        {
            float t = hops <= 1 ? 1f : Mathf.Clamp01((float)index / (hops - 1));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            return Mathf.Lerp(FIRST_HOP, LAST_HOP, eased);
        }

        /// <summary>Seconds from the start of the animation to the end of the reveal.</summary>
        public static float DurationFor(int hops)
        {
            float total = FADE_SECONDS + REVEAL_HOLD;
            for (int i = 0; i < hops; i++) total += HopInterval(i, hops);
            return total;
        }

        /// <summary>
        /// Plays the pick. <paramref name="candidates"/>: map ids on the strip;
        /// <paramref name="votes"/>: votes of each candidate (null when nobody voted);
        /// <paramref name="hops"/>: index into candidates of every hop, the last one being the
        /// chosen map.
        /// </summary>
        public void Play(int[] candidates, int[] votes, int[] hops, bool random)
        {
            var catalog = MapCatalogLoader.Get();
            if (catalog == null || candidates == null || candidates.Length == 0 || hops == null || hops.Length == 0)
            {
                Debug.LogWarning("[MapVoteAnimationUI] Nothing to show - skipping the animation.");
                return;
            }
            var entries = new MapCatalog.Entry[candidates.Length];
            for (int i = 0; i < candidates.Length; i++) catalog.TryGetById(candidates[i], out entries[i]);

            _activeSeq?.Kill();
            if (_root != null) _root.SetActive(true);
            if (_winnerLabel != null) _winnerLabel.gameObject.SetActive(false);
            EnsureLayout();
            BuildStrip(entries, votes);
            MenuUiKit.Localized(_title, random ? "MP.MapVote.Random" : "MP.MapVote.Spin");
            _title.color = Color.white;
            _title.transform.localScale = Vector3.one;

            var seq = DOTween.Sequence().SetUpdate(true);
            _activeSeq = seq;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                seq.Append(_canvasGroup.DOFade(1f, FADE_SECONDS));
            }
            else
            {
                seq.AppendInterval(FADE_SECONDS);
            }

            for (int k = 0; k < hops.Length; k++)
            {
                int index = Mathf.Clamp(hops[k], 0, entries.Length - 1);
                seq.AppendCallback(() => Highlight(index, entries[index], final: false));
                seq.AppendInterval(HopInterval(k, hops.Length));
            }

            int winner = Mathf.Clamp(hops[hops.Length - 1], 0, entries.Length - 1);
            seq.AppendCallback(() =>
            {
                Highlight(winner, entries[winner], final: true);
                MenuUiKit.Localized(_title, "MP.MapVote.Winner");
                _title.color = MenuUiKit.Accent;
                _title.transform.localScale = Vector3.one * 0.6f;
                _title.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack).SetUpdate(true);
            });
            seq.AppendInterval(REVEAL_HOLD);
            // No fade out: the loading curtain takes over from here.
            seq.AppendCallback(() => _activeSeq = null);
        }

        private void Highlight(int index, MapCatalog.Entry entry, bool final)
        {
            for (int i = 0; i < _cards.Count; i++)
            {
                bool on = i == index;
                var card = _cards[i];
                card.Frame.color = on ? MenuUiKit.Accent : MenuUiKit.ButtonDim;
                card.Glow.enabled = on;
                card.Rect.DOKill();
                card.Rect.DOScale(on ? (final ? 1.18f : 1.1f) : 1f, on ? 0.08f : 0.12f).SetUpdate(true);
            }
            if (_mapImage != null)
            {
                _mapImage.sprite = entry.Thumbnail;
                _mapImage.enabled = entry.Thumbnail != null;
                _mapImage.rectTransform.DOKill();
                _mapImage.rectTransform.localScale = Vector3.one * (final ? 0.92f : 0.97f);
                _mapImage.rectTransform.DOScale(1f, final ? 0.4f : 0.1f).SetEase(final ? Ease.OutBack : Ease.OutQuad).SetUpdate(true);
            }
            if (_mapNameLabel != null)
            {
                _mapNameLabel.text = MapCatalog.DisplayName(entry);
                _mapNameLabel.color = final ? MenuUiKit.Accent : Color.white;
            }
        }

        // ── Layout ───────────────────────────────────────────────────────────────────────
        private void EnsureLayout()
        {
            if (_title != null) return;
            Transform root = _root != null ? _root.transform : transform;
            TMP_FontAsset font = MenuUiKit.FontNear(root.parent != null ? root.parent : root);

            _title = MenuUiKit.Text(root, string.Empty, 64f, Color.white, TextAlignmentOptions.Center, font);
            _title.name = "Title";
            _title.fontStyle = FontStyles.Bold;
            Anchor(_title.rectTransform, 0.05f, 0.84f, 0.95f, 0.96f);

            if (_mapImage != null)
            {
                _mapImage.preserveAspect = true;
                Anchor(_mapImage.rectTransform, 0.28f, 0.40f, 0.72f, 0.82f);
            }
            if (_mapNameLabel != null)
            {
                if (font != null) _mapNameLabel.font = font;
                _mapNameLabel.fontSize = 46f;
                Anchor(_mapNameLabel.rectTransform, 0.10f, 0.32f, 0.90f, 0.40f);
            }

            _strip = MenuUiKit.Rect("Candidates", root);
            Anchor(_strip, 0.02f, 0.06f, 0.98f, 0.30f);
            var row = _strip.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 36f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = row.childControlHeight = false;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
        }

        private void BuildStrip(MapCatalog.Entry[] entries, int[] votes)
        {
            MenuUiKit.Clear(_strip);
            _cards.Clear();
            TMP_FontAsset font = _mapNameLabel != null ? _mapNameLabel.font : null;
            for (int i = 0; i < entries.Length; i++)
            {
                var rect = MenuUiKit.Rect("Map " + i, _strip);
                rect.sizeDelta = new Vector2(CARD_WIDTH, CARD_HEIGHT);
                var frame = MenuUiKit.Background(rect, MenuUiKit.ButtonDim);
                var glow = rect.gameObject.AddComponent<Outline>();
                glow.effectColor = MenuUiKit.Accent;
                glow.effectDistance = new Vector2(5f, -5f);
                glow.enabled = false;

                var picture = MenuUiKit.Rect("Picture", rect);
                MenuUiKit.Stretch(picture, 8f, 8f, 8f, 48f);
                var image = picture.gameObject.AddComponent<Image>();
                image.sprite = entries[i].Thumbnail;
                image.enabled = entries[i].Thumbnail != null;
                image.preserveAspect = true;
                image.raycastTarget = false;

                string caption = MapCatalog.DisplayName(entries[i]);
                if (votes != null && i < votes.Length)
                    caption += "  <color=#FFC85A>" + LocalizationManager.Localize("MP.MapVote.Votes", votes[i]) + "</color>";
                var label = MenuUiKit.Text(rect, caption, 26f, Color.white, TextAlignmentOptions.Center, font);
                label.richText = true;
                label.enableWordWrapping = false;
                label.overflowMode = TextOverflowModes.Ellipsis;
                var labelRect = label.rectTransform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(-12f, 44f);
                labelRect.anchoredPosition = new Vector2(0f, 4f);

                _cards.Add(new Card { Rect = rect, Frame = frame, Glow = glow });
            }
        }

        private static void Anchor(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        /// <summary>Hides the panel (back in the room after the match).</summary>
        public void Hide()
        {
            _activeSeq?.Kill();
            _activeSeq = null;
            if (_root != null && _root != gameObject) _root.SetActive(false);
            else gameObject.SetActive(false);
        }

        private void OnDestroy() => _activeSeq?.Kill();
    }
}
