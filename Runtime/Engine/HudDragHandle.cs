using UnityEngine;
using UnityEngine.EventSystems;
namespace Dreamy.Debugger
{
    /// <summary>One pointer owns the grip until release/cancel/focus loss; other HUD graphics pass through.</summary>
    public sealed class HudDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        private PerformanceHud owner;
        private int? pointer;
        private void Awake(){owner=GetComponentInParent<PerformanceHud>();}
        public void OnBeginDrag(PointerEventData data){if(pointer.HasValue || owner==null)return;owner.BeginDrag(data);if(owner.IsDragging)pointer=data.pointerId;}
        public void OnDrag(PointerEventData data){if(pointer==data.pointerId && owner!=null)owner.Drag(data);}
        public void OnEndDrag(PointerEventData data){if(pointer==data.pointerId)Release();}
        public void OnCancel(BaseEventData data){Release();}
        private void Release(){pointer=null;if(owner!=null)owner.EndDrag();}
        private void OnDisable(){Release();}
        private void OnApplicationFocus(bool focused){if(!focused)Release();}
        private void OnApplicationPause(bool paused){if(paused)Release();}
    }
}
