using UnityEngine;
using UnityEngine.UI;

namespace Utilities.Managers.PauseSystem
{
    [RequireComponent(typeof(Button))]
    public class ResumeGameButton : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(Resume);
        }

        private static void Resume()
        {
            if (PauseManager.ExistingInstance != null) PauseManager.ExistingInstance.ResumeGame();
        }
    }
}
