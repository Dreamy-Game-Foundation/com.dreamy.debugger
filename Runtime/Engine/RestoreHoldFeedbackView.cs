using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger
{
    public sealed class RestoreHoldFeedbackView : MonoBehaviour
    {
        public CanvasScaler Scaler;
        public RectTransform Card;
        public RestoreHoldRingGraphic Ring;
        public Text Percent;

        internal void Present(bool visible, float progress)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) { Ring.Progress = 0; Percent.text = "0%"; return; }
            Scaler.scaleFactor = Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160 : 1, 1, 3);
            var parent = (RectTransform)Card.parent;
            Card.sizeDelta = new Vector2(Mathf.Min(300, Mathf.Max(1, parent.rect.width - 16)), 72);
            Ring.Progress = progress;
            Percent.text = Mathf.RoundToInt(progress * 100) + "%";
        }
    }
}
