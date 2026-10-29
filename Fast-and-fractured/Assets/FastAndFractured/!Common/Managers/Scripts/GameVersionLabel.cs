using TMPro;
using UnityEngine;

namespace FastAndFractured
{
    /// <summary>Shows the game version (Project Settings > Player > Version) on the text it sits on.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public class GameVersionLabel : MonoBehaviour
    {
        [SerializeField] private string prefix = "v";

        private void Awake()
        {
            GetComponent<TMP_Text>().text = prefix + Application.version;
        }
    }
}
