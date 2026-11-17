using UnityEngine;
using UnityEngine.InputSystem;
using Utilities.Managers.PauseSystem;
using FastAndFractured;

public class MinimapScaler : MonoBehaviour
{
    private const float SCALE_OFFSET = 0.5f;
    private const float MINIMAP_DEFAULT_VALUE = 0.5f;
    private const string MINIMAP_SCALE_PREFS = "Minimap_Scale";
    private const string TOGGLE_MINIMAP_ACTION = "ToggleMinimap";
    Vector2 VECTOR_HALF;

    private float expandedScale = 3f;

    public static MinimapScaler Instance { get; private set; }

    RectTransform rectTransform;
    InputAction toggleMinimapAction;
    LevelController levelController;
    bool inputEnabled;
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

        baseScale = PlayerPrefs.GetFloat(MINIMAP_SCALE_PREFS, MINIMAP_DEFAULT_VALUE) + SCALE_OFFSET;
        VECTOR_HALF = new Vector2(0.5f, 0.5f);
    }

    void OnEnable()
    {
        toggleMinimapAction = InputSystem.actions?.FindAction(TOGGLE_MINIMAP_ACTION);
        if (toggleMinimapAction != null)
        {
            toggleMinimapAction.performed += OnToggleMinimapPerformed;
        }

        levelController = LevelController.Instance;
        if (levelController != null)
        {
            levelController.charactersCustomStart.AddListener(OnCharactersCustomStart);

            if (levelController.HasGameStarted)
            {
                OnCharactersCustomStart();
            }
        }
        else
        {
            OnCharactersCustomStart();
        }
    }

    private void OnCharactersCustomStart()
    {
        inputEnabled = true;
        toggleMinimapAction?.Enable();
    }

    void OnDisable()
    {
        if (toggleMinimapAction != null)
        {
            toggleMinimapAction.performed -= OnToggleMinimapPerformed;
            toggleMinimapAction = null;
        }

        if (levelController != null)
        {
            levelController.charactersCustomStart.RemoveListener(OnCharactersCustomStart);
            levelController = null;
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
        if (!inputEnabled)
        {
            return;
        }

        ToggleExpanded();
    }

    public void SetCurrentScale()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        float currentScale = PlayerPrefs.GetFloat(MINIMAP_SCALE_PREFS, MINIMAP_DEFAULT_VALUE);
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
        rectTransform.anchorMin = VECTOR_HALF;
        rectTransform.anchorMax = VECTOR_HALF;
        rectTransform.pivot = VECTOR_HALF;
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
        SetCurrentScale();
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
        SetCurrentScale();
    }
}
