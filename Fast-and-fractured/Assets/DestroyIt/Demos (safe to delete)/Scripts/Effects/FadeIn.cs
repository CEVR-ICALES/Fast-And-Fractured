using UnityEngine;

namespace DestroyIt
{
    /// <summary>
    /// This script produces a screen-wide fade-in effect. It starts from one color and fades that color out over the specified number of seconds.
    /// </summary>
    public class FadeIn : MonoBehaviour
    {
        public Color startColor = Color.black;
        [Range(0f, 10f)]
        public float fadeLength = 2f;   // how long (in seconds) the fade effect lasts 

        private Texture2D blackTexture;
        private float alphaFadeValue = 1f;

        void Start()
        {
            blackTexture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            blackTexture.SetPixel(0, 0, startColor);
            blackTexture.Apply();
        }

        void Update()
        {
            alphaFadeValue -= Mathf.Clamp01(Time.deltaTime / fadeLength);

            if (alphaFadeValue <= 0f)
                Destroy(this);
        }

        void OnGUI()
        {
            GUI.color = new Color(alphaFadeValue, alphaFadeValue, alphaFadeValue, alphaFadeValue);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTexture);
        }
    }
}