using Game;
using TMPro;
using UnityEngine;

public class CallTests : MonoBehaviour
{
    public Sprite sprite;

    public void TestUpdateUIElements()
    {
        HUDManager.Instance.UpdateUIImageFillAmount(UIElementType.HealthBar, Random.Range(0f, 100f), 100f);
        HUDManager.Instance.UpdateUIImageFillAmount(UIElementType.DashCooldown, Random.Range(0f, 100f), 100f);
        HUDManager.Instance.UpdateUIImageSprite(UIElementType.Player0, sprite);
        HUDManager.Instance.UpdateUITextString(UIElementType.EventText, "¡TOMA TOMATE VIEJO TRAIDOR!");
    }
}