using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger
{
    public sealed class RestoreHoldFeedbackView : MonoBehaviour
    {
        public CanvasScaler Scaler;
        public RectTransform Card;
        public RestoreHoldRingGraphic Ring;
        public Text Percent, Title, Hint;

        internal void PresentShutter(string title, string hint, float progress)
        {
            Present(true, progress);
            Card.anchorMin = Card.anchorMax = new Vector2(1, 1);
            Card.pivot = new Vector2(1, 1); Card.anchoredPosition = new Vector2(-8, -8);
            if (Title != null) Title.text = title;
            if (Hint != null) Hint.text = hint;
            Percent.text = progress >= 1 ? "OK" : "…";
        }

        internal void Present(bool visible, float progress)
        {
            if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
            if (!visible) { Ring.Progress = 0; Percent.text = "0%"; return; }
            Card.anchorMin = Card.anchorMax = new Vector2(0, 1);
            Card.pivot = new Vector2(0, 1); Card.anchoredPosition = new Vector2(8, -8);
            if (Title != null) Title.text = "Hold to restore UI";
            if (Hint != null) Hint.text = "Release to cancel";
            Scaler.scaleFactor = Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160 : 1, 1, 3);
            var parent = (RectTransform)Card.parent;
            Card.sizeDelta = new Vector2(Mathf.Min(300, Mathf.Max(1, parent.rect.width - 16)), 72);
            Ring.Progress = progress;
            Percent.text = Mathf.RoundToInt(progress * 100) + "%";
        }
    }
}
