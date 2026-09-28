using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FastAndFractured
{
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

            if (selected == lastSelected)
                return;

            lastSelected = selected;

            RectTransform target =
                selected.GetComponent<RectTransform>();

            if (target == null)
                return;

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

            Vector3 bottom =
                viewport.InverseTransformPoint(corners[0]);

            Vector3 top =
                viewport.InverseTransformPoint(corners[1]);

            float viewportBottom = viewport.rect.yMin;
            float viewportTop = viewport.rect.yMax;

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

                scrollRect.verticalNormalizedPosition =
                    Mathf.Clamp01(
                        scrollRect.verticalNormalizedPosition -
                        normalizedMovement
                    );

                scrollRect.StopMovement();
            }

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

                scrollRect.verticalNormalizedPosition =
                    Mathf.Clamp01(
                        scrollRect.verticalNormalizedPosition +
                        normalizedMovement
                    );

                scrollRect.StopMovement();
            }
        }
    }
}