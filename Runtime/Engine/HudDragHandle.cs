using UnityEngine;
using UnityEngine.EventSystems;

namespace Dreamy.Debugger
{
    /// <summary>Only the visible grip receives pointer events; graphs and text pass through.</summary>
    public sealed class HudDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private PerformanceHud owner;
        private void Awake() { owner = GetComponentInParent<PerformanceHud>(); }
        public void OnBeginDrag(PointerEventData data) { if (owner != null) owner.BeginDrag(data); }
        public void OnDrag(PointerEventData data) { if (owner != null) owner.Drag(data); }
        public void OnEndDrag(PointerEventData data) { if (owner != null) owner.EndDrag(); }
        private void OnDisable() { if (owner != null) owner.EndDrag(); }
    }
}
