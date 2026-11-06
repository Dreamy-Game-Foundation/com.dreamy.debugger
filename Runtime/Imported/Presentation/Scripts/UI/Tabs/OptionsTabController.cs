using System.Linq;
using System.Collections;
using System.Diagnostics;

namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using Controls;
    using Controls.Data;
    using Internal;
    using Other;
    using Services;
    using Dreamy.Debugger.Foundation;
    using UnityEngine;
using Debug=UnityEngine.Debug;
    using UnityEngine.UI;

    public class OptionsTabController : ComponentBehaviour
    {
        private class CategoryInstance
        {
            public CategoryGroup CategoryGroup { get; private set; }
            public string Name;
            public readonly Dictionary<object, OptionUnit> Units = new Dictionary<object, OptionUnit>();
            public readonly List<GameObject> Roots=new List<GameObject>();
            public readonly List<OptionsControlBase> Options = new List<OptionsControlBase>();

            public CategoryInstance(CategoryGroup group)
            {
                CategoryGroup = group;
            }
        }

        private sealed class OptionUnit
        {
            public OptionDefinition[] Definitions;
            public GameObject Root;
            public OptionCard Card;
            public readonly List<OptionsControlBase> Controls = new List<OptionsControlBase>();
        }

        private readonly List<OptionsControlBase> _controls = new List<OptionsControlBase>();
        private readonly List<CategoryInstance> _categories = new List<CategoryInstance>();

        private readonly Dictionary<OptionDefinition, OptionsControlBase> _options =
            new Dictionary<OptionDefinition, OptionsControlBase>();

        public InputField SearchField;
        public Dropdown CategoryFilter;
        public Toggle PinnedOnly;
        public Text ResultCount;
        public OptionCard TaskCardPrefab;
        public int VisibleOptionCount { get; private set; }
        private readonly Dictionary<string,bool> collapsed = new Dictionary<string,bool>(StringComparer.Ordinal);
        private readonly Dictionary<OptionsControlBase,GameObject> roots = new Dictionary<OptionsControlBase,GameObject>();
        private readonly Dictionary<GameObject,bool> shownRoots=new Dictionary<GameObject,bool>();
        private readonly Dictionary<OptionsControlBase,string> searchIndex=new Dictionary<OptionsControlBase,string>();
        private readonly List<OptionCard> cards = new List<OptionCard>();
        private string query = "", categoryFilter = "";
        private bool pinnedOnly;
        private Sprite categoryPinSprite;
        private bool changingCategoryPins;
        private Coroutine categoryPinOperation;
        public void SetSearch(string value) { query = value ?? ""; ApplyFilters(); }
        public void SetCategory(string value) { categoryFilter = value ?? ""; ApplyFilters(); }
        public void SetPinnedOnly(bool value) { pinnedOnly = value; ApplyFilters(); }
        public void ToggleCategory(string name) { collapsed[name] = !IsCollapsed(name);PreferenceBridge.Set("category."+name,collapsed[name]?"1":"0"); ApplyFilters(); }
        public bool IsCollapsed(string name) { return collapsed.TryGetValue(name, out var value) ? value : PreferenceBridge.Get("category."+name,name=="Debugger"?"1":"0")=="1"; }
        private void ApplyFilters()
        {
            VisibleOptionCount = 0;
            foreach (var category in _categories) {
                var forceExpanded=query.Length>0 || pinnedOnly || categoryFilter.Length>0;
                var any = false;
                foreach(var root in category.Roots)shownRoots[root]=false;
                foreach (var control in category.Options) {
                    var option = control.Option;
                    var terms=searchIndex[control];
                    var match = (categoryFilter.Length == 0 || category.Name == categoryFilter) &&
                        (!pinnedOnly || Service.PinnedUI.HasPinned(option)) && terms.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                    any |= match; if (match) VisibleOptionCount++;
                    var show = match && (forceExpanded || !IsCollapsed(category.Name));
                    control.gameObject.SetActive(show);
                    if (show) shownRoots[roots[control]]=true;
                }
                foreach (var root in category.Roots)
                    if (root.GetComponent<OptionCard>() != null) root.SetActive(shownRoots[root]);
                category.CategoryGroup.gameObject.SetActive(any);
                category.CategoryGroup.SetPinState(category.Options.Count(control=>Service.PinnedUI.HasPinned(control.Option)),category.Options.Count);
                if(changingCategoryPins && category.CategoryGroup.PinButton!=null)category.CategoryGroup.PinButton.interactable=false;
                if (category.CategoryGroup.CollapseIndicator != null) category.CategoryGroup.CollapseIndicator.text = forceExpanded?"":IsCollapsed(category.Name) ? "+" : "-";
                if(category.CategoryGroup.CollapseButton!=null)category.CategoryGroup.CollapseButton.interactable=!forceExpanded;
            }
            if (ResultCount != null) ResultCount.text = VisibleOptionCount + " options";
            NoOptionsNotice.SetActive(VisibleOptionCount == 0);
        }
        private Coroutine population;
        private bool started, pendingPopulation;
        public bool IsBuilding { get; private set; }
        private bool slicePopulation;
        private bool _selectionModeEnabled;
        private Canvas _optionCanvas;

        [RequiredField] public ActionControl ActionControlPrefab;

        [RequiredField] public CategoryGroup CategoryGroupPrefab;

        [RequiredField] public RectTransform ContentContainer;

        [RequiredField] public GameObject NoOptionsNotice;

        [RequiredField] public Toggle PinButton;

        [RequiredField] public GameObject PinPromptSpacer;

        [RequiredField] public GameObject PinPromptText;


        private void ConfigureEmptyState()
        {
            if (NoOptionsNotice == null || ContentContainer == null) return;
            NoOptionsNotice.transform.SetParent(ContentContainer.parent, false);
            var rect = (RectTransform)NoOptionsNotice.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(16, 16); rect.offsetMax = new Vector2(-16, -16);
            var size = NoOptionsNotice.GetComponent<LayoutElement>();
            if (size != null) size.ignoreLayout = true;
            var text = NoOptionsNotice.GetComponent<Text>();
            if (text != null) { text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; }
        }

        private void ConfigurePinning()
        {
            if (PinButton != null)
            {
                var icon = PinButton.GetComponentsInChildren<Image>(true).FirstOrDefault(image => image.name == "Sprite" && image.sprite != null);
                categoryPinSprite = icon != null ? icon.sprite : null;
                PinButton.gameObject.SetActive(false);
            }
            if (PinnedOnly != null)
            {
                var label = PinnedOnly.GetComponentInChildren<Text>(true);
                if (label != null) { label.text = "Only pinned"; label.fontSize = 12; }
                var size = PinnedOnly.GetComponent<LayoutElement>();
                if (size != null) size.minWidth = size.preferredWidth = 96;
            }
        }

        private void ToggleCategoryPin(CategoryInstance category)
        {
            if (!AccessGate.Authorized || category.Options.Count == 0 || changingCategoryPins) return;
            categoryPinOperation = StartCoroutine(PinCategory(category));
        }

        private IEnumerator PinCategory(CategoryInstance category)
        {
            var definitions = category.Options.Select(control => control.Option).ToArray();
            var remove = definitions.All(Service.PinnedUI.HasPinned);
            changingCategoryPins = true;
            foreach(var item in _categories)if(item.CategoryGroup.PinButton!=null)item.CategoryGroup.PinButton.interactable=false;
            var budget = Stopwatch.StartNew();
            try
            {
                foreach (var option in definitions)
                {
                    if (!AccessGate.Authorized) yield break;
                    if (Service.Options.Options.Contains(option))
                    {
                        if (remove) Service.PinnedUI.Unpin(option); else Service.PinnedUI.Pin(option);
                    }
                    if (budget.Elapsed.TotalMilliseconds >= 4) { yield return null; budget.Restart(); }
                }
            }
            finally { changingCategoryPins = false; ApplyFilters(); }
        }

        protected override void Start()
        {
            base.Start();

            ConfigurePinning();
            ConfigureEmptyState();

            PinPromptText.SetActive(false);
            PinPromptSpacer.SetActive(false);

            if (SearchField != null) SearchField.onValueChanged.AddListener(SetSearch);
            if (PinnedOnly != null) PinnedOnly.onValueChanged.AddListener(SetPinnedOnly);
            if (CategoryFilter != null) CategoryFilter.onValueChanged.AddListener(index => SetCategory(index == 0 ? "" : CategoryFilter.options[index].text));
            PreferenceBridge.Restored+=RestorePreferences;
            started=true;
            Populate();

            _optionCanvas = GetComponent<Canvas>();

            Service.Options.OptionsUpdated += OnOptionsUpdated;
            Service.PinnedUI.OptionPinStateChanged += OnOptionPinnedStateChanged;
        }

        private void RestorePreferences(){if(changingCategoryPins)return;collapsed.Clear();ApplyFilters();}
        protected override void OnDestroy()
        {
            PreferenceBridge.Restored-=RestorePreferences;
            if (Service.Options != null)
            {
                Service.Options.OptionsUpdated -= OnOptionsUpdated;
            }

            if (Service.PinnedUI != null)
            {
                Service.PinnedUI.OptionPinStateChanged -= OnOptionPinnedStateChanged;
            }

            base.OnDestroy();
        }

        private void OnOptionPinnedStateChanged(OptionDefinition optionDefinition, bool isPinned)
        {
            if (changingCategoryPins) return;
            if (_options.ContainsKey(optionDefinition))
            {
                if (_selectionModeEnabled) _options[optionDefinition].IsSelected = isPinned;
                ApplyFilters();
            }
        }

        private void OnOptionsUpdated(object sender, EventArgs eventArgs)
        {
            // Service may emit a burst of add/remove events; reconcile once next frame.
            pendingPopulation=true;
            PruneMissingUnits();
        }

        protected override void OnEnable()
        {
            base.OnEnable();

            Service.Panel.VisibilityChanged += PanelOnVisibilityChanged;
            if(started){if(pendingPopulation)Populate();else ApplyFilters();}
        }

        protected override void OnDisable()
        {
            if(categoryPinOperation!=null){StopCoroutine(categoryPinOperation);categoryPinOperation=null;}
            changingCategoryPins=false;
            if(IsBuilding){if(population!=null)StopCoroutine(population);IsBuilding=false;pendingPopulation=true;}
            // Always end pinning mode when tabbing away
            SetSelectionModeEnabled(false);

            if (Service.Panel != null)
            {
                Service.Panel.VisibilityChanged -= PanelOnVisibilityChanged;
            }

            base.OnDisable();
        }

        protected override void Update()
        {
            base.Update();

            if (pendingPopulation && started) Populate();
        }

        private void PanelOnVisibilityChanged(IDebugPanelService debugPanelService, bool b)
        {
            // Always end pinning mode when panel is closed
            if (!b)
            {
                SetSelectionModeEnabled(false);

                // Refresh bindings for all pinned controls
                Refresh();
            }
            else if (b && CachedGameObject.activeInHierarchy)
            {
                // If the panel is visible, and this tab is active (selected), refresh all the data bindings
                Refresh();
            }

            if (_optionCanvas != null)
            {
                _optionCanvas.enabled = b;
            }
        }

        public void SetSelectionModeEnabled(bool isEnabled)
        {
            if (_selectionModeEnabled == isEnabled)
            {
                return;
            }

            _selectionModeEnabled = isEnabled;

            if (isEnabled && pinnedOnly)
            {
                pinnedOnly = false;
                PinnedOnly.SetIsOnWithoutNotify(false);
                ApplyFilters();
            }

            PinButton.isOn = isEnabled;
            PinPromptText.SetActive(isEnabled);
            PinPromptSpacer.SetActive(isEnabled);

            foreach (var kv in _options)
            {
                kv.Value.SelectionModeEnabled = isEnabled;

                // Set IsSelected if entering selection mode.
                if (isEnabled)
                {
                    kv.Value.IsSelected = Service.PinnedUI.HasPinned(kv.Key);
                }
            }

            foreach (var cat in _categories)
            {
                cat.CategoryGroup.SelectionModeEnabled = isEnabled;
            }

            foreach (var card in cards) card.SetSelectionMode(isEnabled);
            RefreshCategorySelection();

            // Return if entering selection mode
            if (isEnabled)
            {
                return;
            }
        }

        private void Refresh()
        {
            for (var i = 0; i < _options.Count; i++)
            {
                _controls[i].Refresh();
                _controls[i].SelectionModeEnabled = _selectionModeEnabled;
                _controls[i].IsSelected = Service.PinnedUI.HasPinned(_controls[i].Option);
            }
        }

        private void CommitPinnedOptions()
        {
            foreach (var kv in _options)
            {
                var control = kv.Value;

                if (control.IsSelected && !Service.PinnedUI.HasPinned(kv.Key))
                {
                    Service.PinnedUI.Pin(kv.Key);
                }
                else if (!control.IsSelected && Service.PinnedUI.HasPinned(kv.Key))
                {
                    Service.PinnedUI.Unpin(kv.Key);
                }
            }
        }

        private bool _isTogglingCategory;

        private void RefreshCategorySelection()
        {
            _isTogglingCategory = true;

            foreach (var cat in _categories)
            {
                var allSelected = true;

                for (var i = 0; i < cat.Options.Count; i++)
                {
                    if (!cat.Options[i].IsSelected)
                    {
                        allSelected = false;
                        break;
                    }
                }

                cat.CategoryGroup.IsSelected = allSelected;
            }

            _isTogglingCategory = false;
        }

        private void OnOptionSelectionToggle(bool selected)
        {
            if (_selectionModeEnabled && !_isTogglingCategory)
            {
                RefreshCategorySelection();
                CommitPinnedOptions();
            }
        }

        /// <summary>
        /// When a category mode selection is changed.
        /// </summary>
        /// <param name="category"></param>
        /// <param name="selected"></param>
        private void OnCategorySelectionToggle(CategoryInstance category, bool selected)
        {
            _isTogglingCategory = true;

            for (var i = 0; i < category.Options.Count; i++)
            {
                category.Options[i].IsSelected = selected;
            }

            _isTogglingCategory = false;

            CommitPinnedOptions();
        }

        #region Initialisation

        protected void Populate()
        {
            if(population!=null)StopCoroutine(population);
            pendingPopulation=false;IsBuilding=true;
            if(ResultCount!=null)ResultCount.text="Loading…";NoOptionsNotice.SetActive(false);
            population=StartCoroutine(PopulateRoutine());
        }
        private IEnumerator PopulateRoutine()
        {
            try {
            slicePopulation=Service.Options.Options.Count>32;
            var scrollPosition=ContentContainer.anchoredPosition;
            var preservePosition=_controls.Count>0;
            var snapshot=Service.Options.Options.ToArray();
            var liveCategories=new HashSet<string>(snapshot.Select(o=>o.Category));
            foreach(var stale in _categories.Where(c=>!liveCategories.Contains(c.Name)).ToArray())RemoveCategory(stale);
            var sortedOptions = new Dictionary<string, List<OptionDefinition>>();

            foreach (var option in snapshot)
            {
                if (!OptionControlFactory.CanCreateControl(option))
                {
                    if (option.IsProperty)
                    {
                        Debug.LogError("[DreamyDebugger.OptionsTab] Unsupported property type: {0} (on property {1})".Fmt(option.Property.PropertyType, option.Property));
                    }
                    else
                    {
                        Debug.LogError("[DreamyDebugger.OptionsTab] Unsupported method signature: {0}".Fmt(option.Name));
                    }
                    continue;
                }

                // Find a properly list for that category, or create a new one
                List<OptionDefinition> memberList;

                if (!sortedOptions.TryGetValue(option.Category, out memberList))
                {
                    memberList = new List<OptionDefinition>();
                    sortedOptions.Add(option.Category, memberList);
                }

                memberList.Add(option);
            }

            var hasCreated = false;

            foreach (var kv in sortedOptions.OrderBy(p => p.Key == "Debugger" ? 1 : 0).ThenBy(p => p.Key))
            {
                if (kv.Value.Count == 0)
                {
                    continue;
                }

                hasCreated = true;
                if(pendingPopulation)yield break;
                yield return CreateCategory(kv.Key, kv.Value);
                if(pendingPopulation)yield break;
            }

            if (hasCreated)
            {
                NoOptionsNotice.SetActive(false);
            }
            
            if (CategoryFilter != null) {
                CategoryFilter.ClearOptions();
                CategoryFilter.AddOptions(new[]{"All categories"}.Concat(_categories.Select(c=>c.Name)).ToList());
                var index = CategoryFilter.options.FindIndex(o=>o.text == categoryFilter);
                CategoryFilter.SetValueWithoutNotify(Mathf.Max(0,index));
                if (index < 0) categoryFilter = "";
            }
            _categories.Sort((a,b)=>a.Name==b.Name?0:a.Name=="Debugger"?1:b.Name=="Debugger"?-1:StringComparer.Ordinal.Compare(a.Name,b.Name));
            for(var index=0;index<_categories.Count;index++)_categories[index].CategoryGroup.transform.SetSiblingIndex(index);
            RefreshCategorySelection();IsBuilding=false;ApplyFilters();
            if(preservePosition)ContentContainer.anchoredPosition=scrollPosition;
            } finally {IsBuilding=false;}
        }

        private IEnumerator CreateCategory(string title, List<OptionDefinition> options)
        {
            var categoryInstance=_categories.FirstOrDefault(c=>c.Name==title);
            if(categoryInstance==null){
                var group=ObjectFactory.Instantiate(CategoryGroupPrefab);
                categoryInstance=new CategoryInstance(group){Name=title};_categories.Add(categoryInstance);
                group.CachedTransform.SetParent(ContentContainer,false);
                group.Header.text=title=="Debugger"?"Debugger tools":title;
                group.SelectionModeEnabled=_selectionModeEnabled;
                var captured=categoryInstance;
                group.ConfigurePinButton(categoryPinSprite,()=>ToggleCategoryPin(captured));
                if(group.CollapseButton!=null)group.CollapseButton.onClick.AddListener(()=>ToggleCategory(title));
                group.SelectionToggle.onValueChanged.AddListener(value=>OnCategorySelectionToggle(captured,value));
            }
            var validGroups=options.Where(o=>o.Card!=null).GroupBy(o=>o.Card.Id).Where(g=>{
                var first=g.First().Card;var roles=g.Select(o=>o.Card.Role).ToArray();
                return g.All(o=>o.Card.Kind==first.Kind && o.Card.Title==first.Title) && roles.Distinct().Count()==roles.Length &&
                    (first.Kind==Dreamy.Debugger.Options.CheatCardKind.Resource ? roles.Length==3 && roles.Contains(Dreamy.Debugger.Options.CheatCardRole.Balance) && roles.Contains(Dreamy.Debugger.Options.CheatCardRole.Amount) && roles.Contains(Dreamy.Debugger.Options.CheatCardRole.Action) : roles.Length==2 && roles.Contains(Dreamy.Debugger.Options.CheatCardRole.Owned) && roles.Contains(Dreamy.Debugger.Options.CheatCardRole.Action));
            }).Select(g=>g.Key).ToHashSet();
            Func<OptionDefinition,object> keyFor=option=>TaskCardPrefab!=null && option.Card!=null && validGroups.Contains(option.Card.Id)
                ? (object)("card:"+option.Card.Id) : option;
            var desired=options.OrderBy(o=>o.SortPriority).ThenBy(o=>o.Name,StringComparer.Ordinal).GroupBy(keyFor).ToArray();
            var keys=new HashSet<object>(desired.Select(g=>g.Key));
            foreach(var stale in categoryInstance.Units.Keys.Where(key=>!keys.Contains(key)).ToArray())RemoveUnit(categoryInstance,stale);
            var budget=Stopwatch.StartNew();var order=0;
            foreach(var group in desired){
                var members=group.OrderBy(o=>o.Card==null?9:(int)o.Card.Role).ToArray();
                if(categoryInstance.Units.TryGetValue(group.Key,out var unit) && !unit.Definitions.SequenceEqual(members)){
                    RemoveUnit(categoryInstance,group.Key);unit=null;
                }
                if(unit==null){
                    unit=new OptionUnit{Definitions=members};
                    if(group.Key is string){
                        unit.Card=Instantiate(TaskCardPrefab,categoryInstance.CategoryGroup.Container,false);
                        unit.Card.Configure(members[0].Card);unit.Root=unit.Card.gameObject;cards.Add(unit.Card);
                        unit.Card.SetSelectionMode(_selectionModeEnabled);
                    }
                    // Register the unit before yielding, so cancellation/removal can clean it.
                    categoryInstance.Units.Add(group.Key,unit);
                    foreach(var option in members){
                        var control=OptionControlFactory.CreateControl(option,title);
                        if(control==null)continue;
                        control.CachedTransform.SetParent(categoryInstance.CategoryGroup.Container,false);
                        if(unit.Card!=null)unit.Card.Attach(control);else unit.Root=control.gameObject;
                        unit.Controls.Add(control);categoryInstance.Options.Add(control);
                        roots.Add(control,unit.Root);
                        searchIndex.Add(control,option.Name+" "+option.Category+" "+option.StableId+" "+option.Card?.Title);
                        control.IsSelected=Service.PinnedUI.HasPinned(option);control.SelectionModeEnabled=_selectionModeEnabled;
                        var compact=option.IsMethod || option.Property!=null && (option.Property.PropertyType==typeof(bool) || option.Property.PropertyType.IsEnum);
                        var layout=control.GetComponent<LayoutElement>();if(layout==null)layout=control.gameObject.AddComponent<LayoutElement>();
                        layout.minWidth=compact?144:240;layout.preferredWidth=compact?240:360;layout.flexibleWidth=0;layout.layoutPriority=10;
                        if(unit.Card!=null){layout.minWidth=0;layout.preferredWidth=-1;layout.flexibleWidth=1;layout.preferredHeight=option.Card.Role==Dreamy.Debugger.Options.CheatCardRole.Amount?68:option.Card.Role==Dreamy.Debugger.Options.CheatCardRole.Balance?60:48;}
                        control.SelectionModeToggle.onValueChanged.AddListener(OnOptionSelectionToggle);
                        _options.Add(option,control);_controls.Add(control);
                    }
                    if(unit.Root!=null){shownRoots.Add(unit.Root,false);categoryInstance.Roots.Add(unit.Root);}
                }
                if(unit.Root!=null)unit.Root.transform.SetSiblingIndex(order++);
                if(slicePopulation && budget.Elapsed.TotalMilliseconds>=4){
                    ApplyFilters();if(ResultCount!=null)ResultCount.text="Loading "+_controls.Count+" / "+Service.Options.Options.Count;
                    yield return null;if(pendingPopulation)yield break;budget.Restart();
                }
            }
        }

        private void PruneMissingUnits()
        {
            var live=new HashSet<OptionDefinition>(Service.Options.Options);
            foreach(var category in _categories.ToArray()){
                foreach(var key in category.Units.Where(pair=>pair.Value.Definitions.Any(d=>!live.Contains(d))).Select(pair=>pair.Key).ToArray())RemoveUnit(category,key);
                if(category.Units.Count==0)RemoveCategory(category);
            }
        }

        private void RemoveUnit(CategoryInstance category,object key)
        {
            var unit=category.Units[key];category.Units.Remove(key);
            foreach(var control in unit.Controls){
                _options.Remove(control.Option);_controls.Remove(control);category.Options.Remove(control);
                roots.Remove(control);searchIndex.Remove(control);
            }
            if(unit.Card!=null)cards.Remove(unit.Card);
            if(unit.Root!=null){category.Roots.Remove(unit.Root);shownRoots.Remove(unit.Root);unit.Root.SetActive(false);Destroy(unit.Root);}
        }

        private void RemoveCategory(CategoryInstance category)
        {
            foreach(var key in category.Units.Keys.ToArray())RemoveUnit(category,key);
            _categories.Remove(category);category.CategoryGroup.gameObject.SetActive(false);Destroy(category.CategoryGroup.gameObject);
        }

        #endregion
    }
}
