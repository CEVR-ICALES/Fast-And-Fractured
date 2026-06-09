using UnityEngine;
using UnityEngine.UIElements;

public class MinimapScaler : MonoBehaviour
{
    RectTransform rectTransform;
    public void setCurrentScale()
    {
        float currentScale = PlayerPrefs.GetFloat("Minimap_Scale", 1f);
        rectTransform.localScale = Vector3.one * currentScale;

    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        setCurrentScale();
    }


}
