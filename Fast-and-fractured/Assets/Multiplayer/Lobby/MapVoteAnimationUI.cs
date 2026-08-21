using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    /// <summary>
    /// Slot-machine style picker that flashes through every voted map and lands on the resolved
    /// one. Plays the same animation on every peer (host included) when the lobby announces
    /// the map vote resolution. After the animation completes, the server kicks off the FishNet
    /// scene load to the resolved map's networked variant.
    ///
    /// Driven by NetworkedLobbyManager.RpcPlayMapVoteAnimation. UI is hidden by default and
    /// reveals via DOTween fade-in.
    /// </summary>
    public sealed class MapVoteAnimationUI : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _root;            // panel that gets toggled on
        [SerializeField] private CanvasGroup _canvasGroup;    // for fade in/out

        [Header("Slot machine")]
        [SerializeField] private Image      _mapImage;        // big thumbnail in the center
        [SerializeField] private TMP_Text   _mapNameLabel;
        [SerializeField] private TMP_Text   _winnerLabel;     // "WINNER!" reveal

        [Header("Timing (server-side authoritative duration)")]
        [Tooltip("Initial swap interval — fast cycling at the start.")]
        [SerializeField] private float _initialInterval = 0.06f;
        [Tooltip("Final swap interval — slowest cycling just before reveal.")]
        [SerializeField] private float _finalInterval   = 0.45f;
        [Tooltip("How long to hold the final reveal before fading out.")]
        [SerializeField] private float _holdSeconds     = 1.5f;
        [Tooltip("Fade in / fade out duration in seconds.")]
        [SerializeField] private float _fadeSeconds     = 0.4f;

        private Sequence _activeSeq;

        private void Awake()
        {
            if (_canvasGroup == null && _root != null) _canvasGroup = _root.GetComponent<CanvasGroup>();
            if (_root != null) _root.SetActive(false);
            if (_canvasGroup != null) _canvasGroup.alpha = 0f;
        }

        /// <summary>
        /// Plays the slot animation. votedMapIds = the deck to cycle through; resolvedMapId =
        /// the final pick to land on. Returns the total animation duration so the caller can
        /// schedule the scene load to start just after the reveal.
        /// </summary>
        public float Play(int[] votedMapIds, int resolvedMapId)
        {
            var catalog = MapCatalogLoader.Get();
            if (catalog == null)
            {
                Debug.LogWarning("[MapVoteAnimationUI] MapCatalog not available — skipping animation.");
                return 0f;
            }

            // Build a list of map sprites to flash through. If no votes were cast (everyone
            // abstained), fall back to the resolved map repeated so the user at least sees it.
            var deck = new List<MapCatalog.Entry>();
            if (votedMapIds != null)
            {
                foreach (int id in votedMapIds)
                    if (catalog.TryGetById(id, out var entry)) deck.Add(entry);
            }
            if (deck.Count == 0 && catalog.TryGetById(resolvedMapId, out var fallback))
                deck.Add(fallback);

            if (deck.Count == 0)
            {
                Debug.LogWarning("[MapVoteAnimationUI] No maps to animate — skipping.");
                return 0f;
            }

            catalog.TryGetById(resolvedMapId, out var winnerEntry);

            // Kill any previous animation in flight so re-triggers don't stack.
            _activeSeq?.Kill();

            if (_root != null) _root.SetActive(true);
            if (_winnerLabel != null) { _winnerLabel.gameObject.SetActive(false); }

            var seq = DOTween.Sequence();
            _activeSeq = seq;

            // 1. Fade in panel.
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 0f;
                seq.Append(_canvasGroup.DOFade(1f, _fadeSeconds));
            }

            // 2. Slot machine — 20 swaps with ease-out interval (fast → slow).
            const int SWAP_COUNT = 20;
            for (int i = 0; i < SWAP_COUNT; i++)
            {
                // t goes 0 → 1, easeOutCubic for nice decel
                float t = (float)i / Mathf.Max(1, SWAP_COUNT - 1);
                float interval = Mathf.Lerp(_initialInterval, _finalInterval, EaseOutCubic(t));
                var entry = deck[i % deck.Count];
                seq.AppendCallback(() => SetMap(entry));
                seq.AppendInterval(interval);
            }

            // 3. Land on the winner.
            seq.AppendCallback(() =>
            {
                SetMap(winnerEntry);
                if (_winnerLabel != null)
                {
                    _winnerLabel.gameObject.SetActive(true);
                    _winnerLabel.transform.localScale = Vector3.zero;
                    _winnerLabel.transform.DOScale(1f, 0.35f).SetEase(Ease.OutBack);
                }
            });

            // 4. Hold reveal.
            seq.AppendInterval(_holdSeconds);

            // 5. Fade out.
            if (_canvasGroup != null) seq.Append(_canvasGroup.DOFade(0f, _fadeSeconds));
            seq.AppendCallback(() =>
            {
                if (_root != null) _root.SetActive(false);
                _activeSeq = null;
            });

            return EstimateDuration(SWAP_COUNT);
        }

        private float EstimateDuration(int swapCount)
        {
            // Sum of intervals across the swap loop using the same easing.
            float total = _fadeSeconds;
            for (int i = 0; i < swapCount; i++)
            {
                float t = (float)i / Mathf.Max(1, swapCount - 1);
                total += Mathf.Lerp(_initialInterval, _finalInterval, EaseOutCubic(t));
            }
            total += _holdSeconds + _fadeSeconds + 0.35f /* winner bounce */;
            return total;
        }

        private void SetMap(MapCatalog.Entry entry)
        {
            if (_mapImage != null)
            {
                _mapImage.sprite = entry.Thumbnail;
                _mapImage.enabled = entry.Thumbnail != null;
            }
            if (_mapNameLabel != null)
                _mapNameLabel.text = string.IsNullOrEmpty(entry.DisplayNameKey) ? entry.SceneName : entry.DisplayNameKey;
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - Mathf.Pow(1f - t, 3f);
        }

        private void OnDestroy() => _activeSeq?.Kill();
    }
}
