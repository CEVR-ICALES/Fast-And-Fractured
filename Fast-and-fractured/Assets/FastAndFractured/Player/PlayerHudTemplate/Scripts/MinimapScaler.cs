using UnityEngine;
using UnityEngine.InputSystem;
using Utilities.Managers.PauseSystem;

public class MinimapScaler : MonoBehaviour
{
    private const float SCALE_OFFSET = 0.5f;
    private const string MINIMAP_SCALE_PREFS = "Minimap_Scale";
    private const string TOGGLE_MINIMAP_ACTION = "ToggleMinimap";

    private float expandedScale = 3f;

    public static MinimapScaler Instance { get; private set; }

    RectTransform rectTransform;
    InputAction toggleMinimapAction;
    bool isExpanded;
    float baseScale = 1f;

    Transform originalParent;
    int originalSiblingIndex;
    Vector2 originalAnchorMin;
    Vector2 originalAnchorMax;
    Vector2 originalPivot;
    Vector2 originalAnchoredPosition;

    public bool IsExpanded => isExpanded;

    void Awake()
    {
        Instance = this;
        rectTransform = GetComponent<RectTransform>();

        originalParent = rectTransform.parent;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        originalAnchorMin = rectTransform.anchorMin;
        originalAnchorMax = rectTransform.anchorMax;
        originalPivot = rectTransform.pivot;
        originalAnchoredPosition = rectTransform.anchoredPosition;

        baseScale = PlayerPrefs.GetFloat(MINIMAP_SCALE_PREFS, 0.5f) + SCALE_OFFSET;
    }

    void OnEnable()
    {
        toggleMinimapAction = InputSystem.actions?.FindAction(TOGGLE_MINIMAP_ACTION);
        if (toggleMinimapAction != null)
        {
            toggleMinimapAction.performed += OnToggleMinimapPerformed;
            toggleMinimapAction.Enable();
        }
    }

    void OnDisable()
    {
        if (toggleMinimapAction != null)
        {
            toggleMinimapAction.performed -= OnToggleMinimapPerformed;
            toggleMinimapAction = null;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void OnToggleMinimapPerformed(InputAction.CallbackContext context)
    {
        ToggleExpanded();
    }

    public void setCurrentScale()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        float currentScale = PlayerPrefs.GetFloat(MINIMAP_SCALE_PREFS, 0.5f);
        baseScale = currentScale + SCALE_OFFSET;

        if (!isExpanded)
        {
            rectTransform.localScale = Vector3.one * baseScale;
        }
    }

    public void ToggleExpanded()
    {
        if (isExpanded)
        {
            Collapse();
        }
        else
        {
            Expand();
        }
    }

    public void Expand()
    {
        if (isExpanded || rectTransform == null)
        {
            return;
        }

        if (PauseManager.Instance != null && PauseManager.Instance.IsGamePaused)
        {
            return;
        }

        Canvas rootCanvas = GetComponentInParent<Canvas>();
        Transform targetParent = rootCanvas != null ? rootCanvas.rootCanvas.transform : rectTransform.root;

        rectTransform.SetParent(targetParent, false);
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.SetAsLastSibling();
        rectTransform.localScale = Vector3.one * expandedScale;

        isExpanded = true;
    }

    public void Collapse()
    {
        if (!isExpanded || rectTransform == null)
        {
            return;
        }

        if (originalParent != null)
        {
            rectTransform.SetParent(originalParent, false);
            rectTransform.SetSiblingIndex(originalSiblingIndex);
        }

        rectTransform.anchorMin = originalAnchorMin;
        rectTransform.anchorMax = originalAnchorMax;
        rectTransform.pivot = originalPivot;
        rectTransform.anchoredPosition = originalAnchoredPosition;

        isExpanded = false;
        setCurrentScale();
    }

    public static bool TryCollapseExpanded()
    {
        MinimapScaler scaler = Instance;
        if (scaler == null)
        {
            scaler = FindFirstObjectByType<MinimapScaler>();
        }

        if (scaler == null || !scaler.isExpanded)
        {
            return false;
        }

        scaler.Collapse();
        return true;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }
        setCurrentScale();
    }
}
