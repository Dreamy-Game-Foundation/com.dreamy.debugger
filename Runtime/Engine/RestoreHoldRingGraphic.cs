using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RestoreHoldRingGraphic : MaskableGraphic
    {
        private float progress;
        public float Progress
        {
            get => progress;
            set { var next = Mathf.Clamp01(value); if (next == progress) return; progress = next; SetVerticesDirty(); }
        }
        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            AddArc(vertices, 1, Theme.Border);
            AddArc(vertices, progress, color);
        }
        private void AddArc(VertexHelper vertices, float amount, Color32 tint)
        {
            var rect = GetPixelAdjustedRect(); var center = rect.center;
            var outer = Mathf.Min(rect.width, rect.height) * .5f; var inner = outer * .72f;
            var count = Mathf.CeilToInt(amount * 64);
            for (var i = 0; i < count; i++)
            {
                var start = Mathf.PI * .5f - (i / 64f) * Mathf.PI * 2;
                var end = Mathf.PI * .5f - Mathf.Min((i + 1) / 64f, amount) * Mathf.PI * 2;
                var a = new Vector2(Mathf.Cos(start), Mathf.Sin(start)); var b = new Vector2(Mathf.Cos(end), Mathf.Sin(end));
                var index = vertices.currentVertCount;
                vertices.AddVert(center + a * outer, tint, Vector2.zero);
                vertices.AddVert(center + b * outer, tint, Vector2.zero);
                vertices.AddVert(center + b * inner, tint, Vector2.zero);
                vertices.AddVert(center + a * inner, tint, Vector2.zero);
                vertices.AddTriangle(index, index + 1, index + 2); vertices.AddTriangle(index + 2, index + 3, index);
            }
        }
    }
}
