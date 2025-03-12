using UnityEngine;

namespace Game
{
    public class CallTests : MonoBehaviour
    {
        public Sprite sprite;

        public void TestUpdateUIElements()
        {
            HUDManager.Instance.UpdateUIElement(UIElementType.HealthBar, Random.Range(0f, 100f), 100f);
            HUDManager.Instance.UpdateUIElement(UIElementType.DashCooldown, Random.Range(0f, 100f), 100f);
            HUDManager.Instance.UpdateUIElement(UIElementType.Player0, sprite);
            HUDManager.Instance.UpdateUIElement(UIElementType.EventText, "¡TOMA TOMATE VIEJO TRAIDOR!");
        }
    }
}