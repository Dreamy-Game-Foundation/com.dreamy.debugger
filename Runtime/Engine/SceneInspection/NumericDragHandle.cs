using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Dreamy.Debugger.SceneInspection
{
    /// <summary>Horizontal label gestures; vertical touch gestures remain owned by the scroll view.</summary>
    public sealed class NumericDragHandle : MonoBehaviour, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        public Func<bool> Begin;
        public Action<float> Preview;
        public Action<bool> End;
        private bool dragging, scrolling;
        private float distance;
        private UnityEngine.UI.ScrollRect scroll;
        public void OnInitializePotentialDrag(PointerEventData e)
        { scroll = GetComponentInParent<UnityEngine.UI.ScrollRect>(); scroll?.OnInitializePotentialDrag(e); }
        public void OnBeginDrag(PointerEventData e)
        {
            distance = 0;
            scrolling = Dreamy.Debugger.Presentation.InputBridge.IsTouch(e) || Mathf.Abs(e.position.y - e.pressPosition.y) > Mathf.Abs(e.position.x - e.pressPosition.x);
            if (scrolling) { scroll?.OnBeginDrag(e); return; }
            dragging = Begin != null && Begin();
        }
        public void OnDrag(PointerEventData e)
        {
            if (scrolling) { scroll?.OnDrag(e); return; }
            if (!dragging) return;
            var scale = 1f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace) scale = Mathf.Max(.01f, canvas.scaleFactor);
            var fine = Dreamy.Debugger.Presentation.InputBridge.IsHeld(KeyCode.LeftShift) || Dreamy.Debugger.Presentation.InputBridge.IsHeld(KeyCode.RightShift);
            distance += e.delta.x / scale * (fine ? .1f : 1f);
            Preview?.Invoke(distance);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (scrolling) { scroll?.OnEndDrag(e); scrolling = false; return; }
            if (!dragging) return;
            dragging = false; End?.Invoke(true);
        }
        public void Abort()
        {
            if (scrolling && scroll != null) scroll.StopMovement();
            scrolling = false;
            if (!dragging) return;
            dragging = false; End?.Invoke(false);
        }
        public void OnCancel(BaseEventData e) { Abort(); e.Use(); }
        private void OnDisable() => Abort();
        private void OnApplicationFocus(bool focused) { if (!focused) Abort(); }
        private void OnApplicationPause(bool paused) { if (paused) Abort(); }
    }
}
