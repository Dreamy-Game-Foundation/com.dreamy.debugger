using System;
using System.Collections.Generic;
using System.Linq;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation.Internal;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Presentation.UI.Controls.Data;
using UnityEngine;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;

namespace Dreamy.Debugger.Presentation.UI.Other
{
    /// <summary>A lazy, paged category overlay; pinning never invokes a game mutation.</summary>
    public sealed class PinnedCategoryView : MonoBehaviour
    {
        private const int PageSize = 6;
        private readonly List<OptionDefinition> options = new List<OptionDefinition>();
        private readonly List<OptionsControlBase> controls = new List<OptionsControlBase>();
        private RectTransform body;
        private Text caption, pageLabel;
        private Button expand, unpin, previous, next;
        private Action<OptionDefinition> remove;
        private string category;
        private int page;
        private bool expanded;
        private double nextRefresh;
        public bool IsExpanded => expanded;
        public int OptionCount => options.Count;
        public int ControlCount => controls.Count;

        internal void Configure(string name, Action<OptionDefinition> unpinOption)
        {
            category = name; remove = unpinOption;
            var root = (RectTransform)transform;
            root.sizeDelta = new Vector2(248, 40);
            var image = gameObject.AddComponent<Image>(); image.sprite = PresentationAssets.Load<Sprite>("DreamyDebugger/Theme/Panel");
            image.type = Image.Type.Sliced; var color = Theme.Surface; color.a = .92f; image.color = color; image.raycastTarget = false;
            var size = gameObject.AddComponent<LayoutElement>(); size.preferredWidth = size.minWidth = 248; size.preferredHeight = 40;
            expand = MakeButton(root,"Expand category",0,0,204,40,"",ToggleExpanded);
            caption = MakeText(expand.transform,"Summary",name,12,0,188,40);
            caption.fontSize = 13; caption.resizeTextForBestFit = true; caption.resizeTextMinSize = 11; caption.resizeTextMaxSize = 13;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            unpin = MakeButton(root,"Unpin category",208,0,40,40,"×",()=> { foreach(var option in options.ToArray())remove(option); });
            body = Node(root,"Expanded controls",8,44,232,0); body.gameObject.SetActive(false);
            previous = MakeButton(root,"Previous",8,0,44,36,"‹",()=> { page--; Rebuild(); });
            next = MakeButton(root,"Next",196,0,44,36,"›",()=> { page++; Rebuild(); });
            pageLabel = MakeText(root,"Page","",60,0,128,36); pageLabel.alignment=TextAnchor.MiddleCenter;
            RefreshCaption(); Resize();
        }
        internal void Add(OptionDefinition option)
        {
            if(options.Contains(option))return;
            options.Add(option); RefreshCaption(); if(expanded)Rebuild();
        }
        internal bool Remove(OptionDefinition option)
        {
            options.Remove(option); if(options.Count==0)return true;
            page=Mathf.Min(page,(options.Count-1)/PageSize); RefreshCaption(); if(expanded)Rebuild();return false;
        }
        public void ToggleExpanded()
        {
            expanded=!expanded; page=0;
            if(expanded)Rebuild();else ClearControls();
            body.gameObject.SetActive(expanded); RefreshCaption(); Resize();
        }
        private void ClearControls()
        {
            foreach(var control in controls)if(control!=null){control.gameObject.SetActive(false);Destroy(control.gameObject);}
            controls.Clear();
            foreach(Transform child in body)if(child.GetComponent<OptionCard>()!=null){child.gameObject.SetActive(false);Destroy(child.gameObject);}
        }
        private void Rebuild()
        {
            ClearControls();
            // Cards stay atomic: a resource's balance, amount and confirmed action share a page.
            var completeCards = options.Where(o => o.Card != null)
                .GroupBy(o => o.Card.Id).Where(IsCompleteCard)
                .Select(group => group.Key).ToHashSet();
            var units = options.GroupBy(option => option.Card != null && completeCards.Contains(option.Card.Id)
                ? (object)("card:" + option.Card.Id) : option).ToArray();
            page=Mathf.Clamp(page,0,Mathf.Max(0,(units.Length-1)/PageSize));
            var y=0f;
            foreach(var unit in units.Skip(page*PageSize).Take(PageSize))
            {
                var members=unit.ToArray(); OptionCard card=null;
                if(members[0].Card!=null && completeCards.Contains(members[0].Card.Id))
                {
                    var prefab=PresentationAssets.Load<OptionCard>("DreamyDebugger/OptionCard");
                    if(prefab!=null){card=Instantiate(prefab,body,false);card.Configure(members[0].Card);}
                }
                var height=card!=null?208:52;
                foreach(var option in members)
                {
                    var control=OptionControlFactory.CreateControl(option); if(control==null)continue;
                    controls.Add(control);
                    if(card!=null)card.Attach(control);
                    else {control.transform.SetParent(body,false);Place((RectTransform)control.transform,0,y,232,height);}
                }
                if(card!=null)Place((RectTransform)card.transform,0,y,232,height);
                y+=height+6;
            }
            body.sizeDelta=new Vector2(232,y);
            var footer=44+y;
            Place((RectTransform)previous.transform,8,footer,44,36);Place((RectTransform)next.transform,196,footer,44,36);Place(pageLabel.rectTransform,60,footer,128,36);
            previous.interactable=page>0;next.interactable=(page+1)*PageSize<units.Length;
            pageLabel.text=(page+1)+" / "+Mathf.Max(1,Mathf.CeilToInt(units.Length/(float)PageSize));
            Resize();
        }
        private static bool IsCompleteCard(IGrouping<string, OptionDefinition> group)
        {
            var metadata = group.First().Card;
            var roles = group.Select(option => option.Card.Role).ToArray();
            if (group.Any(option => option.Card.Kind != metadata.Kind || option.Card.Title != metadata.Title)
                || roles.Distinct().Count() != roles.Length) return false;
            return metadata.Kind == CheatCardKind.Resource
                ? roles.Length == 3 && roles.Contains(CheatCardRole.Balance)
                    && roles.Contains(CheatCardRole.Amount) && roles.Contains(CheatCardRole.Action)
                : roles.Length == 2 && roles.Contains(CheatCardRole.Owned) && roles.Contains(CheatCardRole.Action);
        }
        private void Resize()
        {
            var height=expanded?84+body.sizeDelta.y:40;
            var element=GetComponent<LayoutElement>();element.preferredHeight=height;
            ((RectTransform)transform).sizeDelta=new Vector2(248,height);
            previous.gameObject.SetActive(expanded);next.gameObject.SetActive(expanded);pageLabel.gameObject.SetActive(expanded);
            // The scroll parent bounds the expanded card on short/portrait viewports.
        }
        private void RefreshCaption()
        {
            var values=new List<string>();
            if(AccessGate.Authorized)
            {
                foreach(var option in options)
                {
                    if(values.Count==2)break;
                    if(!option.IsProperty || option.Property.CanWrite)continue;
                    try{var raw=option.Property.GetValue();var value=Convert.ToString(raw);if(raw is IConvertible && !(raw is string) && !(raw is bool) && !(raw is Enum)){var number=Convert.ToDouble(raw);if(Math.Abs(number)>=1000000)value=(number/1000000).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"M";else if(Math.Abs(number)>=10000)value=(number/1000).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+"K";}if(value!=null && value.Length>16)value=value.Substring(0,15)+"…";values.Add((option.Card!=null?option.Card.Title:option.Name)+" "+value);}catch{values.Add("Unavailable");}
                }
            }
            caption.text=(expanded?"▾ ":"▸ ")+category+(values.Count>0?" · "+string.Join(" · ",values):" · "+options.Count);
        }
        private void Update()
        {
            if(Time.realtimeSinceStartupAsDouble<nextRefresh)return;nextRefresh=Time.realtimeSinceStartupAsDouble+.25;
            RefreshCaption();foreach(var control in controls)if(control!=null)control.Refresh();
        }
        private static RectTransform Node(Transform parent,string name,float x,float y,float width,float height)
        { var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var rect=(RectTransform)go.transform;Place(rect,x,y,width,height);return rect; }
        private static void Place(RectTransform rect,float x,float y,float width,float height)
        { rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(width,height); }
        private static Text MakeText(Transform parent,string name,string value,float x,float y,float width,float height)
        { var rect=Node(parent,name,x,y,width,height);var text=rect.gameObject.AddComponent<Text>();text.font=PresentationAssets.Load<Font>("DreamyDebugger/Theme/Font");text.text=value;text.fontSize=14;text.color=Theme.Ivory;text.alignment=TextAnchor.MiddleLeft;text.raycastTarget=false;return text; }
        private static Button MakeButton(Transform parent,string name,float x,float y,float width,float height,string text,Action action)
        { var rect=Node(parent,name,x,y,width,height);var image=rect.gameObject.AddComponent<Image>();image.color=Color.clear;var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;button.onClick.AddListener(()=>action());if(!string.IsNullOrEmpty(text)){var label=MakeText(rect,"Caption",text,0,0,width,height);label.alignment=TextAnchor.MiddleCenter;}return button; }
    }
}
