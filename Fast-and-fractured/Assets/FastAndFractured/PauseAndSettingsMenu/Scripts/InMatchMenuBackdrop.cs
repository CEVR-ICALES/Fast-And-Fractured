using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FastAndFractured
{
    public sealed class InMatchMenuBackdrop : MonoBehaviour
    {
        private const float VEIL_MAX_ALPHA = 0.6f;
        private const float VEIL_MIN_BRIGHTNESS = 0.8f;
        private static readonly Color BACKDROP_COLOR = new Color(0.07f, 0.05f, 0.09f, 0.92f);

        private sealed class Finder : MonoBehaviour
        {
            private int _seen = -1;

            private void Update()
            {
                if (_seen == MenuScan.Version) return;
                _seen = MenuScan.Version;
                if (SceneManager.GetActiveScene().buildIndex == LevelConstants.MAIN_MENU_SCENE_BUILD_INDEX) return;
                foreach (var screen in MenuScan.Screens)
                    if (screen != null && screen.gameObject.scene.IsValid() && screen.GetComponent<InMatchMenuBackdrop>() == null)
                        screen.gameObject.AddComponent<InMatchMenuBackdrop>().Apply();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("[InMatchMenuBackdrop]");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.AddComponent<Finder>();
        }

        private void Apply()
        {
            foreach (Transform child in transform)
                if (child.TryGetComponent(out Image image) && IsVeil(image.color))
                    image.color = BACKDROP_COLOR;
        }

        public static bool IsVeil(Color color) => color.a < VEIL_MAX_ALPHA && color.grayscale > VEIL_MIN_BRIGHTNESS;
    }
}
