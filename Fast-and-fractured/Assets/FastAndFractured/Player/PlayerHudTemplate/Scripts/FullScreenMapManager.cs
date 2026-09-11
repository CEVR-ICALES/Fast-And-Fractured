using UnityEngine;
using UnityEngine.UI;
using Utilities;

namespace FastAndFractured
{
    public class FullScreenMapManager : AbstractSingleton<FullScreenMapManager>
    {
        [Header("Map Container")]
        [Tooltip("The 'Map' RectTransform that holds the minimap. Auto-resolved if left null.")]
        [SerializeField] private RectTransform mapContainer;
        [Tooltip("The MinimapFollowPlayer component that drves the minimap camera. Auto-resolved if left null.")]
        [SerializeField] private MinimapFollowPlayer minimapFollowPlayer;

        [Header("Fullscreen Settings")]
        [Tooltip("Fully expanded map area as normalized anchors (0..1). Keeps a border so gameplay stays visible.")]
        [SerializeField] private Vector2 fullscreenAnchorMin = new Vector2(0.08f, 0.1f);
        [SerializeField] private Vector2 fullscreenAnchorMax = new Vector2(0.92f, 0.9f);
        [Tooltip("Orthographic size of the minimap camera when the map is expanded.")]
        [SerializeField] private float fullscreenOrthoSize = 300f;
        [Tooltip("Opacity (0..1) of the map texture when expanded, so gameplay shows through.")]
        [Range(0f, 1f)]
        [SerializeField] private float mapOpenOpacity = 0.5f;
        [Tooltip("Opacity (0..1) of the dark dim layer behind the expanded map.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimOpacity = 0.4f;
        [Tooltip("World position used as the center of the expanded map instead of the player.")]
        [SerializeField] private Vector3 fixedMapCenterPosition = Vector3.zero;

        private struct RectState
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 anchoredPosition;
            public Vector2 sizeDelta;
            public Vector2 pivot;

            public static RectState Capture(RectTransform rt)
            {
                return new RectState
                {
                    anchorMin = rt.anchorMin,
                    anchorMax = rt.anchorMax,
                    anchoredPosition = rt.anchoredPosition,
                    sizeDelta = rt.sizeDelta,
                    pivot = rt.pivot
                };
            }

            public void Restore(RectTransform rt)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.anchoredPosition = anchoredPosition;
                rt.sizeDelta = sizeDelta;
                rt.pivot = pivot;
            }
        }

        private Camera _minimapCamera;
        private RawImage _mapRawImage;
        private Mask _circularMask;
        private RawImage _circularMaskGraphic;
        private Image _dimLayer;
        private GameObject _originalParent;
        private int _originalSiblingIndex;
        private bool _isMapOpen;
        private RectTransform _childMaskRect;
        private RectTransform _childImageRect;
        private RectState _childMaskState;
        private RectState _childImageState;

        private float _minimapOrthoSize;
        private Vector2 _originalAnchoredMin;
        private Vector2 _originalAnchoredMax;
        private Vector2 _originalAnchoredPosition;
        private Vector2 _originalSizeDelta;
        private Vector3 _originalLocalScale = Vector3.one;

        public bool IsMapOpen => _isMapOpen;

        protected override void Construct()
        {
            base.Construct();

            if (minimapFollowPlayer == null)
                minimapFollowPlayer = FindFirstObjectByType<MinimapFollowPlayer>();

            if (minimapFollowPlayer != null)
                _minimapCamera = minimapFollowPlayer.GetComponent<Camera>();

            if (mapContainer == null)
                mapContainer = FindMapContainer();

            if (mapContainer != null)
            {
                _originalParent = mapContainer.parent != null ? mapContainer.parent.gameObject : null;
                _originalSiblingIndex = mapContainer.GetSiblingIndex();
                _originalAnchoredMin = mapContainer.anchorMin;
                _originalAnchoredMax = mapContainer.anchorMax;
                _originalAnchoredPosition = mapContainer.anchoredPosition;
                _originalSizeDelta = mapContainer.sizeDelta;
                _originalLocalScale = mapContainer.localScale;

                ResolveMapChildren();
            }

            if (_minimapCamera != null)
                _minimapOrthoSize = _minimapCamera.orthographicSize;

            CreateDimLayer();
        }

        private RectTransform FindMapContainer()
        {
            Transform mapTransform = null;
            Transform[] allTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (Transform t in allTransforms)
            {
                if (t.name == "Map")
                {
                    Transform mask = t.Find("CircularMask (1)");
                    if (mask == null) mask = t.Find("CircularMask");
                    if (mask != null)
                    {
                        mapTransform = t;
                        break;
                    }
                }
            }
            return mapTransform is RectTransform rt ? rt : null;
        }

        private void ResolveMapChildren()
        {
            _circularMask = null;
            _circularMaskGraphic = null;
            _mapRawImage = null;
            _childMaskRect = null;
            _childImageRect = null;

            Transform maskTransform = mapContainer.Find("CircularMask (1)");
            if (maskTransform == null) maskTransform = mapContainer.Find("CircularMask");
            if (maskTransform != null)
            {
                _circularMask = maskTransform.GetComponent<Mask>();
                _circularMaskGraphic = maskTransform.GetComponent<RawImage>();
                _childMaskRect = maskTransform as RectTransform;
                if (_childMaskRect != null)
                    _childMaskState = RectState.Capture(_childMaskRect);

                Transform rawImageTransform = maskTransform.Find("RawImage_Minimap (1)");
                if (rawImageTransform == null) rawImageTransform = maskTransform.Find("RawImage_Minimap");
                if (rawImageTransform != null)
                {
                    _mapRawImage = rawImageTransform.GetComponent<RawImage>();
                    _childImageRect = rawImageTransform as RectTransform;
                    if (_childImageRect != null)
                        _childImageState = RectState.Capture(_childImageRect);
                }
            }
            else
            {
                _mapRawImage = mapContainer.GetComponentInChildren<RawImage>(true);
            }
        }

        private static void StretchToFill(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        private void CreateDimLayer()
        {
            if (mapContainer == null) return;
            Transform canvasRoot = mapContainer.GetComponentInParent<Canvas>().transform;

            Transform existing = canvasRoot.Find("FullScreenMapDim");
            if (existing != null)
            {
                _dimLayer = existing.GetComponent<Image>();
                return;
            }

            GameObject dimGo = new GameObject("FullScreenMapDim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform dimRect = (RectTransform)dimGo.transform;
            dimRect.SetParent(canvasRoot, false);
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;

            _dimLayer = dimGo.GetComponent<Image>();
            _dimLayer.color = new Color(0f, 0f, 0f, dimOpacity);
            _dimLayer.raycastTarget = false;
            _dimLayer.enabled = false;

            dimRect.SetAsLastSibling();
        }

        public void ToggleMap()
        {
            if (_isMapOpen)
                CloseMap();
            else
                OpenMap();
        }

        public void OpenMap()
        {
            if (_isMapOpen) return;
            if (mapContainer == null)
            {
                Debug.LogError("FullScreenMapManager: mapContainer is null. Assign it in the inspector.");
                return;
            }
            _isMapOpen = true;

            if (PlayerInputController.Instance != null)
                PlayerInputController.Instance.BlockInput(Enums.InputBlockTypes.ALL_MECHANICS);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Transform canvasRoot = mapContainer.GetComponentInParent<Canvas>().transform;

            _originalParent = mapContainer.parent != null ? mapContainer.parent.gameObject : null;
            _originalSiblingIndex = mapContainer.GetSiblingIndex();
            _originalLocalScale = mapContainer.localScale;

            mapContainer.SetParent(canvasRoot, false);
            mapContainer.SetAsLastSibling();

            mapContainer.anchorMin = fullscreenAnchorMin;
            mapContainer.anchorMax = fullscreenAnchorMax;
            mapContainer.offsetMin = Vector2.zero;
            mapContainer.offsetMax = Vector2.zero;
            mapContainer.anchoredPosition = Vector2.zero;
            mapContainer.sizeDelta = Vector2.zero;
            mapContainer.localScale = Vector3.one;

            if (_dimLayer != null)
            {
                Color dim = _dimLayer.color;
                dim.a = dimOpacity;
                _dimLayer.color = dim;
                _dimLayer.enabled = true;
                if (_dimLayer.transform.parent == mapContainer.parent)
                {
                    _dimLayer.transform.SetAsLastSibling();
                    mapContainer.SetAsLastSibling();
                }
            }

            if (_circularMask != null)
                _circularMask.enabled = false;
            if (_circularMaskGraphic != null)
                _circularMaskGraphic.enabled = false;

            StretchToFill(_childMaskRect);
            StretchToFill(_childImageRect);

            if (_mapRawImage != null)
            {
                Color c = _mapRawImage.color;
                c.a = mapOpenOpacity;
                _mapRawImage.color = c;
            }

            if (_minimapCamera != null)
                _minimapCamera.orthographicSize = fullscreenOrthoSize;

            if (minimapFollowPlayer != null)
                minimapFollowPlayer.SetFixedView(fixedMapCenterPosition);
        }

        public void CloseMap()
        {
            if (!_isMapOpen) return;
            _isMapOpen = false;

            if (_dimLayer != null)
                _dimLayer.enabled = false;

            if (_circularMask != null)
                _circularMask.enabled = true;
            if (_circularMaskGraphic != null)
                _circularMaskGraphic.enabled = true;

            if (_childMaskRect != null)
                _childMaskState.Restore(_childMaskRect);
            if (_childImageRect != null)
                _childImageState.Restore(_childImageRect);

            if (_mapRawImage != null)
            {
                Color c = _mapRawImage.color;
                c.a = 1f;
                _mapRawImage.color = c;
            }

            if (_minimapCamera != null)
                _minimapCamera.orthographicSize = _minimapOrthoSize;

            if (minimapFollowPlayer != null)
                minimapFollowPlayer.ResumeFollowing();

            if (_originalParent != null)
            {
                mapContainer.SetParent(_originalParent.transform, false);
                mapContainer.SetSiblingIndex(Mathf.Clamp(_originalSiblingIndex, 0, _originalParent.transform.childCount - 1));
            }

            mapContainer.anchorMin = _originalAnchoredMin;
            mapContainer.anchorMax = _originalAnchoredMax;
            mapContainer.anchoredPosition = _originalAnchoredPosition;
            mapContainer.sizeDelta = _originalSizeDelta;
            mapContainer.localScale = _originalLocalScale;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (PlayerInputController.Instance != null)
                PlayerInputController.Instance.EnableInput(Enums.InputBlockTypes.ALL_MECHANICS);
        }
    }
}
