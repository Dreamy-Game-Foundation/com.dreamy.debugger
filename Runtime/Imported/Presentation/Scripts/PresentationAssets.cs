using System;
using UnityEngine;
namespace Dreamy.Debugger.Presentation {
    [Serializable]public sealed class CatalogEntry {public string Key;public UnityEngine.Object Asset;}

    public static class PresentationAssets {
        public static DebuggerAssetCatalog Catalog;
        public static void ApplySkin(GameObject root) {
            if(root==null || Catalog==null)return;
            var font=Load<Font>("DreamyDebugger/Theme/Font");
            var button=Load<Sprite>("DreamyDebugger/Theme/Button");
            var panel=Load<Sprite>("DreamyDebugger/Theme/Panel");
            if(font!=null)foreach(var label in root.GetComponentsInChildren<UnityEngine.UI.Text>(true))label.font=font;
            foreach(var image in root.GetComponentsInChildren<UnityEngine.UI.Image>(true)){
                var selectable=image.GetComponent<UnityEngine.UI.Selectable>();
                var texture=selectable!=null?button:(image.name=="BG" || image.name.Contains("Background"))?panel:null;
                if(texture==null)continue;
                image.sprite=texture;image.type=UnityEngine.UI.Image.Type.Sliced;
            }
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
