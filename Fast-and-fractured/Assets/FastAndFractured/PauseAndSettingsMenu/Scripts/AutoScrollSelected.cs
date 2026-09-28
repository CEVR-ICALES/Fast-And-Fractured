using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AutoScrollSelected : MonoBehaviour
{
    private ScrollRect scrollRect;
    private GameObject lastSelected;

    private void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    private void LateUpdate()
    {
        if (EventSystem.current == null)
            return;

        GameObject selected =
            EventSystem.current.currentSelectedGameObject;

        if (selected == null)
            return;

        // Solo actuar cuando cambia la selección
        if (selected == lastSelected)
            return;

        lastSelected = selected;

        RectTransform target =
            selected.GetComponent<RectTransform>();

        if (target == null)
            return;

        // Comprobar que pertenece al Content de ESTE ScrollRect
        if (!target.IsChildOf(scrollRect.content))
            return;

        ScrollToSelected(target);
    }

    private void ScrollToSelected(RectTransform target)
    {
        RectTransform viewport = scrollRect.viewport;

        if (viewport == null || scrollRect.content == null)
            return;

        Vector3[] corners = new Vector3[4];
        target.GetWorldCorners(corners);

        // Pasar las esquinas al espacio local del Viewport
        Vector3 bottom =
            viewport.InverseTransformPoint(corners[0]);

        Vector3 top =
            viewport.InverseTransformPoint(corners[1]);

        float viewportBottom = viewport.rect.yMin;
        float viewportTop = viewport.rect.yMax;

        // --------------------------------
        // OBJETO POR DEBAJO DEL VIEWPORT
        // --------------------------------

        if (bottom.y < viewportBottom)
        {
            float difference =
                viewportBottom - bottom.y;

            float scrollableHeight =
                scrollRect.content.rect.height -
                viewport.rect.height;

            if (scrollableHeight <= 0)
                return;

            float normalizedMovement =
                difference / scrollableHeight;

            // IMPORTANTE: hacia abajo = disminuir
            scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(
                    scrollRect.verticalNormalizedPosition -
                    normalizedMovement
                );

            scrollRect.StopMovement();
        }

        // --------------------------------
        // OBJETO POR ENCIMA DEL VIEWPORT
        // --------------------------------

        else if (top.y > viewportTop)
        {
            float difference =
                top.y - viewportTop;

            float scrollableHeight =
                scrollRect.content.rect.height -
                viewport.rect.height;

            if (scrollableHeight <= 0)
                return;

            float normalizedMovement =
                difference / scrollableHeight;

            // Hacia arriba = aumentar
            scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(
                    scrollRect.verticalNormalizedPosition +
                    normalizedMovement
                );

            scrollRect.StopMovement();
        }
    }
}