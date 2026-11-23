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
            AuthorHierarchyIcon();
            AuthorObjectIcon();
            ImportInspectorIcons();
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Scene", typeof(RectTransform), typeof(DebugTab), typeof(SceneTabController));
                SceneManager.MoveGameObjectToScene(root, scene); Stretch((RectTransform)root.transform);
                var tab = root.GetComponent<DebugTab>(); tab.SortIndex = 3; tab.IconStyleKey = "Icon_Hierarchy";
                var serialized = new SerializedObject(tab);
                serialized.FindProperty("_key").stringValue = "Scene"; serialized.FindProperty("_title").stringValue = "Hierarchy";
                serialized.FindProperty("_longTitle").stringValue = "Hierarchy"; serialized.ApplyModifiedPropertiesWithoutUndo();
                var view = root.GetComponent<SceneTabController>();
                // Debugger shell already owns safe-area padding, input gating and EventSystem.
                var body = Node(root.transform, "Workspace"); Stretch(body);
                var left = Node(body, "Hierarchy pane", typeof(Image)); left.GetComponent<Image>().color = Theme.Surface; Stretch(left);
                left.anchorMax = new Vector2(.27f, 1); left.offsetMax = new Vector2(-1, 0); view.HierarchyPane = left;
                var right = Node(body, "Inspector pane", typeof(Image)); right.GetComponent<Image>().color = Theme.Background; Stretch(right);
                right.anchorMin = new Vector2(.27f, 0); right.offsetMin = new Vector2(1, 0); view.InspectorPane = right;
                view.Search = Input(left, "Search objects", "Search objects"); Top((RectTransform)view.Search.transform, 8, 40);
                view.HierarchyScroll = Scroll(left, "Hierarchy", 56, 4);
                view.HierarchyScroll.GetComponent<Image>().color = Theme.Surface;
                var tree = BuildHierarchyRow(scene); view.HierarchyTemplate = PrefabUtility.SaveAsPrefabAsset(tree.gameObject, Root + "/HierarchyRow.prefab").GetComponent<HierarchyRowView>();
                Object.DestroyImmediate(tree.gameObject);

                view.BackButton = Button(root.transform, "Objects"); UpperCorner((RectTransform)view.BackButton.transform, false, 100, 44);
                var backArrow = Glyph(view.BackButton.transform, "Back arrow", "chevron", 12);
                backArrow.rectTransform.localEulerAngles = new Vector3(0, 0, 180);
                view.BackButton.GetComponent<Image>().color = Color.clear;
                var objectHeader = Node(right, "Object header"); Top(objectHeader, 8, 42); Horizontal(objectHeader);
                view.ObjectHeader = objectHeader;
                objectHeader.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                var objectIcon = Node(objectHeader, "Selected object icon", typeof(Image), typeof(LayoutElement));
                objectIcon.GetComponent<LayoutElement>().preferredWidth = 26;
                var image = objectIcon.GetComponent<Image>(); image.raycastTarget = false; image.preserveAspect = true;
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/hierarchy-object.png");
                view.Header = Label(objectHeader, "Object name", "Select an object to inspect", 25, Theme.Ivory);
                view.Header.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                view.Header.horizontalOverflow = HorizontalWrapMode.Wrap;
                var active = Node(objectHeader, "Active", typeof(LayoutElement));
                var activeSize = active.GetComponent<LayoutElement>();
                activeSize.minWidth = activeSize.preferredWidth = 80;
                activeSize.flexibleWidth = 0;
                Horizontal(active);
                active.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                view.ObjectActive = Checkbox(active, "Active checkbox", 26, out _);
                var activeText = Label(active, "Active label", "Active", 14, Theme.Muted);
                activeText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var metadata = Node(right, "Object metadata"); Top(metadata, 55, 38); Horizontal(metadata);
                view.Metadata = metadata;
                view.ObjectTag = Choice(metadata, "Tag", out view.ObjectTagLabel);
                view.ObjectLayer = Choice(metadata, "Layer", out view.ObjectLayerLabel);
                view.MemberSearch = Input(right, "Find properties", "Find properties"); Top((RectTransform)view.MemberSearch.transform, 100, 36);
                view.MemberSearch.gameObject.SetActive(false);
                view.InspectorScroll = Scroll(right, "Inspector", 100, 4);
                var controls = Node(right, "Draft actions", typeof(Image)); Bottom(controls, 3, 44); Horizontal(controls);
                controls.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                controls.GetComponent<Image>().color = Theme.Surface;
                view.DraftBar = controls;
                view.DraftLabel = Label(controls, "Draft property", "", 14, Theme.Muted);
                view.DraftLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                view.DraftLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
                view.DiscardButton = Button(controls, "Cancel"); FixedWidth(view.DiscardButton, 68);
                view.ApplyButton = Button(controls, "Apply"); FixedWidth(view.ApplyButton, 68);
                view.ApplyButton.GetComponent<Image>().color = Theme.Accent;
                controls.gameObject.SetActive(false);
                view.Status = Label(root.transform, "Inspection status", "", 13, Theme.Muted);
                Bottom(view.Status.rectTransform, 2, 26);
                view.Status.alignment = TextAnchor.MiddleCenter;
                view.Status.horizontalOverflow = HorizontalWrapMode.Wrap;
                var member = BuildMemberRow(scene); view.InspectorTemplate = PrefabUtility.SaveAsPrefabAsset(member.gameObject, Root + "/InspectorValueRow.prefab").GetComponent<InspectorRowView>();
                Object.DestroyImmediate(member.gameObject);
                var handle = Node(body, "Resize panes", typeof(Image), typeof(SceneSplitHandle));
                handle.GetComponent<Image>().color = Theme.Border; handle.anchorMin = new Vector2(.3f, 0); handle.anchorMax = new Vector2(.3f, 1);
                handle.offsetMin = new Vector2(-1, 0); handle.offsetMax = new Vector2(1, 0);
                handle.GetComponent<SceneSplitHandle>().Owner = view; view.SplitHandle = handle;
                BuildWorkspaceMenu(root.transform, view);
                view.NumberEditor = BuildTouchEditor(root.transform);
                view.Picker = BuildPicker(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root, TabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            Register(AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath));
        }
        private static void BuildWorkspaceMenu(Transform parent, SceneTabController view)
        {
            view.MenuButton = Button(parent, "•••"); UpperCorner((RectTransform)view.MenuButton.transform, true, 44, 44);
            view.MenuButton.GetComponent<Image>().color = Color.clear;
            var overlay = Node(parent, "Hierarchy actions"); Stretch(overlay);
            var scrim = Button(overlay, "Close hierarchy actions"); Stretch((RectTransform)scrim.transform);
            scrim.GetComponent<Image>().color = Color.clear;
            scrim.GetComponentInChildren<Text>().enabled = false;
            scrim.transition = Selectable.Transition.None;
            view.MenuDismiss = scrim;
            var area = Node(overlay, "Commands", typeof(Image)); UpperCorner(area, true, 240, 410, 48);
            area.GetComponent<Image>().color = Theme.Surface;
            var list = Scroll(area, "Actions", 4, 4);
            list.GetComponent<Image>().color = Theme.Surface;
            var commands = list.content;
            view.RefreshButton = MenuCommand(commands, "Refresh", 0);
            view.UndoButton = MenuCommand(commands, "Undo", 1);
            view.RedoButton = MenuCommand(commands, "Redo", 2);
            view.CopyPath = MenuCommand(commands, "Copy object path", 3);
            view.ToggleFind = MenuCommand(commands, "Find properties", 4);
            view.TogglePropertyActions = MenuCommand(commands, "Show Copy and Pin", 5);
            view.CompareButton = MenuCommand(commands, "Compare", 6); view.CompareLabel = view.CompareButton.GetComponentInChildren<Text>();
            view.PauseButton = MenuCommand(commands, "Pause inspection", 7); view.PauseLabel = view.PauseButton.GetComponentInChildren<Text>();
            view.ActiveFilter = MenuCommand(commands, "All objects", 8); view.ActiveLabel = view.ActiveFilter.GetComponentInChildren<Text>();
            view.SceneFilter = MenuCommand(commands, "All scenes", 9); view.SceneLabel = view.SceneFilter.GetComponentInChildren<Text>();
            commands.sizeDelta = new Vector2(0, 442);
            view.Menu = (RectTransform)overlay;
            view.MenuPanel = area;
            overlay.gameObject.SetActive(false);
        }

        private static Button MenuCommand(Transform parent, string title, int index)
        {
            var button = Button(parent, title); Top((RectTransform)button.transform, index * 44, 42);
            button.GetComponent<Image>().color = Theme.Surface;
            var label = button.GetComponentInChildren<Text>();
            label.alignment = TextAnchor.MiddleLeft;
            label.rectTransform.offsetMin = new Vector2(9, 0);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            return button;
        }

        private static Button Choice(Transform parent, string name, out Text value)
        {
            var group = Node(parent, name, typeof(LayoutElement)); Horizontal(group);
            group.GetComponent<LayoutElement>().flexibleWidth = 1;
            group.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            var caption = Label(group, name + " caption", name, 14, Theme.Muted);
            caption.gameObject.AddComponent<LayoutElement>().preferredWidth = 32;
            var button = Button(group, name + " value");
            value = button.GetComponentInChildren<Text>();
            value.alignment = TextAnchor.MiddleLeft;
            value.horizontalOverflow = HorizontalWrapMode.Wrap;
            value.rectTransform.offsetMin = new Vector2(8, 0);
            value.rectTransform.offsetMax = new Vector2(-22, 0);
            var arrow = Glyph(button.transform, "Dropdown arrow", "chevron", -10);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = new Vector2(1, .5f);
            arrow.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
            return button;
        }

        private static Toggle Checkbox(Transform parent, string name, float width, out Text mark)
        {
            var rect = Node(parent, name, typeof(Toggle), typeof(Image), typeof(LayoutElement));
            var size = rect.GetComponent<LayoutElement>(); size.preferredWidth = width; size.minWidth = width;
            var background = rect.GetComponent<Image>(); background.color = Color.clear;
            var box = Node(rect, "Checkbox outline", typeof(Image));
            box.anchorMin = box.anchorMax = new Vector2(0, .5f); box.pivot = new Vector2(0, .5f);
            box.sizeDelta = new Vector2(18, 18); box.anchoredPosition = new Vector2(2, 0);
            box.GetComponent<Image>().color = Theme.Control; box.GetComponent<Image>().raycastTarget = false;
            mark = Label(box, "Check", "✓", 15, Theme.Ivory); Stretch(mark.rectTransform); mark.alignment = TextAnchor.MiddleCenter;
            var toggle = rect.GetComponent<Toggle>(); toggle.targetGraphic = background; toggle.graphic = mark;
            return toggle;
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
            SceneManager.MoveGameObjectToScene(root, scene); RowRect((RectTransform)root.transform, 44);
            var row = root.GetComponent<HierarchyRowView>(); row.Background = root.GetComponent<Image>(); row.Background.color = Theme.Surface;
            row.Select = root.GetComponent<Button>(); row.Select.targetGraphic = row.Background;
            row.Expand = Button(root.transform, "Expand object"); var expand = (RectTransform)row.Expand.transform;
            expand.anchorMin = expand.anchorMax = new Vector2(0, .5f); expand.pivot = new Vector2(0, .5f); expand.sizeDelta = new Vector2(36, 44); expand.anchoredPosition = new Vector2(8, 0);
            row.Expand.GetComponentInChildren<Text>().enabled = false;
            row.Expand.GetComponent<Image>().color = Color.clear;
            row.Arrow = Glyph(expand, "Foldout", "chevron", 0, true);
            var icon = Node(root.transform,"Object icon",typeof(Image)); icon.anchorMin = icon.anchorMax = new Vector2(0,.5f); icon.sizeDelta = new Vector2(16,16);
            row.Icon = icon.GetComponent<Image>(); row.Icon.raycastTarget = false;
            row.ObjectIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.dreamy.debugger/Runtime/Branding/Material/hierarchy-object.png");
            row.CameraIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.dreamy.debugger/Runtime/Branding/Material/camera.png");
            row.Icon.sprite = row.ObjectIcon;
            row.Caption = Label(root.transform, "Object name", "GameObject", 16, Theme.Ivory); Stretch(row.Caption.rectTransform); row.Caption.rectTransform.offsetMin = new Vector2(44, 20);
            row.Context = Label(root.transform, "Scene and path", "Scene", 12, Theme.Muted); Stretch(row.Context.rectTransform); row.Context.rectTransform.offsetMin = new Vector2(44, 2); row.Context.rectTransform.offsetMax = new Vector2(-8, -30);
            return row;
        }
        private static InspectorRowView BuildMemberRow(Scene scene)
        {
            var root = new GameObject("Inspector value row", typeof(RectTransform), typeof(Image), typeof(InspectorRowView));
            SceneManager.MoveGameObjectToScene(root, scene); RowRect((RectTransform)root.transform, 92);
            root.GetComponent<Image>().color = Color.clear;
            var row = root.GetComponent<InspectorRowView>();
            var separator = Node(root.transform, "Section divider", typeof(Image));
            Top(separator, 0, 1); separator.GetComponent<Image>().color = Theme.Border;
            separator.GetComponent<Image>().raycastTarget = false;
            row.SectionDivider = separator.gameObject;
            row.Caption = Label(root.transform, "Member name", "Member", 15, Theme.Ivory); Top(row.Caption.rectTransform, 2, 24);
            row.Caption.raycastTarget = true; row.Caption.supportRichText = false;
            row.LabelHandle = row.Caption.gameObject.AddComponent<NumericDragHandle>();
            row.Hint = Label(root.transform, "Member status", "Read only", 12, Theme.Muted); Bottom(row.Hint.rectTransform, 2, 24);
            var actions = Node(root.transform, "Value and actions"); Top(actions, 28, 44); Horizontal(actions);
            actions.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            row.Actions = actions;
            row.Value = Input(actions, "Value", "Value", true); row.Value.characterLimit = 4096;
            row.Value.textComponent.font = Mono ?? Font;
            var layout = row.Value.gameObject.AddComponent<LayoutElement>(); layout.flexibleWidth = 4; layout.minWidth = 72;
            row.StepDown = Button(actions,"−"); FixedWidth(row.StepDown,32);
            row.StepUp = Button(actions,"+"); FixedWidth(row.StepUp,32);
            var vector = Node(actions, "Vector axes", typeof(LayoutElement)); Horizontal(vector);
            vector.GetComponent<LayoutElement>().flexibleWidth = 4; vector.GetComponent<LayoutElement>().minWidth = 144;
            row.VectorControls = vector; row.AxisInputs = new InputField[4]; row.AxisHandles = new NumericDragHandle[4];
            var axisNames = new[] { "X", "Y", "Z", "W" };
            var axisColors = new[] { new Color(.94f,.6f,.62f), new Color(.5f,.82f,.65f), new Color(.55f,.72f,.94f), Theme.Muted };
            for (var i = 0; i < 4; i++)
            {
                var input = Input(vector, axisNames[i], "0", true); input.characterLimit = 128;
                input.textComponent.font = Mono ?? Font; input.textComponent.fontSize = 13;
                input.textComponent.rectTransform.offsetMin = new Vector2(22, 0);
                var element = input.gameObject.AddComponent<LayoutElement>(); element.minWidth = 36; element.flexibleWidth = 1;
                var label = Label(input.transform, "Axis label", axisNames[i], 12, axisColors[i]);
                label.raycastTarget = true; label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = new Vector2(0,1); label.rectTransform.offsetMin = new Vector2(3,0); label.rectTransform.offsetMax = new Vector2(22,0);
                row.AxisInputs[i] = input; row.AxisHandles[i] = label.gameObject.AddComponent<NumericDragHandle>();
            }
            row.Boolean = Checkbox(actions, "Boolean", 40, out row.BooleanLabel);
            var dropdown = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdown.name = "Enumeration"; dropdown.transform.SetParent(actions, false);
            var dropdownLayout = dropdown.AddComponent<LayoutElement>(); dropdownLayout.flexibleWidth = 4; dropdownLayout.minWidth = 72;
            row.Enumeration = dropdown.GetComponent<Dropdown>();
            foreach (var text in dropdown.GetComponentsInChildren<Text>(true)) { text.font = Font; text.fontSize = 15; text.color = Theme.Ivory; }
            foreach (var image in dropdown.GetComponentsInChildren<Image>(true)) image.color = Theme.Control;
            var arrow = dropdown.transform.Find("Arrow"); if (arrow != null) Object.DestroyImmediate(arrow.gameObject);
            var chevron = Glyph(dropdown.transform, "Dropdown arrow", "chevron", -12);
            chevron.rectTransform.anchorMin = chevron.rectTransform.anchorMax = new Vector2(1, .5f);
            chevron.rectTransform.localEulerAngles = new Vector3(0, 0, -90);
            var swatch = Node(actions, "Color preview", typeof(Image), typeof(LayoutElement));
            swatch.GetComponent<LayoutElement>().minWidth = swatch.GetComponent<LayoutElement>().preferredWidth = 44;
            row.ColorPreview = swatch.GetComponent<Image>(); row.ColorPreview.raycastTarget = false;
            row.Action = Button(actions, "Open"); row.Action.GetComponent<Image>().color = Color.clear; row.ActionLabel = row.Action.GetComponentInChildren<Text>();
            row.FoldoutIcon = Glyph(row.Action.transform, "Foldout", "chevron", 14);
            row.ComponentIcon = Glyph(row.Action.transform, "Component icon", "component", 36);
            row.ComponentSprites = InspectorIcons().Select(path => AssetDatabase.LoadAssetAtPath<Sprite>(path)).ToArray();
            row.PickerButton = Button(actions, ""); row.PickerLabel = row.PickerButton.GetComponentInChildren<Text>();
            row.PickerLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            row.CopyButton = Button(actions, "Copy"); FixedWidth(row.CopyButton, 48);
            row.PinButton = Button(actions, "Pin"); FixedWidth(row.PinButton, 44);
            row.Apply = Button(actions, "Apply"); FixedWidth(row.Apply, 56);
            row.Cancel = Button(actions, "Reload"); FixedWidth(row.Cancel, 60);
            return row;
        }
        private const string IconFolder = "Packages/com.dreamy.debugger/Runtime/Branding/Material";
        private static System.Collections.Generic.IEnumerable<string> InspectorIcons() =>
            AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => System.IO.Path.GetFileName(path).StartsWith("inspector-", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal);

        private static void ImportInspectorIcons()
        {
            foreach (var path in InspectorIcons())
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer.textureType == TextureImporterType.Sprite) continue;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
        }

        private static Image Glyph(Transform parent, string name, string icon, float x, bool centered = false)
        {
            var rect = Node(parent, name, typeof(Image));
            rect.anchorMin = rect.anchorMax = new Vector2(centered ? .5f : 0, .5f);
            rect.anchoredPosition = new Vector2(x, 0);
            rect.sizeDelta = new Vector2(16, 16);
            var image = rect.GetComponent<Image>();
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/inspector-" + icon + ".png");
            image.color = Theme.Ivory;
            image.raycastTarget = false;
            return image;
        }

        [MenuItem("Tools/Dreamy Debugger/Copy project tags to selected debugger configurations")]
        public static void CopyProjectTags()
        {
            var tags = UnityEditorInternal.InternalEditorUtility.tags;
            foreach (var config in Selection.objects.OfType<DebuggerConfiguration>())
            {
                UnityEditor.Undo.RecordObject(config, "Copy Hierarchy project tags");
                config.HierarchyProjectTags = tags;
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssetIfDirty(config);
            }
        }

        private static InspectionPicker BuildPicker(Transform parent)
        {
            var root = Node(parent, "Inspector picker", typeof(InspectionPicker));
            Stretch(root);
            var picker = root.GetComponent<InspectionPicker>();
            picker.Scrim = Button(root, "Dismiss picker");
            Stretch((RectTransform)picker.Scrim.transform);
            picker.Scrim.GetComponentInChildren<Text>().enabled = false;
            var scrim = picker.Scrim.GetComponent<Image>();
            scrim.sprite = null; scrim.color = new Color(0, 0, 0, .63f);
            picker.Scrim.transition = Selectable.Transition.None;
            var panel = Node(root, "Picker sheet", typeof(Image));
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.anchoredPosition = Vector2.zero;
            panel.GetComponent<Image>().color = Theme.Surface;
            picker.Surface = panel;

            picker.Title = Label(panel, "Property", "", 17, Theme.Ivory);
            Top(picker.Title.rectTransform, 4, 28);
            var footer = Node(panel, "Actions"); Bottom(footer, 4, 48); Horizontal(footer);
            picker.Cancel = Button(footer, "Cancel"); picker.Apply = Button(footer, "Apply");
            picker.Apply.GetComponent<Image>().color = Theme.Accent;
            var list = Scroll(panel, "Picker options", 36, 56);
            var content = list.content;
            picker.Search = Input(content, "Filter options", "Search");
            Top((RectTransform)picker.Search.transform, 4, 42);
            picker.Rows = new Button[5];
            for (var i = 0; i < picker.Rows.Length; i++)
            {
                var button = picker.Rows[i] = Button(content, "Option " + (i + 1));
                Top((RectTransform)button.transform, 50 + i * 48, 44);
                var label = button.GetComponentInChildren<Text>();
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.verticalOverflow = VerticalWrapMode.Truncate;
            }
            var pager = Node(content, "Page controls"); Top(pager, 292, 44); Horizontal(pager);
            picker.Previous = Button(pager, "Previous"); picker.Next = Button(pager, "Next");
            picker.PageLabel = Label(pager, "Page", "", 14, Theme.Ivory);
            picker.PageLabel.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            picker.PageLabel.alignment = TextAnchor.MiddleCenter;
            picker.PageLabel.transform.SetSiblingIndex(1);
            picker.ColorFields = Node(content, "RGBA controls"); Top(picker.ColorFields, 4, 214);
            picker.Channels = new InputField[4];
            picker.ChannelSliders = new Slider[4];
            for (var i = 0; i < 4; i++)
            {
                var rgba = Node(picker.ColorFields, "Channel " + "RGBA"[i]);
                Top(rgba, i * 44, 40);
                Horizontal(rgba);
                rgba.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
                var label = Label(rgba, "Channel name", "RGBA"[i].ToString(), 16, Theme.Ivory);
                label.gameObject.AddComponent<LayoutElement>().preferredWidth = 22;
                var sliderObject = DefaultControls.CreateSlider(new DefaultControls.Resources());
                sliderObject.transform.SetParent(rgba, false);
                sliderObject.name = "Slider";
                sliderObject.AddComponent<LayoutElement>().flexibleWidth = 1;
                var slider = picker.ChannelSliders[i] = sliderObject.GetComponent<Slider>();
                slider.targetGraphic.color = Theme.Ivory;
                slider.fillRect.GetComponent<Image>().color = Theme.Accent;
                sliderObject.transform.Find("Background").GetComponent<Image>().color = Theme.Control;
                var input = picker.Channels[i] = Input(rgba, "Value", "");
                input.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
                input.gameObject.AddComponent<LayoutElement>().preferredWidth = 82;
            }
            var swatch = Node(picker.ColorFields, "Color preview", typeof(Image)); Top(swatch, 178, 32);
            picker.Swatch = swatch.GetComponent<Image>(); picker.Swatch.raycastTarget = false;
            picker.Error = Label(content, "Validation", "", 13, Theme.Warning);
            Top(picker.Error.rectTransform, 340, 36);
            picker.Error.horizontalOverflow = HorizontalWrapMode.Wrap;
            list.content.sizeDelta = new Vector2(0, 380);
            picker.Scroll = list;
            root.gameObject.SetActive(false);
            return picker;
        }

        private static TouchNumberEditor BuildTouchEditor(Transform parent)
        {
            var root = Node(parent, "Touch number editor", typeof(TouchNumberEditor));
            Stretch(root);
            var editor = root.GetComponent<TouchNumberEditor>();
            editor.SingleAxisLayout = true;
            editor.Scrim = Button(root, "Dismiss number editor");
            Stretch((RectTransform)editor.Scrim.transform);
            editor.Scrim.GetComponentInChildren<Text>().enabled = false;
            var scrim = editor.Scrim.GetComponent<Image>(); scrim.sprite = null; scrim.color = new Color(0, 0, 0, .63f);
            editor.Scrim.transition = Selectable.Transition.None;
            editor.Surface = Node(root, "Number sheet", typeof(Image));
            editor.Surface.anchorMin = editor.Surface.anchorMax = new Vector2(.5f, 0);
            editor.Surface.pivot = new Vector2(.5f, 0);
            editor.Surface.sizeDelta = new Vector2(400, 288);
            editor.Surface.GetComponent<Image>().color = Theme.Surface;
            var footer = Node(editor.Surface, "Actions"); Bottom(footer, 6, 48); Horizontal(footer);
            editor.Cancel = Button(footer, "Cancel"); editor.Apply = Button(footer, "Apply");
            editor.Apply.GetComponent<Image>().color = Theme.Accent;
            var scroll = Scroll(editor.Surface, "Number controls", 4, 58);
            var content = scroll.content;
            content.sizeDelta = new Vector2(0, 226);
            editor.Title = Label(content, "Property and axis", "Value", 18, Theme.Ivory);
            Top(editor.Title.rectTransform, 0, 28);
            var axisBar = Node(content, "Axes"); Top(axisBar, 30, 48); Horizontal(axisBar);
            editor.Axes = "XYZW".Select(axis => Button(axisBar, axis.ToString())).ToArray();
            var controls = Node(content, "Numeric controls"); Top(controls, 84, 48); Horizontal(controls);
            controls.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = false;
            editor.Minus = Button(controls, "−"); FixedWidth(editor.Minus, 48);
            editor.Number = Input(controls, "Exact value", "");
            editor.Number.keyboardType = TouchScreenKeyboardType.NumbersAndPunctuation;
            editor.Number.textComponent.font = Mono ?? Font;
            editor.Number.textComponent.alignment = TextAnchor.MiddleCenter;
            editor.Number.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            editor.Plus = Button(controls, "+"); FixedWidth(editor.Plus, 48);
            editor.Step = Button(controls, "0.1"); FixedWidth(editor.Step, 68);
            editor.StepLabel = editor.Step.GetComponentInChildren<Text>();
            var slider = DefaultControls.CreateSlider(new DefaultControls.Resources());
            slider.transform.SetParent(content, false); Top((RectTransform)slider.transform, 138, 48);
            editor.Range = slider.GetComponent<Slider>();
            editor.Range.minValue = 0; editor.Range.maxValue = 1;
            editor.Error = Label(content, "Validation", "", 13, Theme.Warning);
            editor.Error.horizontalOverflow = HorizontalWrapMode.Wrap;
            Top(editor.Error.rectTransform, 190, 36);
            root.gameObject.SetActive(false);
            return editor;
        }

        private static void AuthorHierarchyIcon()
        {
            const string path = "Packages/com.dreamy.debugger/Runtime/Branding/Material/hierarchy.png";
            // Original simple tree geometry; no Unity Editor artwork is redistributed.
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (var y = 0; y < 32; y++) for (var x = 0; x < 32; x++)
            {
                var box = x >= 12 && x <= 19 && y >= 22 && y <= 29 || y >= 2 && y <= 9 && (x >= 1 && x <= 8 || x >= 12 && x <= 19 || x >= 23 && x <= 30);
                var line = y >= 14 && y <= 16 && x >= 4 && x <= 27 || x >= 15 && x <= 16 && y >= 8 && y <= 23 || y >= 8 && y <= 16 && (x >= 4 && x <= 5 || x >= 26 && x <= 27);
                texture.SetPixel(x,y,box || line ? Color.white : Color.clear);
            }
            System.IO.File.WriteAllBytes(path.Replace("Packages/com.dreamy.debugger", UnityEditor.PackageManager.PackageInfo.FindForAssetPath(TabPath).resolvedPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            foreach (var guid in AssetDatabase.FindAssets("t:StyleSheet", new[] { "Packages/com.dreamy.debugger/Runtime" }))
            {
                var sheet = AssetDatabase.LoadAssetAtPath<Dreamy.Debugger.Foundation.UI.StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                if (sheet == null) continue;
                if (sheet.GetStyle("Icon_Hierarchy", false) == null) sheet.AddStyle("Icon_Hierarchy");
                var style = sheet.GetStyle("Icon_Hierarchy", false); var reference = sheet.GetStyle("Icon_Console");
                style.Image = icon;
                if (reference != null) { style.NormalColor = reference.NormalColor; style.HoverColor = reference.HoverColor; style.ActiveColor = reference.ActiveColor; style.DisabledColor = reference.DisabledColor; }
                EditorUtility.SetDirty(sheet); AssetDatabase.SaveAssetIfDirty(sheet);
            }
        }
        private static void AuthorObjectIcon()
        {
            const string path = "Packages/com.dreamy.debugger/Runtime/Branding/Material/hierarchy-object.png";
            var points = new[] {new Vector2(16,29),new Vector2(28,22),new Vector2(28,9),new Vector2(16,2),new Vector2(4,9),new Vector2(4,22),new Vector2(16,16)};
            var edges = new[]{0,1,1,2,2,3,3,4,4,5,5,0,5,6,1,6,6,3};
            var texture = new Texture2D(32,32,TextureFormat.RGBA32,false);
            for(var y=0;y<32;y++) for(var x=0;x<32;x++)
            {
                var alpha = 0f; var p = new Vector2(x,y);
                for(var e=0;e<edges.Length;e+=2)
                {
                    var a=points[edges[e]]; var b=points[edges[e+1]]; var ab=b-a;
                    var distance=(p-(a+ab*Mathf.Clamp01(Vector2.Dot(p-a,ab)/ab.sqrMagnitude))).magnitude;
                    alpha=Mathf.Max(alpha,Mathf.Clamp01(1.5f-distance));
                }
                texture.SetPixel(x,y,new Color(1,1,1,alpha));
            }
            System.IO.File.WriteAllBytes(path.Replace("Packages/com.dreamy.debugger",UnityEditor.PackageManager.PackageInfo.FindForAssetPath(TabPath).resolvedPath),texture.EncodeToPNG()); Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.isReadable=false; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
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
        private static void UpperCorner(RectTransform rect, bool right, float width, float height, float y = 4)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(right ? 1 : 0, 1);
            rect.sizeDelta = new Vector2(width, height);
            rect.anchoredPosition = new Vector2(right ? -8 : 8, -y);
        }
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
            label.supportRichText = false;
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
        private static void FixedWidth(Button button, float width)
        {
            var size = button.GetComponent<LayoutElement>(); size.minWidth = size.preferredWidth = width; size.flexibleWidth = 0;
        }
        private static InputField Input(Transform parent, string name, string placeholder, bool touchNumeric = false)
        {
            var rect = Node(parent, name, typeof(Image), touchNumeric ? typeof(TouchNumberField) : typeof(InputField));
            rect.GetComponent<Image>().color = Theme.Control;
            rect.GetComponent<Image>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconFolder + "/rounded-fill.png");
            rect.GetComponent<Image>().type = Image.Type.Sliced;
            var text = Label(rect, "Value text", "", 15, Theme.Ivory); Stretch(text.rectTransform); text.rectTransform.offsetMin = new Vector2(8, 0); text.rectTransform.offsetMax = new Vector2(-8, 0);
            var hint = Label(rect, "Placeholder", placeholder, 14, Theme.Muted); Stretch(hint.rectTransform); hint.rectTransform.offsetMin = new Vector2(8, 0);
            var field = rect.GetComponent<InputField>(); field.textComponent = text; field.placeholder = hint; field.targetGraphic = rect.GetComponent<Image>();
            field.lineType = InputField.LineType.SingleLine; return field;
        }
    }
}
