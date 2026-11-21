using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured
{
    [RequireComponent(typeof(Camera))]
    public sealed class MinimapRenderThrottle : MonoBehaviour
    {
        public const float REFRESH_SECONDS = 1f;
        public const float OVERSCAN = 1.6f;
        private const float MARGIN_USE = 0.8f;
        private const float BIND_RETRY_SECONDS = 0.5f;
        private const string PLAYER_ICON_NAME = "MinimapPlayerIcon";

        private Camera _camera;
        private float _authoredSize;
        private RawImage _view;
        private RectTransform _viewRect;
        private Vector2 _viewPivot;
        private Vector2 _viewAuthoredPosition;
        private Vector2 _viewPosition;
        private Vector3 _viewScale;
        private float _pixelsPerMetre;
        private float _marginPixels;
        private Image _worldIcon;
        private Image _hudIcon;
        private Canvas _ownCanvas;
        private Vector3 _renderedPosition;
        private float _renderedYaw;
        private float _renderedAt = float.NegativeInfinity;
        private RenderTexture _authoredTexture;
        private RenderTexture _wideTexture;
        private float _nextBind;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Attach();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Attach();

        private static void Attach()
        {
            foreach (var minimap in FindObjectsByType<MinimapFollowPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (minimap.GetComponent<Camera>() != null && minimap.GetComponent<MinimapRenderThrottle>() == null)
                    minimap.gameObject.AddComponent<MinimapRenderThrottle>();
        }

        private void Awake() => _camera = GetComponent<Camera>();

        // After MinimapFollowPlayer.Update has placed the camera for this frame.
        private void LateUpdate()
        {
            if (_view == null && !TryBind())
            {
                _camera.enabled = true;
                return;
            }
            if (!_view.isActiveAndEnabled)
            {
                _camera.enabled = false;
                _renderedAt = float.NegativeInfinity;
                return;
            }

            Vector3 position = transform.position;
            float yaw = transform.eulerAngles.y;
            Vector2 offset = PictureOffset(_renderedPosition, position, yaw, _pixelsPerMetre);
            bool render = ShouldRender(Time.unscaledTime - _renderedAt, offset.magnitude, _marginPixels);
            _camera.enabled = render;
            if (render)
            {
                _renderedPosition = position;
                _renderedYaw = yaw;
                _renderedAt = Time.unscaledTime;
                offset = Vector2.zero;
            }

            float turn = render ? 0f : PictureTurn(_renderedYaw, yaw);
            if (offset != _shownOffset || !Mathf.Approximately(turn, _shownTurn))
            {
                _shownOffset = offset;
                _shownTurn = turn;
                _viewRect.anchoredPosition = _viewPosition + offset;
                _viewRect.localRotation = Quaternion.Euler(0f, 0f, turn);
            }
            PlaceHudIcon(yaw);
        }

        private Vector2 _shownOffset;
        private float _shownTurn;
        private float _shownIconTurn = float.NaN;

        private bool TryBind()
        {
            if (Time.unscaledTime < _nextBind || _camera.targetTexture == null) return false;
            _nextBind = Time.unscaledTime + BIND_RETRY_SECONDS;

            LevelController level = LevelController.ExistingInstance;
            GameObject player = level != null ? level.LocalPlayer : null;
            if (player == null) return false;
            foreach (var image in player.transform.root.GetComponentsInChildren<RawImage>(true))
            {
                if (image.texture != _camera.targetTexture) continue;
                _view = image;
                break;
            }
            if (_view == null) return false;

            _viewRect = _view.rectTransform;
            _viewPivot = _viewRect.pivot;
            _viewAuthoredPosition = _viewRect.anchoredPosition;
            _viewScale = _viewRect.localScale;
            if (_authoredSize <= 0f) _authoredSize = _camera.orthographicSize;

            _viewPosition = CentredPosition(_viewAuthoredPosition, _viewPivot, _viewRect.rect.size, _viewScale);
            _viewRect.pivot = CENTRE;
            _viewRect.anchoredPosition = _viewPosition;

            _camera.orthographicSize = _authoredSize * OVERSCAN;
            _viewRect.localScale = _viewScale * OVERSCAN;
            float shownWidth = _viewRect.rect.width * _viewScale.x;
            _pixelsPerMetre = shownWidth / (2f * _authoredSize);
            _marginPixels = shownWidth * 0.5f * (OVERSCAN - 1f);
            _renderedAt = float.NegativeInfinity;
            UseWideTexture();
            IsolateFromHud();
            BindIcon();
            return true;
        }

        private void UseWideTexture()
        {
            _authoredTexture = _camera.targetTexture;
            var descriptor = _authoredTexture.descriptor;
            descriptor.width = WideSize(descriptor.width);
            descriptor.height = WideSize(descriptor.height);
            _wideTexture = new RenderTexture(descriptor) { name = _authoredTexture.name + " (wide)", filterMode = _authoredTexture.filterMode };
            _camera.targetTexture = _wideTexture;
            _view.texture = _wideTexture;
        }

        // The picture moves every frame: without its own canvas the whole HUD was rebuilt with it.
        private void IsolateFromHud()
        {
            GameObject holder = _viewRect.parent.gameObject;
            if (holder.GetComponent<Canvas>() == null)
                _ownCanvas = holder.AddComponent<Canvas>();
        }

        // The own icon is part of the picture: sliding the picture would drag it off the centre.
        private void BindIcon()
        {
            var iconField = typeof(MinimapFollowPlayer).GetField("characterIconMinimap",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var icon = iconField != null ? iconField.GetValue(GetComponent<MinimapFollowPlayer>()) as GameObject : null;
            _worldIcon = icon != null ? icon.GetComponent<Image>() : null;
            if (_worldIcon == null) return;

            Transform holder = _viewRect.parent;
            Transform existing = holder.Find(PLAYER_ICON_NAME);
            _hudIcon = existing != null ? existing.GetComponent<Image>() : null;
            if (_hudIcon == null)
            {
                var go = new GameObject(PLAYER_ICON_NAME, typeof(RectTransform), typeof(Image));
                go.layer = _view.gameObject.layer;
                go.transform.SetParent(holder, false);
                _hudIcon = go.GetComponent<Image>();
                _hudIcon.raycastTarget = false;
            }
            _hudIcon.transform.SetSiblingIndex(_viewRect.GetSiblingIndex() + 1);

            RectTransform world = _worldIcon.rectTransform;
            RectTransform hud = _hudIcon.rectTransform;
            hud.anchorMin = hud.anchorMax = hud.pivot = CENTRE;
            hud.localPosition = _viewRect.localPosition;
            hud.sizeDelta = new Vector2(world.rect.width * world.lossyScale.x, world.rect.height * world.lossyScale.y) * _pixelsPerMetre;
            _hudIcon.color = _worldIcon.color;
            _hudIcon.preserveAspect = _worldIcon.preserveAspect;
            _worldIcon.enabled = false;
        }

        private void PlaceHudIcon(float cameraYaw)
        {
            if (_hudIcon == null || _worldIcon == null) return;
            if (_hudIcon.sprite != _worldIcon.sprite) _hudIcon.sprite = _worldIcon.sprite;
            float turn = IconTurn(cameraYaw, _worldIcon.transform.eulerAngles.y);
            if (Mathf.Approximately(turn, _shownIconTurn)) return;
            _shownIconTurn = turn;
            _hudIcon.rectTransform.localRotation = Quaternion.Euler(0f, 0f, turn);
        }

        private void OnDisable()
        {
            if (_camera != null)
            {
                _camera.enabled = true;
                if (_authoredSize > 0f) _camera.orthographicSize = _authoredSize;
                if (_authoredTexture != null) _camera.targetTexture = _authoredTexture;
            }
            if (_view != null && _authoredTexture != null) _view.texture = _authoredTexture;
            if (_wideTexture != null)
            {
                _wideTexture.Release();
                Destroy(_wideTexture);
                _wideTexture = null;
            }
            if (_viewRect != null)
            {
                _viewRect.pivot = _viewPivot;
                _viewRect.anchoredPosition = _viewAuthoredPosition;
                _viewRect.localRotation = Quaternion.identity;
                _viewRect.localScale = _viewScale;
            }
            if (_worldIcon != null) _worldIcon.enabled = true;
            if (_hudIcon != null) Destroy(_hudIcon.gameObject);
            if (_ownCanvas != null) Destroy(_ownCanvas);
            _view = null;
        }

        private static readonly Vector2 CENTRE = new Vector2(0.5f, 0.5f);

        public static Vector2 CentredPosition(Vector2 anchoredPosition, Vector2 pivot, Vector2 size, Vector3 scale) =>
            anchoredPosition + new Vector2((0.5f - pivot.x) * size.x * scale.x, (0.5f - pivot.y) * size.y * scale.y);

        // Between two pictures nothing on the map changes but the pickup icons: a slow refresh covers them.
        public static bool ShouldRender(float secondsSinceRender, float offsetPixels, float marginPixels) =>
            secondsSinceRender >= REFRESH_SECONDS || offsetPixels > marginPixels * MARGIN_USE;

        public static int WideSize(int authoredPixels) => Mathf.CeilToInt(authoredPixels * OVERSCAN);

        public static Vector2 PictureOffset(Vector3 renderedAt, Vector3 cameraNow, float cameraYaw, float pixelsPerMetre)
        {
            float radians = cameraYaw * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians), cos = Mathf.Cos(radians);
            float x = renderedAt.x - cameraNow.x, z = renderedAt.z - cameraNow.z;
            return new Vector2(x * cos - z * sin, x * sin + z * cos) * pixelsPerMetre;
        }

        public static float PictureTurn(float renderedYaw, float cameraYaw) => Mathf.DeltaAngle(renderedYaw, cameraYaw);

        public static float IconTurn(float cameraYaw, float playerYaw) => Mathf.DeltaAngle(playerYaw, cameraYaw);
    }
}
