using System;
using UnityEngine;
namespace Dreamy.Debugger.Presentation {
    [Serializable]public sealed class CatalogEntry {public string Key;public UnityEngine.Object Asset;}

    public static class PresentationAssets {
        private static readonly uint[] PreviousColors={0x16171A,0x202126,0x292B31,0x303238,0xF4EFEA,0xA5A3A2,0x9E1008,0xE95345,0x572A2A};
        private static readonly uint[] CurrentColors={0x18181B,0x202024,0x27272A,0x3F3F46,0xFAFAFA,0xA1A1AA,0x3F1D27,0xEF4444,0x302125};
        private static Color SurfaceColor(Color color){
            for(var i=0;i<PreviousColors.Length;i++){
                var old=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Hex(PreviousColors[i]);
                if(Mathf.Abs(color.r-old.r)+Mathf.Abs(color.g-old.g)+Mathf.Abs(color.b-old.b)>.005f)continue;
                var changed=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Hex(CurrentColors[i]);changed.a=color.a;return changed;
            }
            return color;
        }
        public static DebuggerAssetCatalog Catalog;
        public static void ApplySkin(GameObject root) {
            if(root==null || Catalog==null)return;
            var font=Load<Font>("DreamyDebugger/Theme/Font");
            var mono=Load<Font>("DreamyDebugger/Theme/MonoFont");
            var button=Load<Sprite>("DreamyDebugger/Theme/Button");
            var panel=Load<Sprite>("DreamyDebugger/Theme/Panel");
            if(font!=null)foreach(var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true)){
                var code=label.name.Contains("StackTrace") || label.name.Contains("Timestamp") || label.name=="Message" || label.name=="Count";
                label.font=code && mono!=null?mono:font;label.color=SurfaceColor(label.color);
            }
            if(mono!=null){
                foreach(var console in root.GetComponentsInChildren<UI.Tabs.ConsoleTabController>(true))if(console.StackTraceText!=null)console.StackTraceText.font=mono;
                foreach(var number in root.GetComponentsInChildren<UI.Controls.Data.NumberControl>(true))if(number.NumberSpinner!=null)number.NumberSpinner.textComponent.font=mono;
            }
            foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true)){
                image.color=SurfaceColor(image.color);
                var selectable=image.GetComponent<UnityEngine.UI.Selectable>();
                var texture=selectable!=null?button:(image.name=="BG" || image.name.Contains("Background"))?panel:null;
                if(texture==null)continue;
                image.sprite=texture;image.type=UnityEngine.UI.Image.Type.Sliced;
                if(selectable!=null){
                    var edge=image.GetComponent<UnityEngine.UI.Outline>()??image.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    edge.effectColor=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Border;edge.effectDistance=new Vector2(1,-1);edge.useGraphicAlpha=true;
                }
            }
            if(root.GetComponent<UI.Controls.PinEntryControl>()!=null || root.GetComponent<UI.Other.BugReportPopoverRoot>()!=null || root.name.StartsWith("ModalPopup",StringComparison.Ordinal))StyleModalSurfaces(root);
        }
        public static void ApplyModalSkin(GameObject root)
        {
            ApplySkin(root);
            StyleModalSurfaces(root);
        }
        private static void StyleModalSurfaces(GameObject root)
        {
            var pin=root.GetComponent<UI.Controls.PinEntryControl>();
            var report=root.GetComponent<UI.Other.BugReportPopoverRoot>();
            foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                if(image.name=="Modal scrim" || image.name=="BackgroundFade" || image.name=="FadeBackground" || image.name=="EventBG" || pin!=null && image==pin.Background)
                {
                    image.sprite=null;image.color=new Color(0,0,0,.52f);
                    var edge=image.GetComponent<UnityEngine.UI.Outline>();if(edge!=null)edge.enabled=false;
                }
                else if(image.name=="Dialog" || image.name=="Panel backing" || image.name=="Modal panel" || image.name=="Box" || report!=null && image.transform==report.Container)
                {
                    var sprite=Load<Sprite>("DreamyDebugger/Theme/Panel");
                    if(sprite!=null){image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;}
                    var surface=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Surface;surface.a=.98f;image.color=surface;
                    var edge=image.GetComponent<UnityEngine.UI.Outline>()??image.gameObject.AddComponent<UnityEngine.UI.Outline>();
                    edge.effectColor=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Border;edge.effectDistance=new Vector2(1,-1);
                }
            }
            if(pin!=null)
            {
                var mono=Load<Font>("DreamyDebugger/Theme/MonoFont");
                foreach(var button in pin.NumberButtons)
                {
                    if(button.targetGraphic is UnityEngine.UI.Image fill)
                    {
                        fill.sprite=Load<Sprite>("DreamyDebugger/Theme/Button")??fill.sprite;
                        fill.type=UnityEngine.UI.Image.Type.Sliced;fill.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Control;
                    }
                    if(mono!=null)foreach(var text in button.GetComponentsInChildren<UnityEngine.UI.Text>(true))text.font=mono;
                }
            }
        }
        public static void ApplyHudSkin(GameObject root,RectTransform card)
        {
            ApplySkin(root);
            var mono=Load<Font>("DreamyDebugger/Theme/MonoFont");
            if(mono!=null)foreach(var text in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))text.font=mono;
            if(card==null)return;
            var image=card.GetComponent<UnityEngine.UI.Image>();
            if(image==null)return;
            var sprite=Load<Sprite>("DreamyDebugger/Theme/Panel");
            if(sprite!=null){image.sprite=sprite;image.type=UnityEngine.UI.Image.Type.Sliced;}
            var color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Background;color.a=.70f;image.color=color;
            image.raycastTarget=false;
        }
        public static T[] LoadAll<T>(string prefix) where T:UnityEngine.Object {
            var list=new System.Collections.Generic.List<T>();
            if(Catalog!=null)foreach(var entry in Catalog.Entries)if(entry.Key.StartsWith(prefix+"/",StringComparison.Ordinal)){var value=Load<T>(entry.Key);if(value!=null)list.Add(value);}
            return list.ToArray();
        }
        public static T Load<T>(string key) where T:UnityEngine.Object {
            if(Catalog!=null)foreach(var entry in Catalog.Entries)if(entry.Key==key){if(entry.Asset is GameObject go && typeof(Component).IsAssignableFrom(typeof(T)))return go.GetComponent(typeof(T)) as T;return entry.Asset as T;}
            return null;
        }
    }
}
