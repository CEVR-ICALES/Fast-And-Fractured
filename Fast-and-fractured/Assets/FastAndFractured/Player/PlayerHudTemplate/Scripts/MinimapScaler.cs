using UnityEngine;

public class MinimapScaler : MonoBehaviour
{
    RectTransform rectTransform;

    private const float SCALE_OFFSET = 0.5f;
    public void setCurrentScale()
    {
        float currentScale = PlayerPrefs.GetFloat("Minimap_Scale", 0.5f);
        rectTransform.localScale = Vector3.one * (currentScale + SCALE_OFFSET);

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        setCurrentScale();
    }
}
