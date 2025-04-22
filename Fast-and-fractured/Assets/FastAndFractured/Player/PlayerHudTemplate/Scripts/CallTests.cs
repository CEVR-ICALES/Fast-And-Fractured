using UnityEngine;

namespace FastAndFractured
{
    public class CallTests : MonoBehaviour
    {
        public Sprite sprite;
        [SerializeField] private StatsController playerStatsController;

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                TestUpdateUIElements();
            }
        }

        public void TestUpdateUIElements()
        {
            // HUDManager.Instance.UpdateUIElement(UIElementType.HealthBar, Random.Range(0f, 100f), 100f);
            // HUDManager.Instance.UpdateUIElement(UIElementType.DashCooldown, Random.Range(0f, 100f), 100f);
            // HUDManager.Instance.UpdateUIElement(UIElementType.Player0, sprite);
            // HUDManager.Instance.UpdateUIElement(UIElementType.EventText, "¡TOMA TOMATE VIEJO TRAIDOR!");

            playerStatsController.TakeEndurance(60, false,this.gameObject);
        }
    }
}