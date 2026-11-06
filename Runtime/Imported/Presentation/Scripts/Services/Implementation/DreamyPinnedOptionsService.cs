namespace Dreamy.Debugger.Presentation.Services.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using Internal;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.Service;
    using UI.Controls;
    using UI.Other;
    using UnityEngine;

    [Service(typeof (IPinnedUIService))]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Dreamy.Debugger.Presentation.Services.Implementation", sourceAssembly: null, sourceClassName: "PinnedUIServiceImpl")]
    public class DreamyPinnedOptionsService : ServiceBase<IPinnedUIService>, IPinnedUIService
    {
        private readonly HashSet<OptionDefinition> _pinnedObjects = new HashSet<OptionDefinition>();
        private readonly Dictionary<string, PinnedCategoryView> categories = new Dictionary<string, PinnedCategoryView>();
        private UnityEngine.RectTransform chipContent, chipViewport;
        private int categoryPage;
        private UnityEngine.UI.Button nextCategories;
        private bool panelVisible;
        private bool _queueRefresh, restoring, restorePending=true, wasUnlocked;
        private void RestorePreferences(){restorePending=true;}
        private void RestoreSavedPins()
        {
            if(AccessGate.IsUnlocked==null || !AccessGate.IsUnlocked())return;
            restoring=true;
            try{
                foreach(var option in Service.Options.Options.ToArray()){
                    if(string.IsNullOrEmpty(option.StableId))continue;
                    var pinned=PreferenceBridge.Get("pin."+option.StableId,"0")=="1";
                    if(pinned && !_pinnedObjects.Contains(option))Pin(option);
                    else if(!pinned && _pinnedObjects.Contains(option))Unpin(option);
                }
            }finally{restoring=false;restorePending=false;}
        }
        private PinnedUIRoot _uiRoot;
        private IOptionsService sourceOptions;
        private IDebugService sourcePanel;

        public DockConsoleController DockConsoleController
        {
            get
            {
                if (_uiRoot == null)
                {
                    Load();
                }
                return _uiRoot.DockConsoleController;
            }
        }

        public event Action<OptionDefinition, bool> OptionPinStateChanged;
        public event Action<RectTransform> OptionsCanvasCreated;

        public bool IsProfilerPinned
        {
            get
            {
                if (_uiRoot == null)
                {
                    return false;
                }
                return _uiRoot.Profiler.activeSelf;
            }
            set
            {
                if (_uiRoot == null)
                {
                    Load();
                }
                _uiRoot.Profiler.SetActive(value);
            }
        }

        public void Pin(OptionDefinition obj, int order = -1)
        {
            if(AccessGate.IsUnlocked!=null && !AccessGate.IsUnlocked())return;
            if (_uiRoot == null)
            {
                Load();
            }

            if (_pinnedObjects.Contains(obj))
            {
                return;
            }

            _pinnedObjects.Add(obj);
            var key = obj.Category ?? "Default";
            if (!categories.TryGetValue(key, out var group))
            {
                EnsureChipViewport();
                var go = new GameObject("Pinned " + key, typeof(RectTransform));
                go.transform.SetParent(chipContent, false);
                group = go.AddComponent<PinnedCategoryView>();
                categories.Add(key, group);group.Configure(key, Unpin);
            }
            group.Add(obj);RefreshCategoryPage();
            OnPinnedStateChanged(obj, true);
        }

        public void Unpin(OptionDefinition obj)
        {
            if (!_pinnedObjects.Contains(obj))
            {
                return;
            }

            _pinnedObjects.Remove(obj);
            var key = obj.Category ?? "Default";
            if(categories.TryGetValue(key,out var group) && group.Remove(obj))
            {
                categories.Remove(key);group.gameObject.SetActive(false);Destroy(group.gameObject);
            }
            RefreshCategoryPage();
            OnPinnedStateChanged(obj, false);
        }

        private void OnPinnedStateChanged(OptionDefinition option, bool isPinned)
        {
            if(!restoring && !string.IsNullOrEmpty(option.StableId))PreferenceBridge.Set("pin."+option.StableId,isPinned?"1":"0");
            if (OptionPinStateChanged != null)
            {
                OptionPinStateChanged(option, isPinned);
            }
        }

        public void UnpinAll()
        {
            foreach(var option in _pinnedObjects.ToArray())Unpin(option);
        }

        public bool HasPinned(OptionDefinition option)
        {
            return _pinnedObjects.Contains(option);
        }

        protected override void Awake()
        {
            base.Awake();

            CachedTransform.SetParent(Hierarchy.Get("DreamyDebugger"));
            PreferenceBridge.Restored+=RestorePreferences;
            sourceOptions=Service.Options;sourceOptions.OptionsUpdated+=OnOptionsUpdated;
        }

        private void Load()
        {
            var prefab = PresentationAssets.Load<PinnedUIRoot>(PanelPaths.PinnedUIPrefabPath);

            if (prefab == null)
            {
                Debug.LogError("[DreamyDebugger.PinnedUI] Error loading ui prefab");
                return;
            }

            var instance = ObjectFactory.Instantiate(prefab);
            instance.CachedTransform.SetParent(CachedTransform, false);

            _uiRoot = instance;
            UpdateAnchors();
            sourcePanel=DreamyDebug.Instance;sourcePanel.PanelVisibilityChanged += OnDebugPanelVisibilityChanged;
            panelVisible=sourcePanel.IsDebugPanelVisible;

            if (OptionsCanvasCreated != null)
            {
                OptionsCanvasCreated(_uiRoot.Canvas.GetComponent<RectTransform>());
            }
        }

        private void UpdateAnchors()
        {
            // Setup alignment of Profiler/Options splitter
            switch (Settings.Instance.ProfilerAlignment)
            {
                case PinAlignment.BottomLeft:
                case PinAlignment.TopLeft:
                case PinAlignment.CenterLeft:
                    _uiRoot.Profiler.transform.SetSiblingIndex(0);
                    break;

                case PinAlignment.BottomRight:
                case PinAlignment.TopRight:
                case PinAlignment.CenterRight:
                    _uiRoot.Profiler.transform.SetSiblingIndex(1);
                    break;
            }

            // Setup alignment of Profiler vertical layout group
            switch (Settings.Instance.ProfilerAlignment)
            {
                case PinAlignment.TopRight:
                case PinAlignment.TopLeft:
                    _uiRoot.ProfilerVerticalLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                    break;

                case PinAlignment.BottomRight:
                case PinAlignment.BottomLeft:
                    _uiRoot.ProfilerVerticalLayoutGroup.childAlignment = TextAnchor.LowerCenter;
                    break;

                case PinAlignment.CenterLeft:
                case PinAlignment.CenterRight:
                    _uiRoot.ProfilerVerticalLayoutGroup.childAlignment = TextAnchor.MiddleCenter;
                    break;
            }

            _uiRoot.ProfilerHandleManager.SetAlignment(Settings.Instance.ProfilerAlignment);

            // Setup alignment of options flow layout group
            switch (Settings.Instance.OptionsAlignment)
            {
                case PinAlignment.BottomLeft: // OptionsBottomLeft
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.LowerLeft;
                    break;
                case PinAlignment.TopLeft:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.UpperLeft;
                    break;
                case PinAlignment.BottomRight:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.LowerRight;
                    break;
                case PinAlignment.TopRight:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.UpperRight;
                    break;
                case PinAlignment.BottomCenter:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.LowerCenter;
                    break;
                case PinAlignment.TopCenter:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.UpperCenter;
                    break;
                case PinAlignment.CenterLeft:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
                    break;
                case PinAlignment.CenterRight:
                    _uiRoot.OptionsLayoutGroup.childAlignment = TextAnchor.MiddleRight;
                    break;
            }
        }

        protected override void Update()
        {
            base.Update();
            if(chipViewport!=null)
            {
                var parent=(RectTransform)chipViewport.parent;
                var height=Mathf.Max(40,chipContent.rect.height);
                var size=new Vector2(Mathf.Min(264,parent.rect.width-24),Mathf.Min(height,Mathf.Min(320,parent.rect.height*.42f)));
                chipViewport.sizeDelta=size;
                chipViewport.gameObject.SetActive(!panelVisible && AccessGate.Authorized && categories.Count>0);
            }
            var unlocked=AccessGate.IsUnlocked!=null && AccessGate.IsUnlocked();
            if(unlocked && (!wasUnlocked || restorePending))RestoreSavedPins();
            wasUnlocked=unlocked;
            if (_queueRefresh)
            {
                _queueRefresh = false;
                Refresh();
            }
        }

        private void OnOptionsUpdated(object sender, EventArgs eventArgs)
        {
            // Check for removed options.
            var pinned = _pinnedObjects.ToList();
            restoring=true;

            foreach (var op in pinned)
            {
                if (!Service.Options.Options.Contains(op))
                {
                    Unpin(op);
                }
            }
            restoring=false;restorePending=true;
        }

        private void OnDebugPanelVisibilityChanged(bool isVisible)
        {
            panelVisible = isVisible;
            // Refresh bindings when debug panel is no longer visible
            if (!isVisible)
            {
                _queueRefresh = true;
            }
        }

        protected override void OnDestroy(){PreferenceBridge.Restored-=RestorePreferences;if(sourceOptions!=null)sourceOptions.OptionsUpdated-=OnOptionsUpdated;if(sourcePanel!=null)sourcePanel.PanelVisibilityChanged-=OnDebugPanelVisibilityChanged;base.OnDestroy();}
        private void EnsureChipViewport()
        {
            if(chipViewport!=null)return;
            var viewport=new GameObject("Pinned category viewport",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.RectMask2D),typeof(UnityEngine.UI.ScrollRect));
            chipViewport=(RectTransform)viewport.transform;chipViewport.SetParent(_uiRoot.Canvas.transform,false);
            chipViewport.anchorMin=chipViewport.anchorMax=chipViewport.pivot=new Vector2(1,0);chipViewport.anchoredPosition=new Vector2(-12,12);
            var image=viewport.GetComponent<UnityEngine.UI.Image>();image.color=Color.clear;image.raycastTarget=false;
            var content=new GameObject("Category chips",typeof(RectTransform),typeof(UnityEngine.UI.VerticalLayoutGroup),typeof(UnityEngine.UI.ContentSizeFitter));
            chipContent=(RectTransform)content.transform;chipContent.SetParent(chipViewport,false);chipContent.anchorMin=new Vector2(0,1);chipContent.anchorMax=Vector2.one;chipContent.pivot=new Vector2(.5f,1);chipContent.sizeDelta=Vector2.zero;
            var layout=content.GetComponent<UnityEngine.UI.VerticalLayoutGroup>();layout.spacing=6;layout.childControlWidth=true;layout.childForceExpandWidth=true;layout.childControlHeight=true;layout.childForceExpandHeight=false;
            content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            var scroll=viewport.GetComponent<UnityEngine.UI.ScrollRect>();scroll.viewport=chipViewport;scroll.content=chipContent;scroll.horizontal=false;scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
            var more=new GameObject("More categories",typeof(RectTransform),typeof(UnityEngine.UI.Image),typeof(UnityEngine.UI.Button),typeof(UnityEngine.UI.LayoutElement));more.transform.SetParent(chipContent,false);
            more.GetComponent<UnityEngine.UI.Image>().color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Control;
            more.GetComponent<UnityEngine.UI.LayoutElement>().preferredHeight=36;
            nextCategories=more.GetComponent<UnityEngine.UI.Button>();nextCategories.onClick.AddListener(()=>{categoryPage++;RefreshCategoryPage();});
            var label=new GameObject("Caption",typeof(RectTransform),typeof(UnityEngine.UI.Text));label.transform.SetParent(more.transform,false);var rect=(RectTransform)label.transform;rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var text=label.GetComponent<UnityEngine.UI.Text>();text.font=PresentationAssets.Load<Font>("DreamyDebugger/Theme/Font");text.text="More categories ›";text.fontSize=13;text.color=Dreamy.Debugger.Foundation.UI.DebuggerPalette.Ivory;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
        }
        private void RefreshCategoryPage()
        {
            if(nextCategories==null)return;
            var groups=categories.Values.ToArray();var pages=Mathf.Max(1,Mathf.CeilToInt(groups.Length/3f));categoryPage%=pages;
            for(var i=0;i<groups.Length;i++)groups[i].gameObject.SetActive(i/3==categoryPage);
            nextCategories.gameObject.SetActive(groups.Length>3);nextCategories.transform.SetAsLastSibling();
        }
        private void Refresh() { /* Expanded categories poll through their own bounded view. */ }
    }
}
