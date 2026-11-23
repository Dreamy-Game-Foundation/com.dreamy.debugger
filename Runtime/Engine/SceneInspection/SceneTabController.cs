using System;
using System.Collections;
using System.Collections.Generic;
using Dreamy.Debugger.Presentation.UI.Other;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.SceneInspection
{
    public sealed class SceneTabController : MonoBehaviour, IEnableTab
    {
        public RectTransform HierarchyPane, InspectorPane;
        public ScrollRect HierarchyScroll, InspectorScroll;
        public HierarchyRowView HierarchyTemplate;
        public InspectorRowView InspectorTemplate;
        public InputField Search, MemberSearch;
        public Button RefreshButton, PauseButton, EditButton, BackButton, ActiveFilter, SceneFilter, CopyPath;
        public Text Status, Header, EditLabel, PauseLabel, ActiveLabel, SceneLabel;
        public RectTransform SplitHandle;
        public TouchNumberEditor NumberEditor;
        public InspectionPicker Picker;
        public Button UndoButton, RedoButton, CompareButton;
        public Text CompareLabel;
        public RectTransform Menu, MenuPanel, ObjectHeader, Metadata, DraftBar;
        public Button MenuButton, MenuDismiss, ToggleFind, TogglePropertyActions;
        public Button ObjectTag, ObjectLayer, DiscardButton, ApplyButton;
        public Toggle ObjectActive;
        public Text ObjectTagLabel, ObjectLayerLabel, DraftLabel;
        private InspectorRowView.Entry tagEntry, layerEntry, activeEntry, currentDraft;
        private bool propertyActions;
        private Text propertyActionLabel, findLabel;
        private Vector2 lastSimpleSize;
        private bool lastCompact, lastInspector, lastFind, lastDraft;
        private bool SimpleLayout => Menu != null;
        private readonly InspectionHistory history = new InspectionHistory();
        private readonly HashSet<string> pins = new HashSet<string>();
        private readonly Dictionary<string, object> comparison = new Dictionary<string, object>();
        private GameObject compared;
        private string historyNotice;
        private double noticeUntil;
        private bool wasEditable;
        public float SplitRatio = .27f;
        public bool IsEnabled => DebuggerRuntime.Instance != null && DebuggerRuntime.Instance.SceneInspectionEnabled;
        private DebuggerRuntime owner;
        private SceneInspectionSession session;
        private readonly InspectionDescriptors descriptors = new InspectionDescriptors();
        private readonly List<SceneInspectionSession.Node> visible = new List<SceneInspectionSession.Node>();
        private readonly HierarchyProjection projection = new HierarchyProjection();
        private readonly List<InspectorRowView.Entry> members = new List<InspectorRowView.Entry>();
        private readonly List<float> offsets = new List<float>();
        private readonly List<HierarchyRowView> hierarchyRows = new List<HierarchyRowView>();
        private readonly List<InspectorRowView> inspectorRows = new List<InspectorRowView>();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly Dictionary<string, int> pages = new Dictionary<string, int>();
        private readonly List<Component> components = new List<Component>();
        private readonly List<int> sceneHandles = new List<int>();
        private GameObject shown;
        private int revision = -1;
        private int? sceneFilter;
        private bool activeOnly, mobileInspector, started;
        private double nextRefresh;
        private DebugPanelRoot panel;
        private bool running;
        private Func<bool> canEdit;
        private GameObject pendingReveal;
        private const float TreeHeight = 44;

        private void Start()
        {
            started = true;
            canEdit = () => running && isActiveAndEnabled && session != null && session.CanEdit &&
                (panel == null || panel.Canvas.enabled && panel.CanvasGroup.alpha > 0 && DreamyDebug.Instance.IsDebugPanelVisible);
            panel = GetComponentInParent<DebugPanelRoot>();
            Search.onValueChanged.AddListener(_ => { revision = -1; HierarchyScroll.verticalNormalizedPosition = 1; });
            MemberSearch.onValueChanged.AddListener(_ => { CancelDrafts(); BuildInspector(); InspectorScroll.verticalNormalizedPosition = 1; });
            RefreshButton.onClick.AddListener(() => { session?.Refresh(); CancelDrafts(); BuildInspector(); });
            PauseButton.onClick.AddListener(() => { if (session != null) session.Paused = !session.Paused; });
            if (EditButton != null) EditButton.interactable = false;
            UndoButton?.onClick.AddListener(() => MoveHistory(true));
            RedoButton?.onClick.AddListener(() => MoveHistory(false));
            CompareButton?.onClick.AddListener(ToggleComparison);
            BackButton.onClick.AddListener(() => { CancelDrafts(); mobileInspector = false; });
            ActiveFilter.onClick.AddListener(() => { activeOnly = !activeOnly; revision = -1; });
            SceneFilter.onClick.AddListener(CycleScene);
            CopyPath.onClick.AddListener(() => { if (session?.Selected != null) GUIUtility.systemCopyBuffer = Path(session.Selected); });
            if (!SimpleLayout) return;
            MenuButton.onClick.AddListener(() => Menu.gameObject.SetActive(true));
            MenuDismiss.onClick.AddListener(CloseMenu);
            foreach (var button in new[] { RefreshButton, UndoButton, RedoButton, CopyPath, ToggleFind,
                TogglePropertyActions, CompareButton, PauseButton, ActiveFilter, SceneFilter })
                button.onClick.AddListener(CloseMenu);
            propertyActionLabel = TogglePropertyActions.GetComponentInChildren<Text>();
            findLabel = ToggleFind.GetComponentInChildren<Text>();
            ToggleFind.onClick.AddListener(TogglePropertySearch);
            TogglePropertyActions.onClick.AddListener(() => propertyActions = !propertyActions);
            ObjectTag.onClick.AddListener(() => OpenPicker(tagEntry));
            ObjectLayer.onClick.AddListener(() => OpenPicker(layerEntry));
            ObjectActive.onValueChanged.AddListener(SetObjectActive);
            ApplyButton.onClick.AddListener(ApplyCurrentDraft);
            DiscardButton.onClick.AddListener(() => { CancelDrafts(); ClearErrors(); });
        }
        private void OnEnable() { revision = -1; nextRefresh = 0; }
        private void OnDisable()
        {
            if (AccessGate.DismissContent == DismissContent) AccessGate.DismissContent = null;
            running = false; CloseMenu(); CancelDrafts(); projection.Cancel(); session?.Suspend();
            foreach (var row in hierarchyRows) row.Clear();
            foreach (var row in inspectorRows) row.Clear();
        }
        private void OnDestroy() { descriptors.Clear(); members.Clear(); session = null; owner = null; }
        private void Update()
        {
            if (!started) return;
            var current = DebuggerRuntime.Instance;
            if (current != owner) { history.Clear(); pins.Clear(); comparison.Clear(); compared = null; session = null; accessVersion = -1; owner = current; shown = null; members.Clear(); revision = -1; }
            if (owner == null || !owner.CanInspectScene || panel != null &&
                (!DreamyDebug.Instance.IsDebugPanelVisible || !panel.Canvas.enabled || panel.CanvasGroup.alpha <= 0))
            {
                if (running) { CloseMenu(); CancelDrafts(); projection.Cancel(); session?.Suspend(); running = false; }
                if (owner == null || !owner.CanInspectScene) history.Clear();
                Status.text = owner != null && owner.CanInspectScene ? "Scene view paused while hidden" : "Scene inspection locked"; return;
            }
            running = true;
            AccessGate.DismissContent = DismissContent;
            if (session == null) session = owner.GetSceneInspection();
            if (session == null) return;
            if (owner != null && accessVersion != owner.Access.UnlockGeneration)
            {
                accessVersion = owner.Access.UnlockGeneration;
                history.Clear(); comparison.Clear(); compared = null; CancelDrafts();
                BuildInspector();
            }
            if (wasEditable && !canEdit()) { CancelDrafts(); ClearErrors(); }
            wasEditable = canEdit();
            var time = Time.realtimeSinceStartupAsDouble;
            if (!projection.IsBuilding) session.Tick(time);
            if (revision != session.Revision)
            {
                revision = session.Revision;
                projection.Begin(session, Search.text, sceneFilter, activeOnly);
            }
            if (projection.Tick(owner.InspectionConfiguration.SceneScanBudgetMilliseconds))
            {
                visible.Clear(); visible.AddRange(projection.Result);
                HierarchyScroll.content.sizeDelta = new Vector2(0, visible.Count * TreeHeight);
                if (pendingReveal != null)
                {
                    for (var i = 0; i < visible.Count; i++) if (visible[i].Target == pendingReveal)
                        HierarchyScroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, i * TreeHeight - HierarchyScroll.viewport.rect.height / 2));
                    pendingReveal = null;
                }
            }
            var selected = session.Selected;
            if (!ReferenceEquals(shown, selected))
            {
                CancelDrafts(); shown = selected; expanded.Clear(); pages.Clear(); BuildInspector();
                InspectorScroll.verticalNormalizedPosition = 1;
            }
            if (shown == null && members.Count > 0) { members.Clear(); InspectorScroll.content.sizeDelta = Vector2.zero; }
            if (SimpleLayout) UpdateSummary();
            Layout();
            Keyboard();
            RenderHierarchy(); RenderInspector(!session.Paused && time >= nextRefresh);
            if (!session.Paused && time >= nextRefresh)
            {
                nextRefresh = time + Mathf.Max(.1f, owner.InspectionConfiguration.SceneRefreshSeconds);
                if (shown != null)
                {
                    components.Clear(); shown.GetComponents(components);
                    var signature = ComponentSignature(components);
                    if (signature != componentSignature) { CancelDrafts(); BuildInspector(); }
                }
            }
            Header.text = shown != null ? shown.name : "Select an object to inspect";
            CopyPath.interactable = shown != null;
            if (EditLabel != null) EditLabel.text = session.CanEdit ? "Editing" : "Read only";
            PauseLabel.text = session.Paused ? "Resume inspection" : "Pause inspection";
            ActiveLabel.text = activeOnly ? "Active only" : "All objects";
            if (UndoButton != null) UndoButton.interactable = canEdit() && history.CanUndo;
            if (RedoButton != null) RedoButton.interactable = canEdit() && history.CanRedo;
            if (CompareButton != null) CompareButton.interactable = shown != null;
            if (CompareLabel != null) CompareLabel.text = compared == null ? "Compare" : "Clear compare";
            if (SimpleLayout)
            {
                propertyActionLabel.text = propertyActions ? "Hide Copy and Pin" : "Show Copy and Pin";
                findLabel.text = MemberSearch.gameObject.activeSelf ? "Hide property search" : "Find properties";
                ToggleFind.interactable = TogglePropertyActions.interactable = shown != null;
            }
            Status.text = time < noticeUntil ? historyNotice : session.IsScanning ? "Scanning..." : projection.IsBuilding ? "Filtering..." : session.Paused ? "Inspection paused" : "";
        }
        private void CloseMenu()
        {
            if (Menu != null) Menu.gameObject.SetActive(false);
        }

        private void TogglePropertySearch()
        {
            var open = !MemberSearch.gameObject.activeSelf;
            MemberSearch.gameObject.SetActive(open);
            if (open) { MemberSearch.Select(); MemberSearch.ActivateInputField(); }
            else MemberSearch.text = "";
        }

        private void UpdateSummary()
        {
            var hasObject = shown != null;
            var editable = hasObject && canEdit();
            Metadata.gameObject.SetActive(hasObject);
            ObjectActive.transform.parent.gameObject.SetActive(hasObject);
            ObjectActive.interactable = ObjectTag.interactable = ObjectLayer.interactable = editable;
            if (hasObject && activeEntry != null)
            {
                if (activeEntry.Member.TryRead(out var current, out _))
                {
                    activeEntry.Baseline = current;
                    ObjectActive.SetIsOnWithoutNotify((bool)current);
                }
                if (tagEntry.Member.TryRead(out var tag, out _)) ObjectTagLabel.text = (string)tag;
                if (layerEntry.Member.TryRead(out var layer, out _)) ObjectLayerLabel.text = LayerMask.LayerToName((int)layer);
            }
            currentDraft = null;
            if (editable)
            {
                foreach (var entry in members)
                {
                    if (entry.Drafting) { currentDraft = entry; break; }
                    if (currentDraft == null && entry.Error != null) currentDraft = entry;
                }
            }
            DraftBar.gameObject.SetActive(currentDraft != null);
            if (currentDraft != null)
            {
                DraftLabel.text = currentDraft.Error ?? currentDraft.Label.Trim();
                DraftLabel.color = currentDraft.Error == null ? Foundation.UI.DebuggerPalette.Muted : Foundation.UI.DebuggerPalette.Warning;
                ApplyButton.interactable = currentDraft.Drafting;
            }
        }

        private void SetObjectActive(bool next)
        {
            if (!canEdit() || shown == null || activeEntry == null) return;
            if (!history.Write(activeEntry.Member, activeEntry.Baseline, next, canEdit, out var error))
            {
                historyNotice = error;
                noticeUntil = Time.realtimeSinceStartupAsDouble + 5;
            }
            else session.Refresh();
        }

        private bool MayDraft(InspectorRowView.Entry candidate)
        {
            foreach (var entry in members)
                if (entry.Drafting && entry != candidate) return false;
            return true;
        }

        private void ApplyCurrentDraft()
        {
            var entry = currentDraft;
            if (!canEdit() || entry == null || !entry.Drafting) return;
            if (!InspectionValues.TryParse(entry.Draft ?? "", entry.Member.ValueType, out var value, out var error) ||
                !history.Write(entry.Member, entry.Baseline, value, canEdit, out error))
            { entry.Error = error; return; }
            CancelDrafts();
            ClearErrors();
            nextRefresh = 0;
        }

        private int componentSignature, accessVersion = -1;
        private static int ComponentSignature(List<Component> list)
        {
            unchecked { var hash = 17; foreach (var c in list) hash = hash * 31 + (c != null ? c.GetInstanceID() : 0); return hash; }
        }
        private void Layout()
        {
            var width = ((RectTransform)transform).rect.width;
            var mobile = Compact;
            HierarchyPane.gameObject.SetActive(!mobile || !mobileInspector);
            InspectorPane.gameObject.SetActive(!mobile || mobileInspector);
            BackButton.gameObject.SetActive(mobile && mobileInspector);
            SplitHandle.gameObject.SetActive(!mobile);
            SplitRatio = mobile ? SplitRatio : Mathf.Clamp(SplitRatio, 240f / width, Mathf.Min(.5f, 1f - 420f / width));
            HierarchyPane.anchorMax = new Vector2(mobile ? 1 : SplitRatio, 1);
            InspectorPane.anchorMin = new Vector2(mobile ? 0 : SplitRatio, 0);
            SplitHandle.anchorMin = new Vector2(SplitRatio, 0); SplitHandle.anchorMax = new Vector2(SplitRatio, 1);
            if (SimpleLayout) LayoutSummary(mobile);
            var inline = SimpleLayout ? InspectorPane.rect.width >= 300 : InspectorPane.rect.width >= 560;
            if (members.Count > 0 && members[0].Inline != inline)
            {
                foreach (var row in inspectorRows) row.Clear();
                var height = 0f; offsets.Clear();
                foreach (var member in members) { member.Inline = inline; offsets.Add(height); height += member.Height; }
                InspectorScroll.content.sizeDelta = new Vector2(0, height);
            }
        }
        private void LayoutSummary(bool compact)
        {
            var size = ((RectTransform)transform).rect.size;
            var find = MemberSearch.gameObject.activeSelf;
            var hasDraft = DraftBar.gameObject.activeSelf;
            if (size == lastSimpleSize && compact == lastCompact && mobileInspector == lastInspector &&
                find == lastFind && hasDraft == lastDraft) return;
            lastSimpleSize = size;
            lastCompact = compact;
            lastInspector = mobileInspector;
            lastFind = find;
            lastDraft = hasDraft;

            var backOffset = compact && mobileInspector ? 42f : 0;
            var metadataBottom = compact ? 99 : 93;
            var filterTop = metadataBottom + 7;
            ObjectHeader.offsetMin = new Vector2(14, -50 - backOffset);
            ObjectHeader.offsetMax = new Vector2(-62, -8 - backOffset);
            Metadata.offsetMin = new Vector2(14, -metadataBottom - backOffset);
            Metadata.offsetMax = new Vector2(-14, -55 - backOffset);
            var searchRect = (RectTransform)Search.transform;
            searchRect.offsetMin = new Vector2(12, compact ? -52 : -48);
            searchRect.offsetMax = new Vector2(compact ? -58 : -12, -8);
            var memberFilter = (RectTransform)MemberSearch.transform;
            memberFilter.offsetMin = new Vector2(14, -filterTop - 36 - backOffset);
            memberFilter.offsetMax = new Vector2(-14, -filterTop - backOffset);
            var scroll = (RectTransform)InspectorScroll.transform;
            scroll.offsetMin = new Vector2(8, hasDraft ? 52 : 4);
            scroll.offsetMax = new Vector2(-8, -filterTop - (find ? 42 : 0) - backOffset);
            MenuPanel.sizeDelta = new Vector2(Mathf.Max(0, Mathf.Min(240, size.x - 16)), Mathf.Max(0, Mathf.Min(450, size.y - 56)));
            Status.gameObject.SetActive(!hasDraft);
        }
        private bool Compact => ((RectTransform)transform).rect.width < 760 || ((RectTransform)transform).rect.height < 405;
        private void RenderHierarchy()
        {
            var first = Mathf.Clamp(Mathf.FloorToInt(HierarchyScroll.content.anchoredPosition.y / TreeHeight), 0, Math.Max(0, visible.Count - 1));
            var count = Math.Min(visible.Count - first, Mathf.CeilToInt(HierarchyScroll.viewport.rect.height / TreeHeight) + 2);
            while (hierarchyRows.Count < count)
            {
                var row = Instantiate(HierarchyTemplate, HierarchyScroll.content); hierarchyRows.Add(row);
            }
            for (var i = 0; i < hierarchyRows.Count; i++)
            {
                var row = hierarchyRows[i]; row.gameObject.SetActive(i < count);
                if (i >= count) { row.Clear(); continue; }
                ((RectTransform)row.transform).anchoredPosition = new Vector2(0, -(first + i) * TreeHeight);
                row.Bind(session, visible[first + i], Search.text.Length > 0);
                row.Selected = HierarchySelected;
            }
        }
        private void RenderInspector(bool refresh)
        {
            // Error and responsive rows can change height without rebuilding members or discarding drafts.
            var height = 0f;
            for (var i = 0; i < members.Count; i++) { offsets[i] = height; height += members[i].Height; }
            if (!Mathf.Approximately(InspectorScroll.content.sizeDelta.y,height)) InspectorScroll.content.sizeDelta = new Vector2(0,height);
            var top = Mathf.Max(0, InspectorScroll.content.anchoredPosition.y);
            var first = offsets.BinarySearch(top);
            if (first < 0) first = Math.Max(0, ~first - 1);
            first = Math.Min(first, Math.Max(0, members.Count - 1));
            var count = 0;
            while (first + count < members.Count && offsets[first + count] <= top + InspectorScroll.viewport.rect.height + 108) count++;
            while (inspectorRows.Count < count) inspectorRows.Add(Instantiate(InspectorTemplate, InspectorScroll.content));
            for (var i = 0; i < inspectorRows.Count; i++)
            {
                var row = inspectorRows[i]; row.gameObject.SetActive(i < count);
                if (i >= count) { row.Clear(); continue; }
                var rect = (RectTransform)row.transform; rect.anchoredPosition = new Vector2(0, -offsets[first + i]);
                rect.sizeDelta = new Vector2(0, members[first + i].Height);
                // Bind keeps drafts in the entry when a pooled view moves to another member.
                row.TouchEditor = NumberEditor;
                row.History = history;
                if (SimpleLayout)
                {
                    row.CompactWorkspace = Compact;
                    row.ShowPropertyActions = propertyActions;
                    if (row.WorkspaceDraftAllowed == null) row.WorkspaceDraftAllowed = MayDraft;
                }
                row.Bind(members[first + i], canEdit, refresh);
            }
        }
        private void CycleScene()
        {
            if (session == null) return;
            sceneHandles.Clear();
            foreach (var node in session.Nodes) if (!sceneHandles.Contains(node.SceneHandle)) sceneHandles.Add(node.SceneHandle);
            var index = sceneFilter.HasValue ? sceneHandles.IndexOf(sceneFilter.Value) + 1 : 0;
            sceneFilter = index >= sceneHandles.Count ? (int?)null : sceneHandles[index];
            SceneLabel.text = "All scenes";
            if (sceneFilter.HasValue) foreach (var node in session.Nodes) if (node.SceneHandle == sceneFilter.Value) { SceneLabel.text = node.SceneName; break; }
            revision = -1;
        }
        private bool DismissContent()
        {
            if (!running || session == null || !session.CanRead) return false;
            if (Picker != null && Picker.Dismiss()) return true;
            if (SimpleLayout && Menu.gameObject.activeSelf) { CloseMenu(); return true; }
            if (NumberEditor != null && NumberEditor.Dismiss()) return true;
            foreach (var entry in members)
                if (entry.Drafting || entry.Error != null) { CancelDrafts(); ClearErrors(); return true; }
            if (Compact && mobileInspector)
            { mobileInspector = false; return true; }
            return false;
        }
        private void Keyboard()
        {
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused == null || !focused.transform.IsChildOf(transform) ||
                Picker != null && Picker.gameObject.activeSelf || NumberEditor != null && NumberEditor.gameObject.activeSelf ||
                SimpleLayout && Menu.gameObject.activeSelf) return;
            if (InputBridge.Down(KeyCode.F) && (InputBridge.IsHeld(KeyCode.LeftControl) || InputBridge.IsHeld(KeyCode.RightControl)))
            { mobileInspector = false; Layout(); Search.Select(); Search.ActivateInputField(); return; }
            if (focused.GetComponent<InputField>() != null || SimpleLayout && !focused.transform.IsChildOf(HierarchyPane)) return;
            var index = -1;
            for (var i = 0; i < visible.Count; i++) if (visible[i].Target == session.Selected) { index = i; break; }
            if (InputBridge.Down(KeyCode.DownArrow) || InputBridge.Down(KeyCode.UpArrow))
            {
                index = Mathf.Clamp(index + (InputBridge.Down(KeyCode.DownArrow) ? 1 : -1), 0, visible.Count - 1);
                if (index < 0 || index >= visible.Count) return;
                session.Select(visible[index].Target);
                HierarchyScroll.content.anchoredPosition = new Vector2(0, Mathf.Max(0, index * TreeHeight - HierarchyScroll.viewport.rect.height / 2));
            }
            else if (index >= 0)
            {
                var node = visible[index];
                if (InputBridge.Down(KeyCode.RightArrow) && node.HasChildren && !session.IsExpanded(node)) session.Toggle(node);
                if (InputBridge.Down(KeyCode.LeftArrow))
                {
                    if (session.IsExpanded(node)) session.Toggle(node);
                    else if (node.Target != null && node.Target.transform.parent != null) session.Select(node.Target.transform.parent.gameObject);
                }
            }
        }
        public bool Reveal(GameObject target)
        {
            if (!running || session == null || !session.CanRead || target == null) return false;
            CancelDrafts();
            Search.SetTextWithoutNotify("");
            sceneFilter = null;
            SceneLabel.text = "All scenes";
            activeOnly = false;
            session.Reveal(target);
            pendingReveal = target;
            mobileInspector = true;
            revision = -1;
            return true;
        }
        private void HierarchySelected() { CancelDrafts(); mobileInspector = true; }
        private void CancelDrafts()
        {
            if (NumberEditor != null) NumberEditor.Dismiss();
            if (Picker != null) Picker.Dismiss();
            foreach (var entry in members) { entry.Drafting = false; entry.Draft = null; }
            foreach (var row in inspectorRows) row.Clear();
        }
        private void ClearErrors() { foreach (var entry in members) entry.Error = null; }
        private void BuildInspector()
        {
            members.Clear(); offsets.Clear();
            activeEntry = tagEntry = layerEntry = null;
            if (shown == null) return;
            if (SimpleLayout)
            {
                activeEntry = new InspectorRowView.Entry { Member = InspectionMember.Active(shown), Baseline = shown.activeSelf };
                tagEntry = new InspectorRowView.Entry { Key = "Object/tag", Label = "Tag", Member = InspectionMember.Adapter(shown, "tag", true) };
                layerEntry = new InspectorRowView.Entry { Key = "Object/layer", Label = "Layer", Member = InspectionMember.Adapter(shown, "layer", true) };
            }
            else
            {
                // SetActive is a method; expose it via the explicit entry action instead of a property setter.
                AddMember(InspectionMember.Active(shown), "Object/active", 0, new List<object>(), true);
                AddMember(InspectionMember.Adapter(shown, "tag", true), "Object/tag", 0, new List<object>(), true, "Existing project tag name");
                AddMember(InspectionMember.Adapter(shown, "layer", true), "Object/layer", 0, new List<object>(), true);
                layerEntry = members[members.Count - 1];
                tagEntry = members[members.Count - 2];
                tagEntry.Pick = () => OpenPicker(tagEntry);
                var layerNames = new List<string>(); var layerValues = new List<object>();
                for (var layer = 0; layer < 32; layer++)
                {
                    var layerName = LayerMask.LayerToName(layer);
                    if (string.IsNullOrEmpty(layerName)) continue;
                    layerNames.Add(layerName + " (" + layer + ")"); layerValues.Add(layer);
                }
                layerEntry.ChoiceLabels = layerNames.ToArray(); layerEntry.ChoiceValues = layerValues.ToArray(); layerEntry.NoScrub = true;
            }
            components.Clear(); shown.GetComponents(components); componentSignature = ComponentSignature(components);
            var occurrences = new Dictionary<Type, int>();
            var filteringMembers = !string.IsNullOrEmpty(MemberSearch.text);
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null) { members.Add(new InspectorRowView.Entry { Label = "Missing script", IconKey = "warning", Hint = "Component cannot be inspected" }); continue; }
                var key = "C" + component.GetInstanceID();
                var type = component.GetType();
                occurrences.TryGetValue(type, out var occurrence);
                occurrences[type] = occurrence + 1;
                var firstField = members.Count + 1;
                var comparisonPrefix = type.FullName + "#" + occurrence;
                var componentMatch = type.Name.IndexOf(MemberSearch.text, StringComparison.OrdinalIgnoreCase) >= 0;
                var collapsed = SimpleLayout && !(component is Transform || component is Rigidbody) ? !expanded.Contains(key) : expanded.Contains(key);
                members.Add(new InspectorRowView.Entry
                {
                    Label = UnityLabel(type.Name),
                    Expanded = filteringMembers || !collapsed,
                    IconKey = InspectorComponentCatalog.Icon(component),
                    CustomIcon = FindComponentIcon(type),
                    Action = () => Toggle(key)
                });
                if (collapsed && !filteringMembers && !HasPinnedComponent(key)) continue;
                if (component is Behaviour) AddMember(InspectionMember.Adapter(component, "enabled", true), key + "/enabled", 0, new List<object>(), componentMatch);
                if (component is Transform)
                {
                    foreach (var name in new[] { "localPosition", "localEulerAngles", "localScale" })
                        AddMember(InspectionMember.Adapter(component, name, name != "position"), key + "/" + name, 0, new List<object>(), componentMatch);
                    if (component is RectTransform)
                        foreach (var name in new[] { "anchoredPosition", "sizeDelta", "anchorMin", "anchorMax", "pivot" })
                            AddMember(InspectionMember.Adapter(component, name, true), key + "/" + name, 0, new List<object>(), componentMatch, "LayoutGroup / Animator may overwrite this value");
                }
                else
                {
                    var properties = InspectorComponentCatalog.Properties(component);
                    foreach (var name in properties)
                    {
                        if (SimpleLayout && !filteringMembers && component is Rigidbody &&
                            name != "mass" && name != "useGravity" && name != "isKinematic" && !expanded.Contains(key + "/details") &&
                            !pins.Contains(shown.GetInstanceID() + "/" + key + "/" + name)) continue;
                        var property = type.GetProperty(name);
                        if (property == null || !property.CanRead) continue;
                        AddMember(InspectionMember.Adapter(component, name, property.CanWrite), key + "/" + name, 0, new List<object>(), componentMatch);
                    }
                    if (SimpleLayout && !filteringMembers && component is Rigidbody)
                    {
                        var details = key + "/details";
                        members.Add(new InspectorRowView.Entry
                        {
                            Label = expanded.Contains(details) ? "Fewer properties" : "More properties",
                            Expanded = expanded.Contains(details),
                            Action = () => Toggle(details)
                        });
                    }
                    if (properties.Length == 0 && component is MonoBehaviour)
                        foreach (var field in descriptors.Fields(type))
                            AddMember(InspectionMember.Field(component, field), key + "/" + field.DeclaringType.FullName + "." + field.Name, 0, new List<object>(), componentMatch);
                }
                if (filteringMembers && !componentMatch && members.Count == firstField)
                {
                    members.RemoveAt(firstField - 1);
                    continue;
                }
                string group = null;
                for (var field = firstField; field < members.Count; field++)
                {
                    var entry = members[field];
                    if (entry.Key == null) continue;
                    entry.CompareKey = comparisonPrefix + entry.Key.Substring(key.Length);
                    if (entry.Key.IndexOf('/', key.Length + 1) >= 0 || entry.Group == group) continue;
                    group = entry.Group;
                    if (group == null) continue;
                    members.Insert(field, new InspectorRowView.Entry { Label = group });
                    field++;
                }
            }
            var pinned = members.FindAll(entry => entry.Pinned);
            if (pinned.Count > 0)
            {
                members.RemoveAll(entry => entry.Pinned);
                members.InsertRange(0, pinned);
            }
            foreach (var entry in members)
                if (compared != null && compared != shown && entry.Member != null &&
                    comparison.TryGetValue(entry.CompareKey ?? entry.Key, out var reference))
                {
                    entry.CompareName = compared.name;
                    entry.CompareValue = reference;
                }
            var height = 0f;
            foreach (var member in members)
            {
                member.Dense = SimpleLayout;
                offsets.Add(height);
                height += member.Height;
            }
            InspectorScroll.content.sizeDelta = new Vector2(0, height);
        }
        private bool HasPinnedComponent(string key)
        {
            var prefix = shown.GetInstanceID() + "/" + key + "/";
            foreach (var pin in pins)
                if (pin.StartsWith(prefix, StringComparison.Ordinal)) return true;
            return false;
        }

        private void MoveHistory(bool undo)
        {
            if (!canEdit() || (undo ? !history.CanUndo : !history.CanRedo)) return;
            CancelDrafts();
            if (history.Move(undo, canEdit, out var error)) BuildInspector();
            historyNotice = error ?? (undo ? "Undone" : "Redone");
            noticeUntil = Time.realtimeSinceStartupAsDouble + 5;
        }

        private void ToggleComparison()
        {
            CancelDrafts();
            comparison.Clear();
            if (compared == null && shown != null)
            {
                compared = shown;
                foreach (var entry in members)
                    if (entry.Member != null && entry.Member.TryRead(out var value, out _))
                        comparison[entry.CompareKey ?? entry.Key] = value;
                historyNotice = "Select another object to compare with " + shown.name;
                noticeUntil = Time.realtimeSinceStartupAsDouble + 5;
            }
            else
            {
                compared = null;
                historyNotice = null;
                noticeUntil = 0;
            }
            BuildInspector();
        }

        private void OpenPicker(InspectorRowView.Entry entry)
        {
            if (Picker == null || !canEdit() || entry?.Member == null || !entry.Member.Writable) return;
            var member = entry.Member;
            if (!member.TryRead(out var baseline, out _)) return;
            var choices = new List<InspectionPicker.Option>();
            var type = member.ValueType;
            Type flags = null;
            void Add(string label, object value) => choices.Add(new InspectionPicker.Option { Label = label, Value = value });
            if (entry.Key == "Object/tag")
            {
                var tags = new SortedSet<string>(new[] { "Untagged", "Respawn", "Finish", "EditorOnly", "MainCamera", "Player", "GameController" }, StringComparer.Ordinal);
                foreach (var tag in owner.InspectionConfiguration.HierarchyProjectTags ?? Array.Empty<string>())
                    if (!string.IsNullOrEmpty(tag)) tags.Add(tag);
                if (baseline is string current) tags.Add(current);
                foreach (var tag in tags) Add(tag, tag);
            }
            else if (entry.Key == "Object/layer")
            {
                for (var layer = 0; layer < 32; layer++)
                {
                    var name = LayerMask.LayerToName(layer);
                    if (!string.IsNullOrEmpty(name)) Add(name, layer);
                }
            }
            else if (type.IsEnum)
            {
                flags = type;
                foreach (var name in Enum.GetNames(type)) Add(UnityLabel(name), Enum.Parse(type, name));
            }
            else if (member.CanAssignReference)
            {
                Add("None", null);
                var matches = new List<Component>();
                foreach (var node in session.Nodes)
                {
                    var candidate = node.Target;
                    if (candidate == null || !candidate.scene.isLoaded) continue;
                    if (type == typeof(GameObject)) Add(Path(candidate), candidate);
                    else
                    {
                        matches.Clear();
                        candidate.GetComponents(type, matches);
                        for (var i = 0; i < matches.Count; i++)
                            if (matches[i] != null) Add(Path(candidate) + " · " + matches[i].GetType().Name + " #" + (i + 1), matches[i]);
                    }
                }
            }
            if (NumberEditor != null) NumberEditor.Dismiss();
            var selected = shown;
            Picker.Open(entry.Label.Trim(), baseline, choices, flags, draft =>
            {
                if (!history.Write(member, baseline, draft, () => canEdit() && shown == selected, out var error)) return error;
                entry.Error = null;
                nextRefresh = 0;
                return null;
            }, () => canEdit() && shown == selected, type == typeof(Color));
        }

        private static string UnityLabel(string name)
        {
            var label = System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
            return label.Length == 0 ? label : char.ToUpperInvariant(label[0]) + label.Substring(1);
        }
        private Sprite FindComponentIcon(Type type)
        {
            var icons = owner.InspectionConfiguration.HierarchyComponentIcons;
            if (icons == null) return null;
            foreach (var entry in icons)
                if (entry.Icon != null && string.Equals(entry.TypeName, type.FullName, StringComparison.Ordinal))
                    return entry.Icon;
            return null;
        }
        private void Toggle(string key) { CancelDrafts(); if (!expanded.Remove(key)) expanded.Add(key); BuildInspector(); }
        private DebuggerConfiguration.MemberPresentation FindMemberPresentation(InspectionMember member, int depth)
        {
            var definitions = owner.InspectionConfiguration.HierarchyMemberPresentation;
            if (depth != 0 || definitions == null || definitions.Length == 0) return default;
            var type = member.OwnerType.FullName;
            foreach (var definition in definitions)
                if (definition.TypeName == type && definition.MemberName == member.Name) return definition;
            return default;
        }

        private void AddMember(InspectionMember member, string key, int depth, List<object> ancestors, bool include = false, string hint = null)
        {
            if (!member.TryRead(out var value, out var error))
            { members.Add(new InspectorRowView.Entry { Label = member.Name, Member = member, Key = key, Error = error }); return; }
            var type = member.ValueType;
            var elementType = InspectionValues.ElementType(type);
            var composite = InspectionValues.IsComposite(type);
            var expandable = value != null && (elementType != null || composite);
            var friendly = member.Name == "localPosition" ? "Position" : member.Name == "localEulerAngles" ? "Rotation" : member.Name == "localScale" ? "Scale" : member.Name == "tag" ? "Tag" : member.Name == "layer" ? "Layer" : UnityLabel(member.Name);
            var presentation = FindMemberPresentation(member, depth);
            if (!string.IsNullOrWhiteSpace(presentation.Label)) friendly = presentation.Label;
            var query = MemberSearch.text;
            var matches = include || string.IsNullOrEmpty(query) ||
                member.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                friendly.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
            var pinKey = shown.GetInstanceID() + "/" + key;
            if (!matches && !expandable && !pins.Contains(pinKey)) return;
            var entry = new InspectorRowView.Entry { Label = new string(' ', depth * 2) + friendly, Member = member, Key = key, Hint = hint, Group = string.IsNullOrWhiteSpace(presentation.Group) ? null : presentation.Group };
            if (!expandable)
            {
                if (InspectionValues.IsEditable(type))
                    entry.Copy = () =>
                    {
                        if (session.CanRead && member.TryRead(out var current, out _))
                            GUIUtility.systemCopyBuffer = InspectionValues.Format(current);
                    };
                entry.Pinned = pins.Contains(pinKey);
                entry.Pin = () =>
                {
                    if (!pins.Remove(pinKey) && pins.Count < 64) pins.Add(pinKey);
                    CancelDrafts();
                    BuildInspector();
                };
            }
            var hasInline = InspectionValues.IsEditable(type);
            var hasPicker = type == typeof(Color) || type.IsEnum && type.IsDefined(typeof(FlagsAttribute), false) || member.CanAssignReference;
            if (hasPicker && (!hasInline || presentation.Drawer != DebuggerConfiguration.HierarchyDrawer.Inline))
            {
                entry.Pick = () => OpenPicker(entry);
                entry.PickerOnly = presentation.Drawer == DebuggerConfiguration.HierarchyDrawer.Picker;
            }
            members.Add(entry);
            if (typeof(Object).IsAssignableFrom(type))
            {
                entry.Hint = member.CanAssignReference ? "Scene object reference" : "Object reference / read only";
                if (member.CanAssignReference || value is GameObject || value is Component)
                {
                    GameObject Target()
                    {
                        if (!member.TryRead(out var reference, out _) || !(reference is Object obj) || obj == null) return null;
                        return obj is Component component ? component.gameObject : obj as GameObject;
                    }
                    entry.Available = () => Target() != null;
                    entry.Action = () => Reveal(Target());
                }
                return;
            }
            if (!expandable)
            {
                if (!InspectionValues.IsEditable(type)) entry.Hint = value == null ? "Null / read only" : "Unsupported / " + type.Name;
                else if (type.IsEnum) entry.Hint = "Values: " + string.Join(", ", Enum.GetNames(type));
                return;
            }
            entry.Member = null; entry.Hint = type.Name;
            if (ancestors.Exists(parent => ReferenceEquals(parent, value))) { entry.Hint = "Cycle / read only"; return; }
            if (depth >= Mathf.Clamp(owner.InspectionConfiguration.InspectionMaxDepth, 1, 8)) { entry.Hint = "Maximum depth"; return; }
            entry.Expanded = expanded.Contains(key);
            entry.Action = () => Toggle(key);
            if (!expanded.Contains(key)) return;
            var nested = new List<object>(ancestors) { value };
            if (elementType != null && value is IList list)
            {
                var pageSize = Mathf.Clamp(owner.InspectionConfiguration.InspectionPageSize, 8, 64);
                pages.TryGetValue(key, out var page); page = Mathf.Clamp(page, 0, Math.Max(0, (list.Count - 1) / pageSize));
                entry.Hint = list.Count + " items / size read only / page " + (page + 1);
                if (page > 0) members.Add(new InspectorRowView.Entry { Label = "Previous page", Action = () => { pages[key] = page - 1; CancelDrafts(); BuildInspector(); } });
                for (var i = page * pageSize; i < Math.Min(list.Count, (page + 1) * pageSize); i++) AddMember(member.Element(i, elementType), key + "/" + i, depth + 1, nested, include);
                if ((page + 1) * pageSize < list.Count) members.Add(new InspectorRowView.Entry { Label = "Next page", Action = () => { pages[key] = page + 1; CancelDrafts(); BuildInspector(); } });
            }
            else foreach (var field in descriptors.Fields(type)) AddMember(member.Child(field), key + "/" + field.Name, depth + 1, nested, include);
        }
        public static string Path(GameObject target)
        {
            var path = target.name;
            for (var parent = target.transform.parent; parent != null; parent = parent.parent) path = parent.name + "/" + path;
            return target.scene.name + "/" + path;
        }
    }
}
