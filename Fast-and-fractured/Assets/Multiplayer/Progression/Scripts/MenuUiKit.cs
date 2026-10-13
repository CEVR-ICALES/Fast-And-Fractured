using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer.Progression
{
    /// <summary>Small builders for the code-built menu screens, in the multiplayer menus' look.</summary>
    internal static class MenuUiKit
    {
        public static readonly Color Panel = new Color(0.07f, 0.09f, 0.15f, 0.92f);
        public static readonly Color Row = new Color(1f, 1f, 1f, 0.05f);
        public static readonly Color ButtonBlue = new Color(0.20f, 0.50f, 0.90f, 1f);
        public static readonly Color ButtonDim = new Color(0.25f, 0.29f, 0.38f, 1f);
        public static readonly Color Accent = new Color(1f, 0.78f, 0.35f, 1f);
        public static readonly Color Good = new Color(0.49f, 0.88f, 0.54f, 1f);
        public static readonly Color Muted = new Color(1f, 1f, 1f, 0.55f);

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Stretch(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
            return rect;
        }

        public static Image Background(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color color,
                                           TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, TMP_FontAsset font = null)
        {
            var rect = Rect("Text", parent);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.richText = true;
            label.raycastTarget = false;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            return label;
        }

        public static Button Button(Transform parent, string text, UnityAction onClick, TMP_FontAsset font = null,
                                    float width = 320f, float height = 72f, Color? color = null)
        {
            var rect = Rect(text, parent);
            rect.sizeDelta = new Vector2(width, height);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            var image = Background(rect, color ?? ButtonBlue);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClick != null) button.onClick.AddListener(onClick);
            var label = Text(rect, text, 28f, Color.white, TextAlignmentOptions.Center, font);
            Stretch((RectTransform)label.transform);
            return button;
        }

        public static RectTransform Column(Transform parent, string name, float spacing, RectOffset padding = null)
        {
            var rect = Rect(name, parent);
            var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding ?? new RectOffset(0, 0, 0, 0);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return rect;
        }

        public static RectTransform RowOf(Transform parent, string name, float height, float spacing = 16f)
        {
            var rect = Rect(name, parent);
            var element = rect.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = height;
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(16, 16, 0, 0);
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return rect;
        }

        public static T Fixed<T>(T component, float width) where T : Component
        {
            var element = component.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.flexibleWidth = 0f;
            return component;
        }

        public static T Flexible<T>(T component) where T : Component
        {
            var element = component.GetComponent<LayoutElement>() ?? component.gameObject.AddComponent<LayoutElement>();
            element.flexibleWidth = 1f;
            return component;
        }

        /// <summary>A vertical scroll list; returns the content to fill.</summary>
        public static RectTransform ScrollList(Transform parent, string name)
        {
            var root = Rect(name, parent);
            Background(root, new Color(0f, 0f, 0f, 0.25f));
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30f;
            var viewport = Stretch(Rect("Viewport", root));
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Column(viewport, "Content", 6f, new RectOffset(8, 8, 8, 8));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.viewport = viewport;
            scroll.content = content;
            return content;
        }

        /// <summary>A display font already used on this screen (keeps the look of the menu it sits in).</summary>
        public static TMP_FontAsset FontNear(Transform root)
        {
            var text = root.GetComponentInChildren<TMP_Text>(true);
            return text != null ? text.font : null;
        }

        public static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Object.Destroy(parent.GetChild(i).gameObject);
        }
    }
}
