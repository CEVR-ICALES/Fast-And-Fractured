using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured.Multiplayer
{
    /// <summary>"[link icon] bars 45 ms" widget for the room browser, room slots and match HUD, built in code.</summary>
    public sealed class NetworkSignalIndicator : MonoBehaviour
    {
        private static readonly Color Empty = new Color(1f, 1f, 1f, 0.22f);
        private const int BAR_COUNT = 4;
        private const float ICON_SPACING_RATIO = 0.25f;
        private const float BAR_SPACING_RATIO = 0.08f;
        private const float BAR_WIDTH_RATIO = 0.18f;
        private const float MIN_BAR_WIDTH = 2f;
        private const float MIN_BAR_HEIGHT_RATIO = 0.3f;
        private const float FONT_SIZE_RATIO = 0.85f;
        private static readonly Color[] BarColors =
        {
            new Color(0.92f, 0.30f, 0.26f),   // 1 bar
            new Color(0.96f, 0.62f, 0.20f),   // 2
            new Color(0.80f, 0.86f, 0.28f),   // 3
            new Color(0.36f, 0.86f, 0.42f),   // 4
        };

        private static Sprite _wifiSprite, _cableSprite, _unknownSprite;

        private Image _icon;
        private readonly Image[] _bars = new Image[BAR_COUNT];
        private TMP_Text _label;

        public static NetworkSignalIndicator Create(Transform parent, TMP_Text styleSource, float height)
        {
            var root = new GameObject("NetworkSignal", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var layout = root.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.spacing = height * ICON_SPACING_RATIO;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var fitter = root.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var indicator = root.AddComponent<NetworkSignalIndicator>();
            indicator._icon = NewImage(root.transform, "Link", height, height);

            var barsRoot = new GameObject("Bars", typeof(RectTransform));
            barsRoot.transform.SetParent(root.transform, false);
            var barsLayout = barsRoot.AddComponent<HorizontalLayoutGroup>();
            barsLayout.childAlignment = TextAnchor.LowerLeft;
            barsLayout.spacing = Mathf.Max(1f, height * BAR_SPACING_RATIO);
            barsLayout.childControlWidth = barsLayout.childControlHeight = true;
            barsLayout.childForceExpandWidth = barsLayout.childForceExpandHeight = false;
            var barsElement = barsRoot.AddComponent<LayoutElement>();
            barsElement.preferredHeight = height;
            for (int i = 0; i < BAR_COUNT; i++)
                indicator._bars[i] = NewImage(barsRoot.transform, "Bar" + i, Mathf.Max(MIN_BAR_WIDTH, height * BAR_WIDTH_RATIO),
                    height * (MIN_BAR_HEIGHT_RATIO + (1f - MIN_BAR_HEIGHT_RATIO) * (i + 1) / BAR_COUNT));

            var labelGo = new GameObject("Ping", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            if (styleSource != null)
            {
                label.font = styleSource.font;
                label.fontSharedMaterial = styleSource.fontSharedMaterial;
            }
            label.fontSize = height * FONT_SIZE_RATIO;
            label.enableWordWrapping = false;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            indicator._label = label;

            indicator.Set(LinkKind.Unknown, -1);
            return indicator;
        }

        /// <summary>Shows a link and a round trip. <paramref name="pingMs"/> &lt; 0 = not measured yet.</summary>
        public void Set(LinkKind link, int pingMs, string overrideText = null)
        {
            _icon.sprite = link == LinkKind.Wifi ? WifiSprite : link == LinkKind.Ethernet ? CableSprite : UnknownSprite;
            _icon.color = link == LinkKind.Unknown ? Empty : Color.white;

            int bars = NetworkLink.Bars(pingMs);
            for (int i = 0; i < _bars.Length; i++)
                _bars[i].color = i < bars ? BarColors[bars - 1] : Empty;

            _label.text = overrideText ?? (pingMs < 0 ? "-- ms" : pingMs + " ms");
        }

        private static Image NewImage(Transform parent, string name, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = width;
            element.preferredHeight = element.minHeight = height;
            return image;
        }

        // Icons, drawn once (4x supersampled)
        private const int SIZE = 32;

        private static Sprite WifiSprite => _wifiSprite != null ? _wifiSprite : _wifiSprite = Draw("Wifi", WifiShape);
        private static Sprite CableSprite => _cableSprite != null ? _cableSprite : _cableSprite = Draw("Cable", CableShape);
        private static Sprite UnknownSprite => _unknownSprite != null ? _unknownSprite : _unknownSprite = Draw("Unknown", UnknownShape);

        private static Sprite Draw(string name, System.Func<float, float, bool> inside)
        {
            var texture = new Texture2D(SIZE, SIZE, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[SIZE * SIZE];
            for (int y = 0; y < SIZE; y++)
                for (int x = 0; x < SIZE; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < 4; sy++)
                        for (int sx = 0; sx < 4; sx++)
                            if (inside(x + (sx + 0.5f) / 4f, y + (sy + 0.5f) / 4f)) hits++;
                    pixels[y * SIZE + x] = new Color32(255, 255, 255, (byte)(hits * 255 / 16));
                }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, SIZE, SIZE), new Vector2(0.5f, 0.5f), 100f);
        }

        // Three arcs over a dot, opening upwards.
        private static bool WifiShape(float x, float y)
        {
            float dx = x - 16f, dy = y - 5f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 3.2f) return true;
            float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (angle < 42f || angle > 138f) return false;
            return (d > 8f && d < 11.5f) || (d > 15f && d < 18.5f) || (d > 22f && d < 25.5f);
        }

        // An RJ45 plug seen from the front: a body with the latch tab underneath and gold pins.
        private static bool CableShape(float x, float y)
        {
            bool body = x > 5f && x < 27f && y > 9f && y < 27f;
            bool tab = x > 11f && x < 21f && y > 4f && y <= 9f;
            bool pinSlot = y > 20f && y < 25f && x > 8f && x < 24f && ((int)(x - 8f) % 4) < 2;
            return (body && !pinSlot) || tab;
        }

        // A hollow circle: link not known yet.
        private static bool UnknownShape(float x, float y)
        {
            float dx = x - 16f, dy = y - 16f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return d > 8f && d < 11f;
        }
    }
}
