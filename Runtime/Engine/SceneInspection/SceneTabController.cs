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
        public float SplitRatio = .42f;
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
        private const float TreeHeight = 52;

        private void Start()
        {
            started = true;
            canEdit = () => session != null && session.CanEdit;
            panel = GetComponentInParent<DebugPanelRoot>();
            Search.onValueChanged.AddListener(_ => { revision = -1; HierarchyScroll.verticalNormalizedPosition = 1; });
            MemberSearch.onValueChanged.AddListener(_ => { CancelDrafts(); BuildInspector(); InspectorScroll.verticalNormalizedPosition = 1; });
            RefreshButton.onClick.AddListener(() => { session?.Refresh(); CancelDrafts(); BuildInspector(); });
            PauseButton.onClick.AddListener(() => { if (session != null) session.Paused = !session.Paused; });
            EditButton.onClick.AddListener(() =>
            {
                if (session == null) return;
                if (session.CanEdit) { session.SetEditing(false); CancelDrafts(); }
                else owner.Confirm("Enable runtime editing? Changes affect the running game and can trigger game logic, saving or network updates.", () => session?.SetEditing(true),
                    () => owner != null && owner.CanEditScene && isActiveAndEnabled && running && panel != null && panel.Canvas.enabled && panel.CanvasGroup.alpha > 0 && DreamyDebug.Instance.IsDebugPanelVisible);
            });
            BackButton.onClick.AddListener(() => { CancelDrafts(); mobileInspector = false; });
            ActiveFilter.onClick.AddListener(() => { activeOnly = !activeOnly; revision = -1; });
            SceneFilter.onClick.AddListener(CycleScene);
            CopyPath.onClick.AddListener(() => { if (session?.Selected != null) GUIUtility.systemCopyBuffer = Path(session.Selected); });
        }
        private void OnEnable() { revision = -1; nextRefresh = 0; }
        private void OnDisable()
        {
            if (AccessGate.DismissContent == DismissContent) AccessGate.DismissContent = null;
            running = false; CancelDrafts(); projection.Cancel(); session?.Suspend();
            foreach (var row in hierarchyRows) row.Clear();
            foreach (var row in inspectorRows) row.Clear();
        }
        private void OnDestroy() { descriptors.Clear(); members.Clear(); session = null; owner = null; }
        private void Update()
        {
            if (!started) return;
            var current = DebuggerRuntime.Instance;
            if (current != owner) { session = null; owner = current; shown = null; members.Clear(); revision = -1; }
            if (owner == null || !owner.CanInspectScene || panel != null &&
                (!DreamyDebug.Instance.IsDebugPanelVisible || !panel.Canvas.enabled || panel.CanvasGroup.alpha <= 0))
            {
                if (running) { CancelDrafts(); projection.Cancel(); session?.Suspend(); running = false; }
                Status.text = owner != null && owner.CanInspectScene ? "Scene view paused while hidden" : "Scene inspection locked"; return;
            }
            running = true;
            AccessGate.DismissContent = DismissContent;
            if (session == null) session = owner.GetSceneInspection();
            if (session == null) return;
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
            Header.text = shown != null ? shown.name + "\n" + Path(shown) + "\nID " + shown.GetInstanceID() + " / Tag " + shown.tag + " / Layer " + shown.layer + " / Active in hierarchy " + shown.activeInHierarchy : "Select an object to inspect";
            CopyPath.interactable = shown != null;
            EditLabel.text = session.CanEdit ? "Edit enabled" : "Read only";
            EditButton.interactable = owner.CanEditScene;
            PauseLabel.text = session.Paused ? "Resume" : "Pause";
            ActiveLabel.text = activeOnly ? "Active only" : "All objects";
            Status.text = (session.IsScanning ? "Scanning... " : projection.IsBuilding ? "Filtering... " : session.Paused ? "Paused / " : "") + visible.Count + " shown / " + session.Nodes.Count + " indexed";
        }
        private int componentSignature;
        private static int ComponentSignature(List<Component> list)
        {
            unchecked { var hash = 17; foreach (var c in list) hash = hash * 31 + (c != null ? c.GetInstanceID() : 0); return hash; }
        }
        private void Layout()
        {
            var width = ((RectTransform)transform).rect.width;
            var mobile = width < 720;
            HierarchyPane.gameObject.SetActive(!mobile || !mobileInspector);
            InspectorPane.gameObject.SetActive(!mobile || mobileInspector);
            BackButton.gameObject.SetActive(mobile && mobileInspector);
            SplitHandle.gameObject.SetActive(!mobile);
            SplitRatio = Mathf.Clamp(SplitRatio, .3f, .6f);
            HierarchyPane.anchorMax = new Vector2(mobile ? 1 : SplitRatio, 1);
            InspectorPane.anchorMin = new Vector2(mobile ? 0 : SplitRatio, 0);
            SplitHandle.anchorMin = new Vector2(SplitRatio, 0); SplitHandle.anchorMax = new Vector2(SplitRatio, 1);
        }
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
            foreach (var entry in members)
                if (entry.Drafting || entry.Error != null) { CancelDrafts(); ClearErrors(); return true; }
            if (((RectTransform)transform).rect.width < 720 && mobileInspector)
            { mobileInspector = false; return true; }
            return false;
        }
        private void Keyboard()
        {
            var focused = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (focused == null || !focused.transform.IsChildOf(transform)) return;
            if (InputBridge.Down(KeyCode.F) && (InputBridge.IsHeld(KeyCode.LeftControl) || InputBridge.IsHeld(KeyCode.RightControl)))
            { Search.Select(); Search.ActivateInputField(); return; }
            if (focused.GetComponent<InputField>() != null) return;
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
        private void HierarchySelected() { CancelDrafts(); mobileInspector = true; }
        private void CancelDrafts()
        {
            foreach (var entry in members) { entry.Drafting = false; entry.Draft = null; }
            foreach (var row in inspectorRows) row.Clear();
        }
        private void ClearErrors() { foreach (var entry in members) entry.Error = null; }
        private void BuildInspector()
        {
            members.Clear(); offsets.Clear();
            if (shown == null) return;
            // SetActive is a method; expose it via the explicit entry action instead of a property setter.
            members.Add(new InspectorRowView.Entry { Label = "Active self", ReadLabel = () => shown != null ? "Active self: " + shown.activeSelf + " / Toggle" : "Object destroyed", Hint = "Requires Edit / invokes game lifecycle callbacks", Available = () => session.CanEdit, Action = () =>
                { if (session.CanEdit && shown != null) { shown.SetActive(!shown.activeSelf); session.Refresh(); BuildInspector(); } } });
            components.Clear(); shown.GetComponents(components); componentSignature = ComponentSignature(components);
            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];
                if (component == null) { members.Add(new InspectorRowView.Entry { Label = "Missing script", Hint = "Component cannot be inspected" }); continue; }
                var key = "C" + component.GetInstanceID();
                var type = component.GetType();
                var componentMatch = type.Name.IndexOf(MemberSearch.text, StringComparison.OrdinalIgnoreCase) >= 0;
                members.Add(new InspectorRowView.Entry { Label = (expanded.Contains(key) ? "> " : "v ") + type.Name, Hint = "Component / " + component.GetInstanceID(), Action = () => Toggle(key) });
                if (expanded.Contains(key)) continue;
                if (component is Behaviour) AddMember(InspectionMember.Adapter(component, "enabled", true), key + "/enabled", 0, new List<object>(), componentMatch);
                if (component is Transform)
                {
                    foreach (var name in new[] { "localPosition", "localEulerAngles", "localScale", "position" })
                        AddMember(InspectionMember.Adapter(component, name, name != "position"), key + "/" + name, 0, new List<object>(), componentMatch);
                    if (component is RectTransform)
                        foreach (var name in new[] { "anchoredPosition", "sizeDelta", "anchorMin", "anchorMax", "pivot" })
                            AddMember(InspectionMember.Adapter(component, name, true), key + "/" + name, 0, new List<object>(), componentMatch, "LayoutGroup / Animator may overwrite this value");
                }
                else if (component is MonoBehaviour)
                    foreach (var field in descriptors.Fields(type)) AddMember(InspectionMember.Field(component, field), key + "/" + field.DeclaringType.FullName + "." + field.Name, 0, new List<object>(), componentMatch);
            }
            var height = 0f; foreach (var member in members) { offsets.Add(height); height += member.Height; }
            InspectorScroll.content.sizeDelta = new Vector2(0, height);
        }
        private void Toggle(string key) { CancelDrafts(); if (!expanded.Remove(key)) expanded.Add(key); BuildInspector(); }
        private void AddMember(InspectionMember member, string key, int depth, List<object> ancestors, bool include = false, string hint = null)
        {
            if (!member.TryRead(out var value, out var error))
            { members.Add(new InspectorRowView.Entry { Label = member.Name, Member = member, Error = error }); return; }
            var type = member.ValueType;
            var elementType = InspectionValues.ElementType(type);
            var composite = InspectionValues.IsComposite(type);
            var expandable = value != null && (elementType != null || composite);
            var matches = include || string.IsNullOrEmpty(MemberSearch.text) || member.Name.IndexOf(MemberSearch.text, StringComparison.OrdinalIgnoreCase) >= 0;
            if (!matches && !expandable) return;
            var entry = new InspectorRowView.Entry { Label = new string(' ', depth * 2) + member.Name, Member = member, Hint = hint };
            members.Add(entry);
            if (typeof(Object).IsAssignableFrom(type))
            {
                entry.Hint = "Object reference / read only";
                if (value is GameObject || value is Component) entry.Action = () =>
                {
                    if (!member.TryRead(out var reference, out _) || !(reference is Object obj) || obj == null) return;
                    var target = obj is Component c ? c.gameObject : obj as GameObject;
                    Search.SetTextWithoutNotify(""); activeOnly = false; sceneFilter = null; SceneLabel.text = "All scenes";
                    session.Reveal(target); pendingReveal = target; revision = -1; mobileInspector = true;
                };
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
            entry.Label = (expanded.Contains(key) ? "v " : "> ") + entry.Label;
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
