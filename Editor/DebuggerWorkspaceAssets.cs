using System;
using System.Linq;
using Dreamy.Debugger.Foundation.UI;
using Dreamy.Debugger.Foundation.UI.Layout;
using Dreamy.Debugger.Presentation.UI;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Presentation.UI.Controls.Data;
using Dreamy.Debugger.Presentation.UI.Other;
using Dreamy.Debugger.Presentation.UI.Tabs;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
using Theme=Dreamy.Debugger.Foundation.UI.DebuggerPalette;
namespace Dreamy.Debugger.Editor
{
    /// <summary>Prefab authoring only. Keeps runtime controls, serialized listeners and scaler owners.</summary>
    public static class DebuggerWorkspaceAssets
    {
        public const string FontPath="Packages/com.dreamy.debugger/Runtime/Engine/ThirdParty/Graphy/Roboto-Regular.ttf";
        public static RectTransform Node(Transform parent,string name,params Type[] types) {
            var old=parent.Find(name);var go=old!=null?old.gameObject:new GameObject(name,typeof(RectTransform));
            if(old==null)go.transform.SetParent(parent,false);go.layer=parent.gameObject.layer;
            foreach(var type in types)if(go.GetComponent(type)==null)go.AddComponent(type);
            return (RectTransform)go.transform;
        }
        public static void Stretch(RectTransform rect,float left=0,float top=0,float right=0,float bottom=0) {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=new Vector2(left,bottom);rect.offsetMax=new Vector2(-right,-top);}
        public static void Top(RectTransform rect,float left,float top,float right,float height) {rect.anchorMin=new Vector2(0,1);rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,1);rect.offsetMin=new Vector2(left,-top-height);rect.offsetMax=new Vector2(-right,-top);}
        public static LayoutElement Size(GameObject go,float width=-1,float height=-1,float minWidth=0) {var e=go.GetComponent<LayoutElement>()??go.AddComponent<LayoutElement>();e.preferredWidth=width;e.preferredHeight=height;e.minWidth=minWidth;e.layoutPriority=10;return e;}
        public static Text Text(Transform parent,string name,string value,int size=14) {var r=Node(parent,name,typeof(Text));var t=r.GetComponent<Text>();t.text=value;t.font=AssetDatabase.LoadAssetAtPath<Font>(FontPath);t.fontSize=size;t.color=Theme.Ivory;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
        public static Image Box(Transform parent,string name,Color color,bool hit=false) {var r=Node(parent,name,typeof(Image));var image=r.GetComponent<Image>();image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=hit;return image;}
        public static Button Button(Transform parent,string name,string caption,float width=100) {var image=Box(parent,name,Theme.Control,true);var button=image.GetComponent<Button>()??image.gameObject.AddComponent<Button>();button.targetGraphic=image;var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.88f,.88f,.88f);colors.pressedColor=new Color(.65f,.65f,.65f);button.colors=colors;Size(button.gameObject,width,44);var t=Text(button.transform,"Label",caption);t.alignment=TextAnchor.MiddleCenter;Stretch(t.rectTransform,8,0,8,0);return button;}
        public static InputField Input(Transform parent,string name,string hint) {var image=Box(parent,name,Theme.Control,true);var field=image.GetComponent<InputField>()??image.gameObject.AddComponent<InputField>();var text=Text(field.transform,"Text","");Stretch(text.rectTransform,12,4,12,4);var placeholder=Text(field.transform,"Placeholder",hint);placeholder.color=Theme.Muted;Stretch(placeholder.rectTransform,12,4,12,4);field.textComponent=text;field.placeholder=placeholder;field.targetGraphic=image;return field;}
        public static Toggle Toggle(Transform parent,string name,string caption) {var image=Box(parent,name,Theme.Control,true);var toggle=image.GetComponent<Toggle>()??image.gameObject.AddComponent<Toggle>();var on=Box(image.transform,"Selected",Theme.Selected);Stretch(on.rectTransform);var t=Text(image.transform,"Label",caption);t.alignment=TextAnchor.MiddleCenter;Stretch(t.rectTransform,8,0,8,0);toggle.targetGraphic=image;toggle.graphic=on;Size(toggle.gameObject,88,44);return toggle;}
        public static Dropdown Dropdown(Transform parent,string name) {
            var image=Box(parent,name,Theme.Control,true);var dropdown=image.GetComponent<Dropdown>()??image.gameObject.AddComponent<Dropdown>();
            var caption=Text(image.transform,"Caption","All categories");Stretch(caption.rectTransform,10,0,24,0);dropdown.captionText=caption;dropdown.targetGraphic=image;
            var arrow=Text(image.transform,"Arrow","v",12);arrow.alignment=TextAnchor.MiddleRight;Stretch(arrow.rectTransform,0,0,10,0);
            var template=Node(image.transform,"Template",typeof(Image),typeof(ScrollRect));template.GetComponent<Image>().color=Theme.Surface;
            template.anchorMin=Vector2.zero;template.anchorMax=new Vector2(1,0);template.pivot=new Vector2(.5f,1);template.anchoredPosition=Vector2.zero;template.sizeDelta=new Vector2(0,200);
            var viewport=Node(template,"Viewport",typeof(RectMask2D));Stretch(viewport,4,4,4,4);
            var content=Node(viewport,"Content",typeof(VerticalLayoutGroup),typeof(ContentSizeFitter));content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            content.GetComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var item=Toggle(content,"Item","All categories");Size(item.gameObject,-1,44);dropdown.itemText=item.GetComponentInChildren<Text>();
            var scroll=template.GetComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            dropdown.template=template;template.gameObject.SetActive(false);Size(dropdown.gameObject,160,44,100);return dropdown;
        }
        public static void BuildWordmark(string path) {
            if(AssetDatabase.LoadAssetAtPath<Sprite>(path)!=null)return;
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(DebuggerRedesignAssets.BannerPath);
            var sprite=Sprite.Create(texture,new Rect(texture.width*.08f,texture.height*.33f,texture.width*.84f,texture.height*.46f),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect);
            sprite.name="Dreamy wordmark";AssetDatabase.CreateAsset(sprite,path);
        }
        public static void BuildTaskCard(string path) {
            var root=new GameObject("Task card",typeof(RectTransform),typeof(Image),typeof(VerticalLayoutGroup),typeof(LayoutElement),typeof(OptionCard));root.layer=5;
            try {
                var image=root.GetComponent<Image>();image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");image.type=Image.Type.Sliced;image.color=Theme.Surface;image.raycastTarget=false;
                var layout=root.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(12,12,12,12);layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
                var element=Size(root,320,-1,260);element.flexibleWidth=1;
                var card=root.GetComponent<OptionCard>();card.Title=Text(root.transform,"Title","Resource",18);card.Title.fontStyle=FontStyle.Bold;Size(card.Title.gameObject,-1,28);
                card.BalanceSlot=Slot(root.transform,"Balance",60);card.OwnedSlot=Slot(root.transform,"Ownership",44);
                card.OwnedLabel=Text(card.OwnedSlot,"Status","Not owned",14);Stretch(card.OwnedLabel.rectTransform,100,0,8,0);card.OwnedLabel.alignment=TextAnchor.MiddleRight;Size(card.OwnedLabel.gameObject).ignoreLayout=true;card.OwnedLabel.gameObject.SetActive(false);
                var actions=Node(root.transform,"Actions",typeof(FlowLayoutGroup));var flow=actions.GetComponent<FlowLayoutGroup>();flow.Spacing=8;flow.ChildForceExpandWidth=true;
                card.AmountSlot=Slot(actions,"Amount",68);Size(card.AmountSlot.gameObject,220,68,150);
                card.ActionSlot=Slot(actions,"Action",48);Size(card.ActionSlot.gameObject,128,48,110);
                card.BalanceSlot.gameObject.SetActive(false);card.OwnedSlot.gameObject.SetActive(false);card.AmountSlot.gameObject.SetActive(false);card.ActionSlot.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            } finally {Object.DestroyImmediate(root);}
        }
        private static RectTransform Slot(Transform parent,string name,float height) {var slot=Node(parent,name,typeof(VerticalLayoutGroup));var layout=slot.GetComponent<VerticalLayoutGroup>();layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=true;Size(slot.gameObject,-1,height);return slot;}
        public static void StyleOptions(GameObject go,string cardPath) {
            var tab=go.GetComponent<OptionsTabController>();if(tab==null)return;
            var body=go.transform.Find("OptionsContent");var toolbar=Node(body,"Workspace toolbar");Top(toolbar,16,12,16,100);
            tab.SearchField=Input(toolbar,"Search","Search options");Top((RectTransform)tab.SearchField.transform,0,0,0,44);
            var filters=Node(toolbar,"Filters",typeof(HorizontalLayoutGroup));Top(filters,0,52,0,44);var row=filters.GetComponent<HorizontalLayoutGroup>();row.spacing=8;row.childControlHeight=true;row.childControlWidth=true;row.childForceExpandWidth=false;
            tab.CategoryFilter=Dropdown(filters,"Category");tab.CategoryFilter.GetComponent<LayoutElement>().flexibleWidth=1;
            tab.PinnedOnly=Toggle(filters,"Pinned","Pinned");
            tab.ResultCount=Text(filters,"Count","0 options",12);tab.ResultCount.alignment=TextAnchor.MiddleRight;Size(tab.ResultCount.gameObject,56,44);
            tab.TaskCardPrefab=AssetDatabase.LoadAssetAtPath<OptionCard>(cardPath);
            var viewport=(RectTransform)body.Find("Viewport");viewport.offsetMax=new Vector2(viewport.offsetMax.x,-124);
            var scroll=body.GetComponent<ScrollRect>();if(scroll.verticalScrollbar!=null){var r=(RectTransform)scroll.verticalScrollbar.transform;r.offsetMax=new Vector2(r.offsetMax.x,-124);}
            var layout=tab.ContentContainer.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(16,16,8,24);layout.spacing=16;
            var empty=tab.NoOptionsNotice.GetComponent<Text>();if(empty!=null){empty.text="No matching options";empty.fontSize=16;}
        }
        public static void StyleCategory(GameObject go) {
            var category=go.GetComponent<CategoryGroup>();if(category==null)return;
            var header=category.Header.transform.parent;category.CollapseButton=header.GetComponent<Button>()??header.gameObject.AddComponent<Button>();
            var target=header.GetComponent<Image>()??header.gameObject.AddComponent<Image>();target.color=Theme.Background;target.raycastTarget=true;category.CollapseButton.targetGraphic=target;
            var oldLayout=header.GetComponent<VerticalLayoutGroup>();if(oldLayout!=null)Object.DestroyImmediate(oldLayout);
            var oldFit=header.GetComponent<ContentSizeFitter>();if(oldFit!=null)Object.DestroyImmediate(oldFit);
            Size(header.gameObject).ignoreLayout=true;var rect=(RectTransform)header;Top(rect,0,0,0,48);category.Header.fontSize=18;category.Header.fontStyle=FontStyle.Bold;category.Header.alignment=TextAnchor.MiddleLeft;Stretch(category.Header.rectTransform,0,0,44,0);
            var oldIndicator=header.Find("Indicator");if(oldIndicator!=null)oldIndicator.gameObject.SetActive(false);
            category.CollapseIndicator=Text(header,"Collapse","-",18);category.CollapseIndicator.alignment=TextAnchor.MiddleRight;Stretch(category.CollapseIndicator.rectTransform,0,0,12,0);
            var flow=category.Container.GetComponent<FlowLayoutGroup>();flow.Spacing=12;flow.padding=new RectOffset(0,0,56,4);flow.ChildForceExpandWidth=true;
        }
        public static void StyleTabButton(GameObject go) {
            var tab=go.GetComponent<DebugTabButton>();if(tab==null)return;
            Size(go,-1,56);tab.TitleText.fontSize=14;tab.TitleText.alignment=TextAnchor.MiddleLeft;Stretch(tab.TitleText.rectTransform,48,4,12,4);
            var icon=(RectTransform)tab.IconStyleComponent.transform;icon.anchorMin=icon.anchorMax=new Vector2(0,.5f);icon.pivot=new Vector2(.5f,.5f);icon.sizeDelta=new Vector2(22,22);icon.anchoredPosition=new Vector2(24,0);
        }
        public static void StylePanel(GameObject go) {
            var mobile=go.GetComponentInChildren<MobileMenuController>(true);var state=new SerializedObject(mobile);state.FindProperty("_maxMenuWidth").floatValue=184;state.ApplyModifiedPropertiesWithoutUndo();
            go.transform.Find("Canvas/Content/Main/HeaderPositionPlaceholder/HeaderContentsPlaceholder").GetComponent<LayoutElement>().preferredHeight=56;
            var bar=go.transform.Find("Canvas/Content/Main/Header/MenuBar");var layout=bar.GetComponent<HorizontalLayoutGroup>();layout.spacing=4;
            Size(mobile.OpenButton.gameObject,48,56,48);Size(bar.Find("CloseButton").gameObject,48,56,48);
            var header=go.transform.Find("Canvas/Content/SideBar/Content/Header");Size(header.gameObject,-1,56);header.GetComponent<Image>().color=Theme.Brand;
            var title=bar.Find("Title").GetComponent<Text>();title.fontSize=20;title.resizeTextForBestFit=true;title.resizeTextMinSize=14;title.resizeTextMaxSize=20;title.horizontalOverflow=HorizontalWrapMode.Overflow;
            var wordmark=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/__DreamyDebuggerVisualAuthoring/Prefabs/Options/DreamyWordmark.asset");
            foreach(var logo in new[]{mobile.HeaderLogo,mobile.SidebarLogo})if(logo!=null){logo.GetComponent<Image>().sprite=wordmark;logo.GetComponent<Image>().type=Image.Type.Simple;}
            Size(mobile.HeaderLogo,96,40,72);Size(mobile.SidebarLogo,112,40,80);
            var responsive=go.GetComponentInChildren<ResponsiveResize>(true);if(responsive!=null)responsive.enabled=false;
            var rail=go.transform.Find("Canvas/Content/SideBar/Content/ViewportLayout/Sizer").GetComponent<LayoutElement>();rail.minWidth=184;rail.preferredWidth=184;
            var scrim=Box(mobile.Content,"Drawer scrim",new Color(0,0,0,.55f),true);Stretch(scrim.rectTransform);Size(scrim.gameObject).ignoreLayout=true;
            mobile.Scrim=scrim.GetComponent<Button>()??scrim.gameObject.AddComponent<Button>();mobile.Scrim.targetGraphic=scrim;mobile.Scrim.transition=Selectable.Transition.None;scrim.gameObject.SetActive(false);
            mobile.Menu.SetAsLastSibling();mobile.DrawerClose=Button(header,"Close drawer","×",44);mobile.DrawerClose.gameObject.SetActive(false);
            // Tab canvases intentionally sort above the shell. Drawer and scrim must
            // also sit above those canvases, not merely be later siblings.
            var shell=mobile.Content.GetComponentInParent<Canvas>();
            foreach(var item in new[]{scrim.gameObject,mobile.Menu.gameObject}){
                var canvas=item.GetComponent<Canvas>();if(canvas==null)canvas=item.AddComponent<Canvas>();
                canvas.overrideSorting=true;canvas.sortingLayerID=shell.sortingLayerID;
                canvas.sortingOrder=shell.sortingOrder+(item==scrim.gameObject?2:3);
                if(item.GetComponent<GraphicRaycaster>()==null)item.AddComponent<GraphicRaycaster>();
            }
            Size(mobile.Menu.gameObject,184).ignoreLayout=true;
        }
        public static void StylePin(GameObject go) {
            var pin=go.GetComponent<PinEntryControl>();if(pin==null)return;
            var layout=go.transform.Find("Layout");var grid=go.GetComponentInChildren<GridLayoutGroup>(true);grid.cellSize=new Vector2(72,56);grid.spacing=new Vector2(8,8);
            pin.LengthStatus=Text(layout,"PIN length","",12);pin.LengthStatus.alignment=TextAnchor.MiddleCenter;Size(pin.LengthStatus.gameObject,-1,24);pin.LengthStatus.transform.SetSiblingIndex(2);
            pin.PromptText.fontSize=20;
            var panel=Box(layout,"Panel backing",Theme.Surface,true);Stretch(panel.rectTransform,-16,-20,-16,-20);Size(panel.gameObject).ignoreLayout=true;panel.transform.SetAsFirstSibling();
            var top=Box(panel.transform,"Brand edge",Theme.Brand);Top(top.rectTransform,0,0,0,3);
            foreach(var button in pin.NumberButtons){
                var flash=button.GetComponent<FlashGraphic>();if(flash!=null)flash.enabled=false;
                var ring=button.transform.Find("Ring");if(ring!=null)ring.gameObject.SetActive(false);
                var fill=button.transform.Find("Fill");if(fill!=null){var image=fill.GetComponent<Image>();var style=fill.GetComponent<StyleComponent>();if(style!=null)style.enabled=false;image.color=Color.white;image.sprite=AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");image.type=Image.Type.Sliced;Stretch((RectTransform)fill,2,2,2,2);button.targetGraphic=image;}
                foreach(var text in button.GetComponentsInChildren<Text>(true))if(text.name=="Letters")text.gameObject.SetActive(false);else{text.fontSize=22;text.alignment=TextAnchor.MiddleCenter;Stretch(text.rectTransform);}
            }
        }
        public static void StyleConfirmation(GameObject go) {
            var prompt=go.GetComponent<ConfirmationPrompt>();if(prompt==null)return;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=1;
            if(go.GetComponent<DebugRetinaScaler>()==null)go.AddComponent<DebugRetinaScaler>();
            if(go.GetComponent<Dreamy.Debugger.Presentation.UI.Other.ConfigureCanvasFromSettings>()==null)go.AddComponent<Dreamy.Debugger.Presentation.UI.Other.ConfigureCanvasFromSettings>();
            var dialog=(RectTransform)go.transform.Find("SafeArea/Dialog");dialog.sizeDelta=new Vector2(340,220);dialog.GetComponent<Image>().color=Theme.Surface;
            var parent=dialog.parent;var group=parent.GetComponent<HorizontalLayoutGroup>()??parent.gameObject.AddComponent<HorizontalLayoutGroup>();group.padding=new RectOffset(16,16,16,16);group.childAlignment=TextAnchor.MiddleCenter;group.childControlWidth=true;group.childControlHeight=false;group.childForceExpandWidth=false;group.childForceExpandHeight=false;Size(dialog.gameObject,340,220);
            prompt.Title.fontSize=20;prompt.Title.fontStyle=FontStyle.Bold;Top(prompt.Title.rectTransform,20,24,20,72);
            var description=Text(dialog,"Description","Uses the registered game action.",14);description.color=Theme.Muted;Top(description.rectTransform,20,100,20,48);prompt.Details=description;
            var cr=(RectTransform)prompt.Confirm.transform;cr.anchorMin=new Vector2(.52f,0);cr.anchorMax=new Vector2(1,0);cr.pivot=new Vector2(.5f,0);cr.offsetMin=new Vector2(0,20);cr.offsetMax=new Vector2(-20,68);
            var ar=(RectTransform)prompt.Cancel.transform;ar.anchorMin=Vector2.zero;ar.anchorMax=new Vector2(.48f,0);ar.pivot=new Vector2(.5f,0);ar.offsetMin=new Vector2(20,20);ar.offsetMax=new Vector2(0,68);
            prompt.Confirm.GetComponent<Image>().color=Theme.Brand;prompt.Cancel.GetComponent<Image>().color=Theme.Control;
            foreach(var text in prompt.Confirm.GetComponentsInChildren<Text>(true)){text.text="Confirm";text.fontSize=16;}foreach(var text in prompt.Cancel.GetComponentsInChildren<Text>(true)){text.text="Cancel";text.fontSize=16;}
            var fade=Box(go.transform,"Modal scrim",new Color(0,0,0,.65f),true);Stretch(fade.rectTransform);fade.transform.SetAsFirstSibling();
            var edge=Box(dialog,"Brand edge",Theme.Brand);Top(edge.rectTransform,0,0,0,3);
        }

        public static void StyleConsole(GameObject go) {
            var tab=go.GetComponent<ConsoleTabController>();if(tab==null)return;
            var layout=go.transform.Find("Layout");var toolbar=Node(layout,"Console toolbar",typeof(VerticalLayoutGroup));toolbar.SetAsFirstSibling();
            Size(toolbar.gameObject).flexibleHeight=0;
            var v=toolbar.GetComponent<VerticalLayoutGroup>();v.padding=new RectOffset(12,12,12,8);v.spacing=8;v.childControlWidth=true;v.childControlHeight=true;v.childForceExpandWidth=true;v.childForceExpandHeight=false;
            tab.FilterBarContainer.transform.SetParent(toolbar,false);Size(tab.FilterBarContainer,-1,44);
            Stretch((RectTransform)tab.FilterField.transform,0,0,0,0);tab.FilterField.image.color=Theme.Control;
            if(tab.FilterField.placeholder is Text placeholder){placeholder.text="Search logs";placeholder.fontSize=14;}
            tab.FilterToggle.isOn=true;tab.FilterToggle.gameObject.SetActive(false);
            var row=Node(toolbar,"Severity filters",typeof(FlowLayoutGroup));var flow=row.GetComponent<FlowLayoutGroup>();flow.Spacing=8;flow.ChildForceExpandWidth=true;
            var toggles=new[]{tab.ToggleErrors,tab.ToggleWarnings,tab.ToggleInfo};var widths=new[]{80,88,72};
            for(var j=0;j<toggles.Length;j++){var toggle=toggles[j];toggle.transform.SetParent(row,false);Size(toggle.gameObject,widths[j],44);var group=toggle.GetComponent<HorizontalLayoutGroup>();if(group!=null){group.padding=new RectOffset(8,8,4,4);group.spacing=4;}}
            tab.CollapseToggle=Toggle(row,"Collapse","Collapse");Size(tab.CollapseToggle.gameObject,100,44);
            var clear=go.transform.Find("Header/ConsoleHeaderContent/Clear");if(clear==null)clear=row.Find("Clear");
            clear.SetParent(row,false);Size(clear.gameObject,64,44);var icon=clear.Find("Sprite");if(icon!=null)icon.gameObject.SetActive(false);var label=Text(clear,"Label","Clear",13);label.alignment=TextAnchor.MiddleCenter;Stretch(label.rectTransform);
            tab.DetailPane=(RectTransform)tab.StackTraceScrollRect.transform.parent;
            var logState=new SerializedObject(tab.ConsoleLogControl);tab.ListPane=(RectTransform)((ScrollRect)logState.FindProperty("_consoleScrollRect").objectReferenceValue).transform;
            tab.DetailBack=Button(tab.DetailPane,"Back to logs","Back",80);Top((RectTransform)tab.DetailBack.transform,12,8,200,44);
            var copy=(RectTransform)tab.CopyToClipboardContainer.transform;Top(copy,130,8,12,44);Size(tab.CopyToClipboardButton,88,44);
            var copyIcon=tab.CopyToClipboardButton.transform.Find("Icon");if(copyIcon!=null)copyIcon.gameObject.SetActive(false);var copyLabel=Text(tab.CopyToClipboardButton.transform,"Label","Copy",14);copyLabel.alignment=TextAnchor.MiddleCenter;Stretch(copyLabel.rectTransform);
            var detail=(RectTransform)tab.StackTraceScrollRect.transform;Stretch(detail,12,62,12,12);tab.StackTraceText.fontSize=14;
            var latest=tab.ListPane.Find("ScrollToBottomButton");if(latest!=null){
                var rect=(RectTransform)latest;rect.sizeDelta=new Vector2(112,44);rect.anchoredPosition=new Vector2(-8,8);
                var old=latest.Find("Icon");if(old!=null)old.gameObject.SetActive(false);
                tab.ConsoleLogControl.NewEntriesText=Text(latest,"New entries","Latest",12);tab.ConsoleLogControl.NewEntriesText.alignment=TextAnchor.MiddleCenter;Stretch(tab.ConsoleLogControl.NewEntriesText.rectTransform);
                var click=latest.GetComponent<Button>().onClick;
                for(var index=click.GetPersistentEventCount()-1;index>=0;index--)if(click.GetPersistentTarget(index)==tab.ConsoleLogControl && click.GetPersistentMethodName(index)=="JumpToLatest")UnityEditor.Events.UnityEventTools.RemovePersistentListener(click,index);
                UnityEditor.Events.UnityEventTools.AddPersistentListener(click,tab.ConsoleLogControl.JumpToLatest);
            }
        }
        public static void StyleConsoleEntry(GameObject go) {
            var entry=go.GetComponent<ConsoleEntryView>();if(entry==null)return;
            ((RectTransform)go.transform).SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,56);
            Size(go,-1,56);
            entry.Timestamp=Text(go.transform,"Timestamp","—",12);entry.Timestamp.color=Theme.Muted;var time=entry.Timestamp.rectTransform;time.anchorMin=new Vector2(0,0);time.anchorMax=new Vector2(0,1);time.offsetMin=new Vector2(8,0);time.offsetMax=new Vector2(72,0);
            var icon=(RectTransform)entry.ImageStyle.transform;icon.anchorMin=icon.anchorMax=new Vector2(0,.5f);icon.sizeDelta=new Vector2(16,16);icon.anchoredPosition=new Vector2(86,0);
            Stretch(entry.Message.rectTransform,104,8,40,8);entry.Message.fontSize=14;entry.Message.verticalOverflow=VerticalWrapMode.Truncate;entry.StackTrace.gameObject.SetActive(false);
        }

        public static void StyleInfoBlock(GameObject go) {
            var block=go.GetComponent<InfoBlock>();if(block==null)return;
            var layout=go.GetComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(16,16,12,16);layout.spacing=8;layout.childControlWidth=true;layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var header=Node(go.transform,"Section header",typeof(HorizontalLayoutGroup));header.SetAsFirstSibling();var row=header.GetComponent<HorizontalLayoutGroup>();row.childControlHeight=true;row.childControlWidth=true;row.childForceExpandWidth=false;row.spacing=8;
            block.Title.transform.SetParent(header,false);block.Title.fontSize=18;block.Title.fontStyle=FontStyle.Bold;Size(block.Title.gameObject,-1,44).flexibleWidth=1;
            block.CopyButton=Button(header,"Copy section","Copy",64);
            block.Rows=Node(go.transform,"Rows",typeof(VerticalLayoutGroup));var rows=block.Rows.GetComponent<VerticalLayoutGroup>();rows.spacing=4;rows.childControlHeight=true;rows.childControlWidth=true;rows.childForceExpandWidth=true;rows.childForceExpandHeight=false;
            var template=Node(go.transform,"Row template",typeof(HorizontalLayoutGroup),typeof(InfoRow));var group=template.GetComponent<HorizontalLayoutGroup>();group.childControlHeight=true;group.childControlWidth=true;group.childForceExpandWidth=false;group.childForceExpandHeight=false;group.spacing=12;group.childAlignment=TextAnchor.UpperLeft;
            block.RowPrefab=template.GetComponent<InfoRow>();block.RowPrefab.Key=Text(template,"Key","Key",14);block.RowPrefab.Value=Text(template,"Value","Value",14);block.RowPrefab.Key.color=Theme.Muted;
            block.RowPrefab.Key.supportRichText=block.RowPrefab.Value.supportRichText=false;
            Size(block.RowPrefab.Key.gameObject,132,-1,80).flexibleWidth=.6f;Size(block.RowPrefab.Value.gameObject,220,-1,100).flexibleWidth=1;template.gameObject.SetActive(false);
        }

    }
}
