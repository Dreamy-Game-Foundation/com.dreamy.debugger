using System;
using Dreamy.Debugger.Presentation;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class TouchNumberField : InputField
    {
        internal Action OpenEditor;
        internal Func<bool> UseEditor;
        private ScrollRect scroll;
        private bool UseTouchEditor(PointerEventData e) => UseEditor?.Invoke() == true && InputBridge.IsTouch(e);
        public override void OnPointerDown(PointerEventData e)
        {
            if (UseTouchEditor(e))
            {
                scroll = GetComponentInParent<ScrollRect>();
                scroll?.OnInitializePotentialDrag(e);
                return;
            }
            base.OnPointerDown(e);
        }
        public override void OnPointerClick(PointerEventData e)
        {
            if (UseTouchEditor(e)) { OpenEditor(); return; }
            base.OnPointerClick(e);
        }
        public override void OnBeginDrag(PointerEventData e)
        {
            if (UseTouchEditor(e)) scroll?.OnBeginDrag(e); else base.OnBeginDrag(e);
        }
        public override void OnDrag(PointerEventData e)
        {
            if (UseTouchEditor(e)) scroll?.OnDrag(e); else base.OnDrag(e);
        }
        public override void OnEndDrag(PointerEventData e)
        {
            if (scroll != null) { scroll.OnEndDrag(e); scroll = null; }
            else if (!UseTouchEditor(e)) base.OnEndDrag(e);
        }
    }
}
