using UnityEngine;
using UnityEngine.EventSystems;
namespace Dreamy.Debugger
{
    /// <summary>One pointer owns the grip until release/cancel/focus loss; other HUD graphics pass through.</summary>
    public sealed class HudDragHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, ICancelHandler
    {
        private PerformanceHud owner;
        private int? pointer;
        private RectTransform hitTarget;
        internal void FitHitTarget(float pixelsPerUnit)
        {
            if (hitTarget == null)
            {
                var graphic = GetComponent<UnityEngine.UI.Graphic>();
                if (graphic != null) graphic.raycastTarget = false;
                var region = new GameObject("Drag hit area", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                hitTarget = (RectTransform)region.transform;
                hitTarget.SetParent(transform, false);
                var image = region.GetComponent<UnityEngine.UI.Image>();
                image.color = Color.clear; image.raycastTarget = true;
            }
            var visual = (RectTransform)transform;
            var minimum = 48 / Mathf.Max(.01f, pixelsPerUnit);
            hitTarget.anchorMin = hitTarget.anchorMax = hitTarget.pivot = new Vector2(.5f, .5f);
            hitTarget.sizeDelta = Vector2.Max(visual.rect.size, new Vector2(minimum, minimum));
            // Grow toward the card interior, keeping the small visible grip unchanged.
            hitTarget.anchoredPosition = new Vector2(-Mathf.Max(0, (hitTarget.rect.width - visual.rect.width) * .5f),
                -Mathf.Max(0, (hitTarget.rect.height - visual.rect.height) * .5f));
        }
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
