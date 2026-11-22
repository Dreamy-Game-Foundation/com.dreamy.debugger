using System;
using System.Linq;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI.Other;
using Dreamy.Debugger.SceneInspection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Editor
{
    /// <summary>Explicit maintainer authoring; never runs on package import or touches the active scene.</summary>
    public static class SceneInspectionAssets
    {
        public const string TabPath = "Packages/com.dreamy.debugger/Runtime/Prefabs/SceneInspection/SceneTab.prefab";
        public const string Key = "DreamyDebugger/UI/Prefabs/Tabs/Scene";
        private const string Root = "Packages/com.dreamy.debugger/Runtime/Prefabs/SceneInspection";
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dreamy.debugger/Runtime/Branding/Material/Barlow-Regular.ttf");
        private static Font Mono => AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dreamy.debugger/Runtime/Branding/Material/JetBrainsMono-Regular.ttf");
        [MenuItem("Tools/Dreamy Debugger/Author Scene Inspection UI")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring Scene UI.");
            var info = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.dreamy.debugger/package.json");
            if (info == null || info.source != UnityEditor.PackageManager.PackageSource.Local && info.source != UnityEditor.PackageManager.PackageSource.Embedded)
                throw new InvalidOperationException("Authoring requires a writable local/embedded maintainer package.");
            if (!AssetDatabase.IsValidFolder(Root)) AssetDatabase.CreateFolder("Packages/com.dreamy.debugger/Runtime/Prefabs", "SceneInspection");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Scene", typeof(RectTransform), typeof(DebugTab), typeof(SceneTabController));
                SceneManager.MoveGameObjectToScene(root, scene); Stretch((RectTransform)root.transform);
                var tab = root.GetComponent<DebugTab>(); tab.SortIndex = 3; tab.IconStyleKey = "Icon_Options";
                var serialized = new SerializedObject(tab);
                serialized.FindProperty("_key").stringValue = "Scene"; serialized.FindProperty("_title").stringValue = "Scene";
                serialized.FindProperty("_longTitle").stringValue = "Scene inspection"; serialized.ApplyModifiedPropertiesWithoutUndo();
                var view = root.GetComponent<SceneTabController>();
                // Debugger shell already owns safe-area padding, input gating and EventSystem.
                var toolbar = Node(root.transform, "Scene toolbar"); Top(toolbar, 0, 54);
                Horizontal(toolbar);
                view.RefreshButton = Button(toolbar, "Refresh"); view.PauseButton = Button(toolbar, "Pause"); view.PauseLabel = view.PauseButton.GetComponentInChildren<Text>();
                view.EditButton = Button(toolbar, "Read only"); view.EditLabel = view.EditButton.GetComponentInChildren<Text>();
                view.Status = Label(root.transform, "Index status", "Open Scene to begin scanning", 13, Theme.Muted); Bottom(view.Status.rectTransform, 0, 28);
                var body = Node(root.transform, "Workspace"); Stretch(body); body.offsetMin = new Vector2(0, 30); body.offsetMax = new Vector2(0, -58);
                var left = Node(body, "Hierarchy pane", typeof(Image)); left.GetComponent<Image>().color = Theme.Surface; Stretch(left);
                left.anchorMax = new Vector2(.42f, 1); left.offsetMax = new Vector2(-5, 0); view.HierarchyPane = left;
                var right = Node(body, "Inspector pane", typeof(Image)); right.GetComponent<Image>().color = Theme.Background; Stretch(right);
                right.anchorMin = new Vector2(.42f, 0); right.offsetMin = new Vector2(5, 0); view.InspectorPane = right;
                var search = Input(left, "Search objects", "Search objects..."); Top(search.GetComponent<RectTransform>(), 8, 44); view.Search = search;
                var filters = Node(left, "Hierarchy filters"); Top(filters, 58, 44); Horizontal(filters);
                view.ActiveFilter = Button(filters, "All objects"); view.ActiveLabel = view.ActiveFilter.GetComponentInChildren<Text>();
                view.SceneFilter = Button(filters, "All scenes"); view.SceneLabel = view.SceneFilter.GetComponentInChildren<Text>();
                view.HierarchyScroll = Scroll(left, "Hierarchy", 108, 4);
                var tree = BuildHierarchyRow(scene); view.HierarchyTemplate = PrefabUtility.SaveAsPrefabAsset(tree.gameObject, Root + "/HierarchyRow.prefab").GetComponent<HierarchyRowView>();
                Object.DestroyImmediate(tree.gameObject);
                var headerActions = Node(right, "Object actions"); Top(headerActions, 4, 44); Horizontal(headerActions);
                view.BackButton = Button(headerActions, "Back"); view.CopyPath = Button(headerActions, "Copy path");
                view.Header = Label(right, "Object breadcrumb", "Select an object to inspect", 14, Theme.Ivory); Top(view.Header.rectTransform, 52, 90);
                view.Header.horizontalOverflow = HorizontalWrapMode.Wrap; view.Header.verticalOverflow = VerticalWrapMode.Truncate;
                view.MemberSearch = Input(right, "Filter members", "Filter components / members..."); Top((RectTransform)view.MemberSearch.transform, 146, 44);
                view.InspectorScroll = Scroll(right, "Inspector", 196, 4);
                var member = BuildMemberRow(scene); view.InspectorTemplate = PrefabUtility.SaveAsPrefabAsset(member.gameObject, Root + "/InspectorValueRow.prefab").GetComponent<InspectorRowView>();
                Object.DestroyImmediate(member.gameObject);
                var handle = Node(body, "Resize panes", typeof(Image), typeof(SceneSplitHandle));
                handle.GetComponent<Image>().color = Theme.Border; handle.anchorMin = new Vector2(.42f, 0); handle.anchorMax = new Vector2(.42f, 1);
                handle.offsetMin = new Vector2(-4, 0); handle.offsetMax = new Vector2(4, 0);
                handle.GetComponent<SceneSplitHandle>().Owner = view; view.SplitHandle = handle;
                PrefabUtility.SaveAsPrefabAsset(root, TabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Register(AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath));
        }
        public static void Register(DebuggerAssetCatalog catalog)
        {
            if (catalog == null) throw new InvalidOperationException("Debugger catalog is missing");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TabPath);
            if (prefab == null) return; // Old catalogs remain usable until the maintainer authors the feature.
            catalog.Entries = catalog.Entries.Where(e => e.Key != Key).Concat(new[] { new CatalogEntry { Key = Key, Asset = prefab } }).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        }
        private static HierarchyRowView BuildHierarchyRow(Scene scene)
        {
            var root = new GameObject("Hierarchy row", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HierarchyRowView));
            SceneManager.MoveGameObjectToScene(root, scene); RowRect((RectTransform)root.transform, 52);
            var row = root.GetComponent<HierarchyRowView>(); row.Background = root.GetComponent<Image>(); row.Background.color = Theme.Surface;
            row.Select = root.GetComponent<Button>(); row.Select.targetGraphic = row.Background;
            row.Expand = Button(root.transform, ">"); var expand = (RectTransform)row.Expand.transform;
            expand.anchorMin = expand.anchorMax = new Vector2(0, .5f); expand.pivot = new Vector2(0, .5f); expand.sizeDelta = new Vector2(36, 44); expand.anchoredPosition = new Vector2(8, 0);
            row.Arrow = row.Expand.GetComponentInChildren<Text>();
            row.Caption = Label(root.transform, "Object name", "GameObject", 16, Theme.Ivory); Stretch(row.Caption.rectTransform); row.Caption.rectTransform.offsetMin = new Vector2(44, 20);
            row.Context = Label(root.transform, "Scene and path", "Scene", 12, Theme.Muted); Stretch(row.Context.rectTransform); row.Context.rectTransform.offsetMin = new Vector2(44, 2); row.Context.rectTransform.offsetMax = new Vector2(-8, -30);
            return row;
        }
        private static InspectorRowView BuildMemberRow(Scene scene)
        {
            var root = new GameObject("Inspector value row", typeof(RectTransform), typeof(Image), typeof(InspectorRowView));
            SceneManager.MoveGameObjectToScene(root, scene); RowRect((RectTransform)root.transform, 108);
            root.GetComponent<Image>().color = Theme.Surface;
            var row = root.GetComponent<InspectorRowView>();
            row.Caption = Label(root.transform, "Member name", "Member", 15, Theme.Ivory); Top(row.Caption.rectTransform, 2, 24);
            row.Hint = Label(root.transform, "Member status", "Read only", 12, Theme.Muted); Bottom(row.Hint.rectTransform, 2, 24);
            var actions = Node(root.transform, "Value and actions"); Top(actions, 28, 44); Horizontal(actions);
            row.Actions = actions;
            row.Value = Input(actions, "Value", "Value"); row.Value.characterLimit = 4096;
            row.Value.textComponent.font = Mono ?? Font;
            var layout = row.Value.gameObject.AddComponent<LayoutElement>(); layout.flexibleWidth = 4; layout.minWidth = 72;
            var toggleRect = Node(actions, "Boolean", typeof(Image), typeof(Toggle), typeof(LayoutElement));
            toggleRect.GetComponent<Image>().color = Theme.Control;
            toggleRect.GetComponent<LayoutElement>().flexibleWidth = 4; toggleRect.GetComponent<LayoutElement>().minWidth = 72;
            row.Boolean = toggleRect.GetComponent<Toggle>(); row.Boolean.targetGraphic = toggleRect.GetComponent<Image>();
            var selected = Node(toggleRect, "Selected", typeof(Image)); Stretch(selected); selected.GetComponent<Image>().color = Theme.Selected;
            selected.GetComponent<Image>().raycastTarget = false; row.Boolean.graphic = selected.GetComponent<Image>();
            row.BooleanLabel = Label(toggleRect, "Boolean state", "False", 15, Theme.Ivory); Stretch(row.BooleanLabel.rectTransform);
            row.BooleanLabel.alignment = TextAnchor.MiddleCenter;
            var dropdown = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdown.name = "Enumeration"; dropdown.transform.SetParent(actions, false);
            var dropdownLayout = dropdown.AddComponent<LayoutElement>(); dropdownLayout.flexibleWidth = 4; dropdownLayout.minWidth = 72;
            row.Enumeration = dropdown.GetComponent<Dropdown>();
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true)) { text.font = Font; text.fontSize = 15; text.color = Theme.Ivory; }
            foreach (var image in dropdown.GetComponentsInChildren<Image>(true)) image.color = Theme.Control;
            var arrow = dropdown.transform.Find("Arrow"); if (arrow != null) Object.DestroyImmediate(arrow.gameObject);
            var chevron = Label(dropdown.transform, "Chevron", "v", 14, Theme.Muted);
            chevron.rectTransform.anchorMin = new Vector2(1, 0); chevron.rectTransform.anchorMax = Vector2.one;
            chevron.rectTransform.offsetMin = new Vector2(-24, 0); chevron.rectTransform.offsetMax = Vector2.zero;
            var swatch = Node(actions, "Color preview", typeof(Image), typeof(LayoutElement));
            swatch.GetComponent<LayoutElement>().minWidth = swatch.GetComponent<LayoutElement>().preferredWidth = 44;
            row.ColorPreview = swatch.GetComponent<Image>(); row.ColorPreview.raycastTarget = false;
            row.Action = Button(actions, "Open"); row.ActionLabel = row.Action.GetComponentInChildren<Text>();
            row.Apply = Button(actions, "Apply"); row.Cancel = Button(actions, "Reload");
            return row;
        }
        private static void RowRect(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(0, height); rect.anchoredPosition = Vector2.zero;
        }
        private static ScrollRect Scroll(Transform parent, string name, float top, float bottom)
        {
            var rect = Node(parent, name, typeof(Image), typeof(ScrollRect)); Stretch(rect);
            rect.offsetMin = new Vector2(4, bottom); rect.offsetMax = new Vector2(-4, -top);
            rect.GetComponent<Image>().color = Theme.Background;
            var viewport = Node(rect, "Viewport", typeof(RectMask2D)); Stretch(viewport);
            var content = Node(viewport, "Content"); RowRect(content, 0);
            var scroll = rect.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            return scroll;
        }
        private static RectTransform Node(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray()); go.layer = 5;
            go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Top(RectTransform rect, float y, float height)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, 1);
            rect.offsetMin = new Vector2(8, -y - height); rect.offsetMax = new Vector2(-8, -y);
        }
        private static void Bottom(RectTransform rect, float y, float height)
        {
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchorMax = new Vector2(1, 0);
            rect.offsetMin = new Vector2(8, y); rect.offsetMax = new Vector2(-8, y + height);
        }
        private static void Horizontal(RectTransform rect)
        {
            var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        }
        private static Text Label(Transform parent, string name, string text, int size, Color color)
        {
            var rect = Node(parent, name, typeof(Text)); var label = rect.GetComponent<Text>();
            label.text = text; label.font = Font; label.fontSize = size; label.color = color;
            label.alignment = TextAnchor.MiddleLeft; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Truncate;
            return label;
        }
        private static Button Button(Transform parent, string caption)
        {
            var rect = Node(parent, caption, typeof(Image), typeof(Button), typeof(LayoutElement));
            var size = rect.GetComponent<LayoutElement>(); size.minWidth = 48; size.flexibleWidth = 1;
            var image = rect.GetComponent<Image>(); image.color = Theme.Control;
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.dreamy.debugger/Runtime/Branding/Material/rounded-fill.png"); image.type = Image.Type.Sliced;
            var button = rect.GetComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f); colors.selectedColor = Theme.Selected; button.colors = colors;
            var label = Label(rect, "Label", caption, 14, Theme.Ivory); Stretch(label.rectTransform); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static InputField Input(Transform parent, string name, string placeholder)
        {
            var rect = Node(parent, name, typeof(Image), typeof(InputField)); rect.GetComponent<Image>().color = Theme.Control;
            var text = Label(rect, "Value text", "", 15, Theme.Ivory); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(8, 0); text.rectTransform.offsetMax = new Vector2(-8, 0);
            var hint = Label(rect, "Placeholder", placeholder, 14, Theme.Muted); Stretch(hint.rectTransform); hint.rectTransform.offsetMin = new Vector2(8, 0);
            var field = rect.GetComponent<InputField>(); field.textComponent = text; field.placeholder = hint; field.targetGraphic = rect.GetComponent<Image>();
            field.lineType = InputField.LineType.SingleLine; return field;
        }
    }
}
