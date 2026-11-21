using Assets.SimpleLocalization.Scripts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Lobby
{
    public sealed class RoomJoiningOverlay : MonoBehaviour
    {
        public const float TIMEOUT_SECONDS = 15f;
        private const float FONT_SIZE = 34f;
        private static readonly Color BACKDROP_COLOR = new Color(0f, 0f, 0f, 0.55f);
        private static readonly Color TEXT_COLOR = new Color(1f, 0.92f, 0.75f, 1f);

        private TMP_Text _label;
        private SearchingIndicator _indicator;
        private float _shownAt;
        private bool _timedOut;

        public bool IsShown => gameObject.activeSelf;

        public static RoomJoiningOverlay Create(RectTransform area, TMP_Text fontSource)
        {
            var go = new GameObject(nameof(RoomJoiningOverlay), typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(area, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var backdrop = go.AddComponent<Image>();
            backdrop.color = BACKDROP_COLOR;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            var labelRect = (RectTransform)labelGo.transform;
            labelRect.SetParent(rect, false);
            labelRect.anchorMin = labelRect.anchorMax = labelRect.pivot = new Vector2(0.5f, 0.5f);
            labelRect.sizeDelta = new Vector2(900f, 80f);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (fontSource != null) label.font = fontSource.font;
            label.fontSize = FONT_SIZE;
            label.alignment = TextAlignmentOptions.Center;
            label.color = TEXT_COLOR;
            label.raycastTarget = false;

            var overlay = go.AddComponent<RoomJoiningOverlay>();
            overlay._label = label;
            overlay._indicator = SearchingIndicator.Create(label);
            go.SetActive(false);
            return overlay;
        }

        public void Show(bool show)
        {
            if (show == gameObject.activeSelf) return;
            gameObject.SetActive(show);
            if (!show) return;
            transform.SetAsLastSibling();
            _shownAt = Time.unscaledTime;
            _timedOut = false;
            _label.text = LocalizationManager.Localize("MP.Room.Joining");
            _indicator.FollowText(_label);
            _indicator.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_timedOut || Time.unscaledTime - _shownAt < TIMEOUT_SECONDS) return;
            _timedOut = true;
            _label.text = LocalizationManager.Localize("MP.Room.JoinTimedOut");
            _indicator.gameObject.SetActive(false);
        }
    }
}
