using UnityEngine;
using UnityEngine.UI;

namespace FastAndFractured
{
    public class SkewedImage : Image
    {
        [Tooltip("Horizontal offset (px) of the top edge relative to the bottom edge.")]
        [SerializeField] private float skew = 40f;

        public float Skew
        {
            get => skew;
            set { skew = value; SetVerticesDirty(); }
        }

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);
            Rect rect = GetPixelAdjustedRect();
            UIVertex vertex = new UIVertex();
            for (int i = 0; i < toFill.currentVertCount; i++)
            {
                toFill.PopulateUIVertex(ref vertex, i);
                vertex.position.x += skew * Mathf.InverseLerp(rect.yMin, rect.yMax, vertex.position.y);
                toFill.SetUIVertex(vertex, i);
            }
        }
    }
}
