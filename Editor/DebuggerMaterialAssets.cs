using System;
using System.Linq;
using Dreamy.Debugger.Foundation.UI;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Presentation.UI.Other;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger.Editor
{
    /// <summary>Maintainer-only authoring of the included skin; consumers do not run setup.</summary>
    public static class DebuggerMaterialAssets
    {
        private const string Root="Packages/com.dreamy.debugger/Runtime/Branding/Material/";
        private static Font font, medium, mono;
        private static Sprite surface, wordmark;
        public static void Install()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play before authoring assets.");
            ExtractOriginalWordmark();
            AssetDatabase.Refresh();
            foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Root.TrimEnd('/')}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(importer==null)continue;
                var logo=path.EndsWith("Wordmark.png");importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
                importer.mipmapEnabled=false;importer.isReadable=false;importer.alphaIsTransparency=true;importer.npotScale=TextureImporterNPOTScale.None;
                importer.maxTextureSize=logo?512:32;importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.spriteBorder=path.Contains("rounded-")?new Vector4(8,8,8,8):Vector4.zero;
                var android=importer.GetPlatformTextureSettings("Android");android.overridden=true;android.maxTextureSize=logo?512:32;android.format=TextureImporterFormat.ASTC_4x4;android.compressionQuality=100;importer.SetPlatformTextureSettings(android);
                var ios=importer.GetPlatformTextureSettings("iPhone");ios.overridden=true;ios.maxTextureSize=logo?512:32;ios.format=TextureImporterFormat.ASTC_4x4;ios.compressionQuality=100;importer.SetPlatformTextureSettings(ios);
                importer.SaveAndReimport();
            }
            font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Barlow-Regular.ttf");medium=AssetDatabase.LoadAssetAtPath<Font>(Root+"Barlow-SemiBold.ttf");mono=AssetDatabase.LoadAssetAtPath<Font>(Root+"JetBrainsMono-Regular.ttf");
            surface=Sprite("rounded-fill");wordmark=Sprite("Wordmark");
            var catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath);
            var entries=catalog.Entries.Where(e=>!e.Key.StartsWith("DreamyDebugger/Theme/")).ToList();
            Add(entries,"Font",font);Add(entries,"MediumFont",medium);Add(entries,"MonoFont",mono);Add(entries,"Button",surface);Add(entries,"Panel",surface);Add(entries,"Wordmark",wordmark);
            foreach(var icon in new[]{"menu","close","backspace","pin","drag","search","chevron","console","performance","settings","circle"})Add(entries,icon,Sprite(icon));
            var cardPath=AssetDatabase.FindAssets("t:Prefab",new[]{"Packages/com.dreamy.debugger/Runtime"}).Select(AssetDatabase.GUIDToAssetPath).First(p=>p.EndsWith("/TaskCard.prefab"));
            entries.RemoveAll(e=>e.Key=="DreamyDebugger/OptionCard");entries.Add(new CatalogEntry{Key="DreamyDebugger/OptionCard",Asset=AssetDatabase.LoadAssetAtPath<GameObject>(cardPath)});
            catalog.Entries=entries.ToArray();EditorUtility.SetDirty(catalog);AssetDatabase.SaveAssetIfDirty(catalog);
            PresentationAssets.Catalog=catalog;
            foreach(var guid in AssetDatabase.FindAssets("t:StyleSheet",new[]{"Packages/com.dreamy.debugger/Runtime"}))
            {
                var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var key in sheet.GetStyleKeys(false))
                {
                    var style=sheet.GetStyle(key);
                    if(key=="PinEntry_Number_Button" || key=="Button_Background" || key=="Options_Item_Button") {style.NormalColor=Theme.Control;style.HoverColor=Theme.Control*1.1f;style.ActiveColor=Theme.Selected;style.DisabledColor=new Color(Theme.Control.r,Theme.Control.g,Theme.Control.b,.4f);style.Image=surface;continue;}
                    style.NormalColor=Remap(style.NormalColor);style.HoverColor=Remap(style.HoverColor);style.ActiveColor=Remap(style.ActiveColor);style.DisabledColor=Remap(style.DisabledColor);
                    if(key.Contains("Button") || key.Contains("Background") || key.EndsWith("_BG"))style.Image=surface;
                    var icon=IconFor(key);if(icon!=null){style.Image=icon;}
                }
                EditorUtility.SetDirty(sheet);AssetDatabase.SaveAssetIfDirty(sheet);
            }
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Packages/com.dreamy.debugger/Runtime"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(guid);var go=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach(var label in go.GetComponentsInChildren<Text>(true))
                    {
                        label.font=label.fontStyle==FontStyle.Bold || label.font==medium?medium:font;label.color=Remap(label.color);
                        if(!label.name.Contains("StackTrace") && label.GetComponentInParent<InputField>()==null){label.resizeTextForBestFit=true;label.resizeTextMinSize=11;label.resizeTextMaxSize=Mathf.Max(12,label.fontSize);}
                    }
                    foreach(var image in go.GetComponentsInChildren<Image>(true))
                    {
                        image.color=Remap(image.color);
                        if(image.sprite!=null && image.sprite.texture!=null && image.sprite.texture.name=="DreamyBanner")
                        {
                            image.sprite=wordmark;image.type=Image.Type.Simple;image.preserveAspect=true;
                        }
                        var style=image.GetComponent<StyleComponent>();var icon=style!=null?IconFor(style.StyleKey):null;
                        if(icon!=null){image.sprite=icon;image.type=Image.Type.Simple;image.preserveAspect=true;}
                        else if(image.name=="BG" || image.name.Contains("Background") || image.name=="Panel backing" || image.name=="Metric card" || image.GetComponent<Button>()!=null || image.GetComponent<InputField>()!=null){image.sprite=surface;image.type=Image.Type.Sliced;}
                        if(image.sprite!=null && (image.sprite.name.Contains("circle") || image.name.IndexOf("dot",StringComparison.OrdinalIgnoreCase)>=0 || style!=null && style.StyleKey.EndsWith("_Blob"))){image.sprite=Sprite("circle");image.type=Image.Type.Simple;image.preserveAspect=true;var rt=image.rectTransform;rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f);rt.sizeDelta=new Vector2(8,8);var element=image.GetComponent<LayoutElement>();if(element!=null){element.minWidth=element.preferredWidth=8;element.minHeight=element.preferredHeight=8;element.flexibleWidth=element.flexibleHeight=0;}}
                    }
                    var category=go.GetComponent<CategoryGroup>();if(category!=null){category.Header.font=medium;category.Header.fontSize=18;if(category.CollapseButton!=null){var image=category.CollapseButton.GetComponent<Image>();if(image!=null){image.sprite=null;image.color=Color.clear;}}}
                    var card=go.GetComponent<OptionCard>();if(card!=null)
                    {
                        card.GetComponent<Image>().sprite=surface;card.GetComponent<Image>().color=Theme.Control;
                        var layout=card.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(16,16,12,12);layout.spacing=6;
                        card.Title.font=medium;card.Title.fontSize=20;card.Title.fontStyle=FontStyle.Normal;
                        card.BalanceSlot.GetComponent<LayoutElement>().preferredHeight=32;
                        card.AmountSlot.GetComponent<LayoutElement>().preferredHeight=44;card.AmountSlot.GetComponent<LayoutElement>().minWidth=144;card.AmountSlot.GetComponent<LayoutElement>().preferredWidth=160;
                        card.ActionSlot.GetComponent<LayoutElement>().preferredHeight=44;
                        card.ActionSlot.GetComponent<LayoutElement>().minWidth=88;card.ActionSlot.GetComponent<LayoutElement>().preferredWidth=88;
                    }
                    var pin=go.GetComponent<PinEntryControl>();if(pin!=null)Keypad(pin);
                    var logRow=go.GetComponent<ConsoleEntryView>();
                    if(logRow!=null && logRow.ImageStyle!=null)
                    {
                        var dot=(RectTransform)logRow.ImageStyle.transform;
                        dot.anchorMin=dot.anchorMax=new Vector2(0,.5f);
                        dot.anchoredPosition=new Vector2(86,0);dot.sizeDelta=new Vector2(8,8);
                    }
                    var hud=go.GetComponent<PerformanceHud>();if(hud!=null)Hud(hud);
                    var menu=go.GetComponentInChildren<MobileMenuController>(true);if(menu!=null)Panel(menu);
                    var dock=go.GetComponentInChildren<DockConsoleController>(true);if(dock!=null)Dock(dock);
                    PresentationAssets.ApplySkin(go);
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            AssetDatabase.SaveAssets();Debug.Log("[DreamyDebugger] Material assets saved. "+DebuggerAssetBuilder.ValidatePrefabs());
        }
        private static void ExtractOriginalWordmark()
        {
            // Deterministic source-color extraction keeps original letterforms and slash.
            // No generated typography is used as the production mark.
            var sourcePath=AssetDatabase.GetAssetPath(AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dreamy.debugger/Runtime/Branding/DreamyBanner.png"));
            var info=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(sourcePath);
            var path=System.IO.Path.Combine(info.resolvedPath,"Runtime/Branding/DreamyBanner.png");
            var source=new Texture2D(2,2,TextureFormat.RGBA32,false);source.LoadImage(System.IO.File.ReadAllBytes(path));
            var pixels=source.GetPixels32();var minX=source.width;var maxX=0;var minY=source.height;var maxY=0;
            for(var y=(int)(source.height*.33f);y<(int)(source.height*.76f);y++)for(var x=0;x<source.width;x++)
            {
                var pixel=pixels[y*source.width+x];if(Mathf.Min(pixel.g,pixel.b)<70)continue;
                minX=Mathf.Min(minX,x);maxX=Mathf.Max(maxX,x);minY=Mathf.Min(minY,y);maxY=Mathf.Max(maxY,y);
            }
            if(maxX<=minX || maxY<=minY){UnityEngine.Object.DestroyImmediate(source);throw new InvalidOperationException("No source logo pixels found.");}
            var width=maxX-minX+1;var height=maxY-minY+1;
            var cropped=new Texture2D(width+8,height+8,TextureFormat.RGBA32,false);var output=new Color32[(width+8)*(height+8)];
            for(var y=0;y<height;y++)for(var x=0;x<width;x++)
            {
                var pixel=pixels[(minY+y)*source.width+minX+x];var alpha=Mathf.Clamp01((Mathf.Min(pixel.g,pixel.b)-48)/70f);
                output[(y+4)*(width+8)+x+4]=new Color32(255,255,255,(byte)Mathf.RoundToInt(alpha*255));
            }
            cropped.SetPixels32(output);cropped.Apply();
            var texture=new Texture2D(512,Mathf.Max(1,Mathf.RoundToInt((height+8)*512f/(width+8))),TextureFormat.RGBA32,false);
            var resized=new Color[texture.width*texture.height];for(var y=0;y<texture.height;y++)for(var x=0;x<texture.width;x++)resized[y*texture.width+x]=cropped.GetPixelBilinear((x+.5f)/texture.width,(y+.5f)/texture.height);
            texture.SetPixels(resized);texture.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(info.resolvedPath,"Runtime/Branding/Material/Wordmark.png"),texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(cropped);UnityEngine.Object.DestroyImmediate(source);
        }
        private static void Add(System.Collections.Generic.List<CatalogEntry> entries,string key,UnityEngine.Object asset){if(asset!=null)entries.Add(new CatalogEntry{Key="DreamyDebugger/Theme/"+key,Asset=asset});}
        private static Sprite Sprite(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(Root+name+".png");
        private static Sprite IconFor(string key)
        {
            if(string.IsNullOrEmpty(key))return null;var k=key.ToLowerInvariant();
            if(k.EndsWith("_blob"))return Sprite("circle");
            if(!k.StartsWith("icon_") && !k.Contains("pindot") && !k.Contains("pin_dot"))return null;
            var name=k.Contains("pin") && !k.Contains("dot")?"pin":k.Contains("close")?"close":k.Contains("menu")?"menu":k.Contains("console")?"console":k.Contains("profiler") || k.Contains("monitor")?"performance":k.Contains("option") || k.Contains("info") || k.Contains("system")?"settings":k.Contains("down")?"chevron":k.Contains("dot")?"circle":null;
            return name!=null?Sprite(name):null;
        }
        private static Color Remap(Color color)
        {
            var old=new uint[]{0x18181B,0x202024,0x27272A,0x3F3F46,0xFAFAFA,0xA1A1AA,0x3F1D27,0xEF4444,0x302125,0x16171A,0x202126,0x292B31,0x303238,0xF4EFEA,0xA5A3A2,0x9E1008,0xE95345,0x572A2A};
            var next=new[]{Theme.Background,Theme.Surface,Theme.Control,Theme.Border,Theme.Ivory,Theme.Muted,Theme.Surface,Theme.Accent,Theme.Selected};
            for(int i=0;i<old.Length;i++){var before=Theme.Hex(old[i]);if(Mathf.Abs(color.r-before.r)+Mathf.Abs(color.g-before.g)+Mathf.Abs(color.b-before.b)>.012f)continue;var result=next[i%9];result.a=color.a;return result;}return color;
        }
        private static Image Icon(Transform parent,string name,Sprite sprite,Vector2 size)
        {
            var existing=parent.Find(name);var go=existing!=null?existing.gameObject:new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
            var image=go.GetComponent<Image>();if(image==null)image=go.AddComponent<Image>();image.sprite=sprite;image.color=Theme.Muted;image.preserveAspect=true;image.raycastTarget=false;
            var rect=(RectTransform)go.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=size;return image;
        }
        private static Button Button(Transform parent,string name,Sprite icon)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.sprite=surface;image.type=Image.Type.Sliced;image.color=Theme.Control;var button=go.GetComponent<Button>();button.targetGraphic=image;Icon(go.transform,"Icon",icon,new Vector2(20,20));return button;
        }
        private static void Keypad(PinEntryControl pin)
        {
            pin.PromptText.gameObject.SetActive(false);pin.PromptText.text="";
            foreach(var key in pin.NumberButtons)
            {
                var component=key.GetComponent<StyleComponent>();if(component!=null)component.enabled=false;
                var colors=ColorBlock.defaultColorBlock;colors.normalColor=Color.white;colors.highlightedColor=new Color(1.08f,1.08f,1.08f);colors.pressedColor=new Color(.8f,.8f,.8f);colors.disabledColor=new Color(1,1,1,.4f);key.colors=colors;
            }
            var oldStyle=pin.CancelButton.GetComponent<StyleComponent>();if(oldStyle!=null)oldStyle.enabled=false;
            pin.CancelButtonText.raycastTarget=false;pin.CancelButtonText.text="";
            var hitNode=pin.CancelButton.transform.Find("Hit surface");
            var hit=hitNode!=null?hitNode.GetComponent<Image>():null;
            if(hit==null){var go=new GameObject("Hit surface",typeof(RectTransform),typeof(Image));go.transform.SetParent(pin.CancelButton.transform,false);var rt=(RectTransform)go.transform;rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;go.transform.SetAsFirstSibling();hit=go.GetComponent<Image>();}hit.sprite=surface;hit.type=Image.Type.Sliced;hit.color=Theme.Control;hit.raycastTarget=true;pin.CancelButton.targetGraphic=hit;
            Icon(pin.CancelButton.transform,"Cancel icon",Sprite("close"),new Vector2(20,20));
            if(pin.BackspaceButton==null)pin.BackspaceButton=Button(pin.NumberButtons[0].transform.parent,"Backspace",Sprite("backspace"));
            foreach(var dot in pin.NumberDots){foreach(var image in dot.GetComponentsInChildren<Image>(true)){image.sprite=Sprite("circle");image.type=Image.Type.Simple;image.preserveAspect=true;}}
        }
        private static void Hud(PerformanceHud hud)
        {
            var card=hud.transform.Find("SafeArea/Metric card");if(card==null)return;
            var grip=card.GetComponentInChildren<HudDragHandle>(true);if(grip!=null){var text=grip.GetComponent<Text>();if(text!=null)text.text="";Icon(grip.transform,"Move icon",Sprite("drag"),new Vector2(20,20));}
            if(hud.CloseButton==null){hud.CloseButton=Button(card,"Close HUD",Sprite("close"));var rect=(RectTransform)hud.CloseButton.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-4,-4);rect.sizeDelta=new Vector2(44,44);}
            if(grip!=null){var rect=(RectTransform)grip.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-48,-4);rect.sizeDelta=new Vector2(44,44);}
            if(hud.CycleButton==null){var go=new GameObject("Cycle HUD",typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(card,false);var rect=(RectTransform)go.transform;rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(0,1);rect.offsetMin=new Vector2(0,-58);rect.offsetMax=new Vector2(-96,0);var image=go.GetComponent<Image>();image.color=Color.clear;hud.CycleButton=go.GetComponent<Button>();hud.CycleButton.targetGraphic=image;}
        }
        private static void Panel(MobileMenuController menu)
        {
            var state=new SerializedObject(menu);state.FindProperty("_maxMenuWidth").floatValue=160;state.ApplyModifiedPropertiesWithoutUndo();
            var sidebar=menu.Menu.Find("Content");if(sidebar!=null){var rail=sidebar.Find("ViewportLayout/Sizer");if(rail!=null){var element=rail.GetComponent<LayoutElement>();if(element!=null){element.minWidth=160;element.preferredWidth=160;}}}
            foreach(var logo in new[]{menu.HeaderLogo,menu.SidebarLogo})if(logo!=null){var image=logo.GetComponent<Image>();if(image!=null){image.sprite=wordmark;image.color=Color.white;image.type=Image.Type.Simple;image.preserveAspect=true;image.raycastTarget=false;var element=logo.GetComponent<LayoutElement>();if(element!=null){element.minWidth=96;element.preferredWidth=112;element.flexibleWidth=0;element.preferredHeight=28;element.minHeight=0;}var parent=logo.transform.parent.GetComponent<HorizontalLayoutGroup>();if(parent!=null){parent.childForceExpandWidth=false;parent.childAlignment=TextAnchor.MiddleCenter;}var style=image.GetComponent<StyleComponent>();if(style!=null)style.enabled=false;}}
            var header=menu.OpenButton.transform.parent.parent.GetComponent<Image>();if(header!=null)header.color=Theme.Surface;
            foreach(var label in menu.GetComponentsInChildren<Text>(true))if(label.name=="Title")label.font=medium;
        }
        private static void Dock(DockConsoleController dock)
        {
            foreach(var text in dock.GetComponentsInChildren<Text>(true))text.font=mono;
            var toolbar = dock.TopBar.transform.Find("Content");
            var toolbarLayout = toolbar != null ? toolbar.GetComponent<HorizontalLayoutGroup>() : null;
            if (toolbarLayout != null) toolbarLayout.padding.right = 42;
            if(dock.CloseButton==null){dock.CloseButton=Button(dock.TopBar.transform,"Close dock",Sprite("close"));var rect=(RectTransform)dock.CloseButton.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,1);rect.anchoredPosition=new Vector2(-2,-2);rect.sizeDelta=new Vector2(36,36);var element=dock.CloseButton.gameObject.AddComponent<LayoutElement>();element.ignoreLayout=true;}
            var dropdown=(RectTransform)dock.Dropdown.transform;var layout=dropdown.GetComponent<LayoutElement>()??dropdown.gameObject.AddComponent<LayoutElement>();layout.minHeight=80;layout.preferredHeight=140;layout.flexibleHeight=0;
        }
    }
}
