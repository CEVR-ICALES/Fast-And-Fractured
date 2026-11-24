using System.Collections.Generic;
using UnityEngine;

namespace FastAndFractured
{
    public sealed class DebugHud
    {
        private readonly List<Canvas> _hidden = new List<Canvas>();

        public bool Hidden { get; private set; }

        public void Hide() => Hide(Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None));

        public void Hide(IEnumerable<Canvas> canvases)
        {
            Hidden = true;
            foreach (Canvas canvas in canvases)
            {
                if (!ShouldHide(canvas)) continue;
                canvas.enabled = false;
                _hidden.Add(canvas);
            }
        }

        public void Show()
        {
            Hidden = false;
            foreach (Canvas canvas in _hidden)
                if (canvas != null) canvas.enabled = true;
            _hidden.Clear();
        }

        public static bool ShouldHide(Canvas canvas) => canvas != null && canvas.enabled && canvas.isRootCanvas;
    }
}
