using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Perfomance : MonoBehaviour
{
    public bool isVisible;
    [Header("Perfomance Box")]
    public GameObject boxPerfomance;
    public Text textFPS;
    public Text textMinFPS;
    public Text textMaxFPS;

    private float fps;
    private float minFPS;
    private float maxFPS;

    // Start is called before the first frame update    
    void Start()
    {
        fps = 0;
        minFPS = 10000;
        maxFPS = 0;
        if (!isVisible)
        {
            boxPerfomance?.SetActive(false);
        }
    }

    // Update is called once per frame
    void Update()
    {
        fps = GetFPS();
        if (fps > maxFPS)
        {
            maxFPS = fps;
        }
        if (fps < minFPS)
        {
            minFPS = fps;
        }
        textFPS.text = "FPS: " + Mathf.RoundToInt(fps).ToString();
        textMaxFPS.text = "Max: " + Mathf.RoundToInt(maxFPS).ToString();
        textMinFPS.text = "Min: " + Mathf.RoundToInt(minFPS).ToString();
    }

    private float GetFPS()
    {
        return 1 / Time.unscaledDeltaTime;
    }
}
