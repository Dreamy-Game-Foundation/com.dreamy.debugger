using System;
using System.IO;
using System.Linq;
using Dreamy.Debugger.Foundation.UI;
using Dreamy.Debugger.Presentation.UI.Controls;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;
namespace Dreamy.Debugger.Editor {
    /// <summary>Scoped, repeatable authoring pass. Keeps prefab and component identities intact.</summary>
    public static class DebuggerRedesignAssets {
        public const string BannerPath="Packages/com.dreamy.debugger/Runtime/Branding/DreamyBanner.png";
        private const string Package="Packages/com.dreamy.debugger";
        private const string Staging="Assets/__DreamyDebuggerVisualAuthoring";
        private const string ResourceStaging="Assets/__DreamyDebuggerVisualResources";
        private const string HudStaging="Assets/__DreamyDebuggerVisualHud";
        [MenuItem("Tools/Dreamy Debugger/Advanced/Rebuild maintained visuals",priority=120)]
        public static void Apply(){
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play Mode before authoring debugger assets.");
            if(AssetDatabase.IsValidFolder(Staging)||AssetDatabase.IsValidFolder(HudStaging)||AssetDatabase.IsValidFolder(ResourceStaging))throw new InvalidOperationException("Visual authoring staging exists; inspect it before retrying.");
            ImportBanner();
            EditorApplication.LockReloadAssemblies();AssetDatabase.DisallowAutoRefresh();
            try {
                Move(Package+"/Runtime/Imported/Presentation/UI",Staging);
                Move(Package+"/Runtime/Imported/Presentation/Assets",ResourceStaging);
                Move(Package+"/Runtime/Prefabs",HudStaging);
                StyleSheets(Staging);
                StylePrefabs(Staging);
                StylePrefabs(ResourceStaging);
                StylePrefabs(HudStaging);
                BuildHud(HudStaging+"/PerformanceHud.prefab");
            } finally {
                try {
                    if(AssetDatabase.IsValidFolder(HudStaging))Move(HudStaging,Package+"/Runtime/Prefabs");
                    if(AssetDatabase.IsValidFolder(ResourceStaging))Move(ResourceStaging,Package+"/Runtime/Imported/Presentation/Assets");
                    if(AssetDatabase.IsValidFolder(Staging))Move(Staging,Package+"/Runtime/Imported/Presentation/UI");
                } finally { AssetDatabase.AllowAutoRefresh();EditorApplication.UnlockReloadAssemblies(); }
            }
            Debug.Log("[DreamyDebugger] Visual theme applied. "+DebuggerAssetBuilder.ValidatePrefabs());
        }
        private static void Move(string from,string to){var error=AssetDatabase.MoveAsset(from,to);if(!string.IsNullOrEmpty(error))throw new IOException(error);}
        private static void ImportBanner(){
            AssetDatabase.ImportAsset(BannerPath);
            var importer=AssetImporter.GetAtPath(BannerPath) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Dreamy banner is missing.");
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false;importer.alphaIsTransparency=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.maxTextureSize=2048;importer.SaveAndReimport();
        }
        public static void StyleSheets(string root){
            foreach(var guid in AssetDatabase.FindAssets("t:StyleSheet",new[]{root})){
                var sheet=AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                foreach(var key in sheet.GetStyleKeys(false)){
                    var style=sheet.GetStyle(key);var color=StyleColor(key,sheet.name);
                    style.NormalColor=color;style.HoverColor=color;style.ActiveColor=color;
                    var disabled=color;disabled.a*=.4f;style.DisabledColor=disabled;
                    if(key=="Options_Item_Button" || key=="Button_Background" || key=="PinEntry_Number_Button"){
                        style.HoverColor=Theme.Border;style.ActiveColor=Theme.Selected;
                    }
                    // Remove the old patterned backdrop, retain original icon and control sprites.
                    if(key=="SideBar_Background")style.Image=null;
                }
                EditorUtility.SetDirty(sheet);AssetDatabase.SaveAssetIfDirty(sheet);
            }
        }
        private static Color StyleColor(string key,string sheet){
            if(sheet.Contains("TabActive"))return key.Contains("Text")?Theme.Ivory:key.Contains("Border")?Theme.Accent:Theme.Selected;
            if(sheet.Contains("ConsoleSelected"))return Theme.Selected;
            if(sheet.Contains("ConsoleAlt"))return Theme.Surface;
            switch(key){
                case "Console_Error_Blob": return Theme.Accent;
                case "Console_Warning_Blob": return Theme.Warning;
                case "Console_Info_Blob": return Theme.Muted;
                case "SideBar_Background": case "Content_Block_Background": return Theme.Surface;
                case "Content_Background": case "Tab_Background": case "Pinned_Profiler_BG": return Theme.Background;
                case "Content_Background_Transparent": case "Tab_Background_Transparent": return new Color(Theme.Background.r,Theme.Background.g,Theme.Background.b,.9f);
                case "Tab_Header_Background": return Theme.Brand;
                case "SideBar_Tab_Background": return new Color(0,0,0,0);
                case "SideBar_Tab_Text": case "Text_Dim": case "Console_Entry_Stack": case "Profiler_Axis_Text": return Theme.Muted;
                case "Sidebar_Border": case "Content_Border": case "SideBar_Tab_Border": case "Profiler_Axis_Background": return Theme.Border;
                case "Options_Item_Background": case "Options_Item_Button": case "Button_Background": case "Options_SelectionToggleBackground": case "PinEntry_Number_Button": case "Console_Count": return Theme.Control;
                case "Toggle_BG_Active": case "Options_SelectionToggle": case "PinEntry_Dot_Fill": case "PinEntry_Number_Ring_Fill": case "Profiler_Memory_FG": return Theme.Accent;
                case "Profiler_Memory_BG": return Theme.Control;
                case "Console_Entry_BG": return new Color(0,0,0,0);
                case "ScrollBar": return Theme.Border;
                case "Tab_Header_Button": return new Color(0,0,0,0);
                case "Tab_Header_Toggle_Selected": return Theme.Selected;
                case "Modal_BackgroundFade": return new Color(0,0,0,.75f);
                default: return Theme.Ivory;
            }
        }
        public static void StylePrefabs(string root){
            foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{root})){
                var path=AssetDatabase.GUIDToAssetPath(guid);
                if(path.EndsWith("PerformanceHud.prefab"))continue;
                var go=PrefabUtility.LoadPrefabContents(path);
                try {
                    foreach(var text in go.GetComponentsInChildren<Text>(true)){
                        text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.verticalOverflow=VerticalWrapMode.Overflow;text.color=Theme.Ivory;text.raycastTarget=false;
                        // Replace the previous mint word-label with the real banner below.
                        if(text.name=="DreamyBrand")text.gameObject.SetActive(false);
                    }
                    foreach(var image in go.GetComponentsInChildren<Image>(true)){
                        var c=image.color;
                        if(c.b>c.r*1.25f || c.g>c.r*1.25f)image.color=image.GetComponent<Selectable>()!=null?Theme.Control:Theme.Surface;
                    }
                    foreach(var selectable in go.GetComponentsInChildren<Selectable>(true)){
                        if(selectable.GetComponent<StyleComponent>()!=null)continue;
                        var c=selectable.colors;c.normalColor=Theme.Control;c.highlightedColor=Theme.Border;c.pressedColor=Theme.Selected;c.selectedColor=Theme.Border;c.disabledColor=new Color(.2f,.2f,.2f,.5f);c.colorMultiplier=1;selectable.colors=c;
                    }
                    foreach(var graph in go.GetComponentsInChildren<ProfilerGraphControl>(true))graph.LineColours=new[]{Theme.Ivory,Theme.Warning,Theme.FpsSeries};
                    if(path.EndsWith("DebugPanel.prefab"))StylePanel(go);
                    if(path.EndsWith("/Tabs/Profiler.prefab")){
                        foreach(var text in go.GetComponentsInChildren<Text>(true)){
                            if(text.text=="Render" || text.text=="Update")text.transform.parent.gameObject.SetActive(false);
                            if(text.text=="Other" || text.text=="Frame")text.text="Frame time";
                        }
                    }
                    if(path.EndsWith("/SideBar/Tab.prefab")){
                        var layout=go.GetComponent<LayoutElement>();if(layout!=null){layout.minHeight=62;layout.preferredHeight=68;}
                        var tab=go.GetComponent<DebugTabButton>();if(tab!=null){tab.TitleText.fontSize=10;tab.TitleText.color=Theme.Muted;}
                    }
                    if(path.EndsWith("/Options/Category.prefab")){
                        var category=go.GetComponent<Dreamy.Debugger.Presentation.UI.Other.CategoryGroup>();
                        if(category!=null){
                            category.Header.fontSize=14;category.Header.fontStyle=FontStyle.Bold;
                            var rows=category.Container.GetComponent<VerticalLayoutGroup>();
                            if(rows!=null)UnityEngine.Object.DestroyImmediate(rows);
                            var flow=category.Container.GetComponent<Dreamy.Debugger.Foundation.UI.Layout.FlowLayoutGroup>();
                            if(flow==null)flow=category.Container.gameObject.AddComponent<Dreamy.Debugger.Foundation.UI.Layout.FlowLayoutGroup>();
                            flow.ChildForceExpandWidth=true;flow.ChildForceExpandHeight=false;
                            flow.Spacing=6;flow.padding=new RectOffset(0,0,40,0);
                        }
                    }
                    if(path.Contains("/Options/") && path.EndsWith("Option.prefab")){
                        var row=go.GetComponent<LayoutElement>();if(row==null)row=go.AddComponent<LayoutElement>();
                        row.minHeight=40;row.preferredHeight=path.EndsWith("ReadOnlyOption.prefab")?64:44;row.flexibleWidth=1;row.layoutPriority=3;
                        var number=go.GetComponent<Dreamy.Debugger.Presentation.UI.Controls.Data.NumberControl>();
                        if(number!=null){
                            var title=number.Title.rectTransform;title.anchorMin=Vector2.zero;title.anchorMax=new Vector2(.55f,1);title.offsetMin=new Vector2(10,4);title.offsetMax=new Vector2(-4,-4);number.Title.alignment=TextAnchor.MiddleLeft;number.Title.fontSize=12;
                            var input=number.NumberSpinner.GetComponent<RectTransform>();input.anchorMin=new Vector2(.55f,0);input.anchorMax=Vector2.one;input.offsetMin=new Vector2(30,4);input.offsetMax=new Vector2(-30,-4);
                            var value=number.NumberSpinner.textComponent;value.rectTransform.anchorMin=Vector2.zero;value.rectTransform.anchorMax=Vector2.one;value.rectTransform.offsetMin=new Vector2(2,2);value.rectTransform.offsetMax=new Vector2(-2,-2);value.alignment=TextAnchor.MiddleCenter;value.fontSize=14;value.resizeTextForBestFit=false;
                            var down=number.DownNumberButton.GetComponent<RectTransform>();down.anchorMin=new Vector2(.55f,0);down.anchorMax=new Vector2(.55f,1);down.offsetMin=new Vector2(0,4);down.offsetMax=new Vector2(28,-4);
                            var up=number.UpNumberButton.GetComponent<RectTransform>();up.anchorMin=new Vector2(1,0);up.anchorMax=Vector2.one;up.offsetMin=new Vector2(-28,4);up.offsetMax=new Vector2(0,-4);
                        }
                    }
                    foreach(var style in go.GetComponentsInChildren<StyleComponent>(true))if(style.enabled && style.GetComponentInParent<StyleRoot>()!=null)style.Refresh(true);
                    PrefabUtility.SaveAsPrefabAsset(go,path);
                } finally {PrefabUtility.UnloadPrefabContents(go);}
            }
        }
        private static void StylePanel(GameObject go){
            foreach(var image in go.GetComponentsInChildren<Image>(true)){
                if(image.name=="Logo" || image.name=="DreamyBanner" || image.sprite!=null && image.sprite.name=="DreamyBanner")image.gameObject.SetActive(false);
            }
            foreach(var t in go.GetComponentsInChildren<Text>(true))if(t.name=="DreamyBrand")t.gameObject.SetActive(false);
            var sidebarHeader=go.transform.Find("Canvas/Content/SideBar/Content/Header");if(sidebarHeader!=null){var image=sidebarHeader.GetComponent<Image>();if(image!=null)image.color=Theme.Surface;}
            var footer=go.transform.Find("Canvas/Content/SideBar/Content/Footer");if(footer!=null)footer.gameObject.SetActive(false);
            var controller=go.GetComponentInChildren<Dreamy.Debugger.Presentation.Scripts.DebuggerTabController>(true);if(controller!=null)controller.AboutTab=null;
            go.transform.Find("Canvas/Content/Main/HeaderPositionPlaceholder/HeaderContentsPlaceholder").GetComponent<LayoutElement>().preferredHeight=44;
            var mobile=go.GetComponentInChildren<Dreamy.Debugger.Presentation.UI.MobileMenuController>(true);
            if(mobile!=null){
                var menu=new SerializedObject(mobile);menu.FindProperty("_maxMenuWidth").floatValue=144;menu.ApplyModifiedPropertiesWithoutUndo();
                mobile.enabled=true;
                for(var i=mobile.OpenButton.onClick.GetPersistentEventCount()-1;i>=0;i--)
                    if(mobile.OpenButton.onClick.GetPersistentTarget(i)==mobile)UnityEditor.Events.UnityEventTools.RemovePersistentListener(mobile.OpenButton.onClick,i);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(mobile.OpenButton.onClick,mobile.Toggle);
                mobile.OpenButton.gameObject.SetActive(true);
            }
            var rail=go.transform.Find("Canvas/Content/SideBar").GetComponent<LayoutElement>();rail.minWidth=76;
            var responsive=go.GetComponentInChildren<ResponsiveEnable>(true);
            for(var i=0;i<responsive.Entries.Length;i++){
                var e=responsive.Entries[i];
                e.Components=e.Components.Where(c=>c!=mobile).ToArray();
                responsive.Entries[i]=e;
            }
            var resize=go.GetComponentInChildren<ResponsiveResize>(true);
            foreach(var e in resize.Elements){for(var i=0;i<e.SizeDefinitions.Length;i++){var d=e.SizeDefinitions[i];d.ElementWidth=d.ThresholdWidth>=800?116:88;e.SizeDefinitions[i]=d;}}
            var sizer=go.transform.Find("Canvas/Content/SideBar/Content/ViewportLayout/Sizer").GetComponent<LayoutElement>();sizer.minWidth=76;sizer.preferredWidth=88;
            var bar=go.transform.Find("Canvas/Content/Main/Header/MenuBar");
            var title=bar.Find("Title").GetComponent<Text>();title.fontStyle=FontStyle.Bold;title.fontSize=18;title.resizeTextForBestFit=true;title.resizeTextMinSize=12;title.resizeTextMaxSize=18;
            var titleLayout=title.GetComponent<LayoutElement>();titleLayout.minWidth=0;titleLayout.flexibleWidth=1;
            var logoNode=bar.Find("DreamyCornerLogo");
            if(logoNode==null){var logoObject=new GameObject("DreamyCornerLogo",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(LayoutElement));logoObject.transform.SetParent(bar,false);logoNode=logoObject.transform;}
            var logo=logoNode.GetComponent<Image>();logo.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(BannerPath);logo.color=Color.white;logo.preserveAspect=true;logo.raycastTarget=false;
            var logoLayout=logoNode.GetComponent<LayoutElement>();logoLayout.minWidth=72;logoLayout.preferredWidth=80;logoLayout.preferredHeight=36;logoLayout.flexibleWidth=0;
            logoNode.SetSiblingIndex(mobile.OpenButton.transform.GetSiblingIndex()+1);logoNode.gameObject.SetActive(true);
            var sideLogo=go.GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="Logo");
            if(sideLogo!=null){
                sideLogo.sprite=logo.sprite;sideLogo.color=Color.white;sideLogo.preserveAspect=true;sideLogo.raycastTarget=false;
                var rect=sideLogo.rectTransform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=new Vector2(112,36);rect.anchoredPosition=Vector2.zero;
                foreach(var style in sideLogo.GetComponents<StyleComponent>())style.enabled=false;
                foreach(var button in sideLogo.GetComponents<Button>())button.enabled=false;
                sideLogo.gameObject.SetActive(false);
                mobile.HeaderLogo=logoNode.gameObject;mobile.SidebarLogo=sideLogo.gameObject;
            }
        }
        public static void BuildHud(string path){
            var go=PrefabUtility.LoadPrefabContents(path);
            try {
                var hud=go.GetComponent<PerformanceHud>();var so=new SerializedObject(hud);
                var label=(Text)so.FindProperty("label").objectReferenceValue;
                var card=(RectTransform)label.transform.parent;
                card.sizeDelta=new Vector2(360,288);card.GetComponent<Image>().color=new Color(.065f,.07f,.08f,.94f);card.GetComponent<Image>().raycastTarget=false;
                var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
                if(go.GetComponent<GraphicRaycaster>()==null)go.AddComponent<GraphicRaycaster>();
                label.font=AssetDatabase.LoadAssetAtPath<Font>(Package+"/Runtime/Engine/ThirdParty/Graphy/Roboto-Bold.ttf");label.fontSize=22;label.supportRichText=true;label.color=Theme.Ivory;label.alignment=TextAnchor.UpperLeft;
                label.horizontalOverflow=HorizontalWrapMode.Overflow;label.verticalOverflow=VerticalWrapMode.Overflow;label.raycastTarget=false;
                Place(label.rectTransform,12,8,-54,54);
                var old=(Graphic)so.FindProperty("graph").objectReferenceValue;
                var graphObject=old.gameObject;
                var frames=old as Image;
                if(frames==null){UnityEngine.Object.DestroyImmediate(old);frames=graphObject.AddComponent<Image>();}
                frames.color=Color.white;frames.raycastTarget=false;
                var graphRenderer=graphObject.GetComponent<GraphyHudGraph>();if(graphRenderer==null)graphRenderer=graphObject.AddComponent<GraphyHudGraph>();
                var graphState=new SerializedObject(graphRenderer);graphState.FindProperty("graphShader").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Shader>(Package+"/Runtime/Engine/ThirdParty/Graphy/GraphMobile.shader");graphState.ApplyModifiedPropertiesWithoutUndo();
                Place(frames.rectTransform,12,108,-12,166);
                var summary=Label(card,"Summary",16);Place(summary.rectTransform,12,56,-12,100);
                var memory=Label(card,"Memory",16);Place(memory.rectTransform,12,178,-12,236);memory.lineSpacing=1.1f;
                var memoryGraph=GetPlot(card,"Memory history",HudHistoryGraphic.Plot.Memory);Place(memoryGraph.rectTransform,12,242,-12,276);
                var audio=Label(card,"Audio",16);Place(audio.rectTransform,12,288,-12,312);
                var audioGraph=GetPlot(card,"Audio level",HudHistoryGraphic.Plot.Audio);Place(audioGraph.rectTransform,12,318,-12,352);
                var gripNode=card.Find("Drag grip");
                if(gripNode==null){var grip=new GameObject("Drag grip",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(HudDragHandle));grip.transform.SetParent(card,false);gripNode=grip.transform;}
                var gripRect=(RectTransform)gripNode;gripRect.anchorMin=gripRect.anchorMax=gripRect.pivot=Vector2.one;gripRect.sizeDelta=new Vector2(44,44);gripRect.anchoredPosition=new Vector2(-4,-4);
                var gripImage=gripNode.GetComponent<Image>();gripImage.color=new Color(1,1,1,.035f);gripImage.raycastTarget=true;
                var hint=Label(gripNode,"Grip icon",22);hint.text="::";hint.alignment=TextAnchor.MiddleCenter;hint.color=Theme.Muted;hint.rectTransform.anchorMin=Vector2.zero;hint.rectTransform.anchorMax=Vector2.one;hint.rectTransform.offsetMin=hint.rectTransform.offsetMax=Vector2.zero;
                so.FindProperty("graph").objectReferenceValue=frames;so.FindProperty("card").objectReferenceValue=card;
                so.FindProperty("summary").objectReferenceValue=summary;so.FindProperty("memoryLabel").objectReferenceValue=memory;so.FindProperty("audioLabel").objectReferenceValue=audio;
                so.FindProperty("memoryGraph").objectReferenceValue=memoryGraph;so.FindProperty("audioGraph").objectReferenceValue=audioGraph;so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(go,path);
            } finally {PrefabUtility.UnloadPrefabContents(go);}
        }
        private static Text Label(Transform parent,string name,int size){
            var child=parent.Find(name);var t=child!=null?child.GetComponent<Text>():null;
            if(t==null){var go=new GameObject(name,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);t=go.GetComponent<Text>();}
            t.font=AssetDatabase.LoadAssetAtPath<Font>(Package+"/Runtime/Engine/ThirdParty/Graphy/Roboto-Regular.ttf");t.fontSize=size;t.supportRichText=true;t.color=Theme.Ivory;t.alignment=TextAnchor.UpperLeft;t.raycastTarget=false;return t;
        }
        private static HudHistoryGraphic Plot(string name,Transform parent,HudHistoryGraphic.Plot kind){var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(HudHistoryGraphic));go.transform.SetParent(parent,false);var p=go.GetComponent<HudHistoryGraphic>();p.Kind=kind;p.raycastTarget=false;return p;}
        private static HudHistoryGraphic GetPlot(Transform parent,string name,HudHistoryGraphic.Plot kind){var child=parent.Find(name);return child!=null?child.GetComponent<HudHistoryGraphic>():Plot(name,parent,kind);}
        private static void Place(RectTransform rect,float left,float top,float right,float bottom){rect.anchorMin=new Vector2(0,1);rect.anchorMax=new Vector2(1,1);rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(left,-bottom);rect.offsetMax=new Vector2(right,-top);}
    }
}
