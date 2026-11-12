using System;
using System.Linq;
using Dreamy.Debugger.Foundation.UI;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI.Other;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Editor
{
    /// <summary>Scoped maintainer authoring. Consumers use the shipped prefabs and catalog.</summary>
    public static class ScreenshotTabAssets
    {
        public const string TabPath = "Packages/com.dreamy.debugger/Runtime/Prefabs/ScreenshotTab.prefab";
        public const string OverlayPath = "Packages/com.dreamy.debugger/Runtime/Prefabs/ScreenshotOverlay.prefab";
        public const string IconPath = "Packages/com.dreamy.debugger/Runtime/Branding/Material/camera.png";
        private static Font Font => AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dreamy.debugger/Runtime/Branding/Material/Barlow-Regular.ttf");
        private static Sprite Surface => AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.dreamy.debugger/Runtime/Branding/Material/rounded-fill.png");

        [MenuItem("Tools/Dreamy Debugger/Author Screenshot UI")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before authoring Screenshot UI.");
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true; importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 32;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            // Keep small UI glyphs crisp; no platform format is assumed before a device check.
            foreach (var platform in new[] { "Android", "iPhone" })
            {
                var settings = importer.GetPlatformTextureSettings(platform); settings.overridden = true;
                settings.maxTextureSize = 32; settings.format = TextureImporterFormat.RGBA32;
                importer.SetPlatformTextureSettings(settings);
            }
            importer.SaveAndReimport();
            var camera = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            foreach (var guid in AssetDatabase.FindAssets("t:StyleSheet", new[] { "Packages/com.dreamy.debugger/Runtime" }))
            {
                var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guid));
                if (sheet.GetStyle("Icon_Camera", false) == null) sheet.AddStyle("Icon_Camera");
                var style = sheet.GetStyle("Icon_Camera", false);
                var reference = sheet.GetStyle("Icon_Console");
                if (reference != null) style.CopyFrom(reference);
                style.Image = camera;
                EditorUtility.SetDirty(sheet); AssetDatabase.SaveAssetIfDirty(sheet);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath);
            var previous = PresentationAssets.Catalog;
            PresentationAssets.Catalog = catalog;
            try
            {
                Author(TabPath, BuildTab);
                Author(OverlayPath, BuildOverlay);
                Register(catalog);
                ScreenshotHoldFeedbackAssets.Install();
            }
            finally { PresentationAssets.Catalog = previous; }
        }

        public static void Register(DebuggerAssetCatalog catalog)
        {
            var entries = catalog.Entries.Where(entry => entry.Key != "DreamyDebugger/UI/Prefabs/Tabs/Screenshot" && entry.Key != "DreamyDebugger/ScreenshotOverlay" && entry.Key != "DreamyDebugger/Theme/camera").ToList();
            entries.Add(new CatalogEntry { Key = "DreamyDebugger/UI/Prefabs/Tabs/Screenshot", Asset = AssetDatabase.LoadAssetAtPath<GameObject>(TabPath) });
            entries.Add(new CatalogEntry { Key = "DreamyDebugger/ScreenshotOverlay", Asset = AssetDatabase.LoadAssetAtPath<GameObject>(OverlayPath) });
            entries.Add(new CatalogEntry { Key = "DreamyDebugger/Theme/camera", Asset = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath) });
            catalog.Entries = entries.ToArray(); EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
        }

        private static void Author(string path, Func<Scene, GameObject> build)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = build(scene);
                PresentationAssets.ApplySkin(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private static GameObject BuildTab(Scene scene)
        {
            var root = new GameObject("Screenshot", typeof(RectTransform), typeof(DebugTab), typeof(ScreenshotTabController));
            SceneManager.MoveGameObjectToScene(root, scene); Stretch((RectTransform)root.transform);
            var tab = root.GetComponent<DebugTab>(); tab.IconStyleKey = "Icon_Camera"; tab.SortIndex = 4;
            var serialized = new SerializedObject(tab);
            serialized.FindProperty("_title").stringValue = "Screenshot";
            serialized.FindProperty("_longTitle").stringValue = "Screenshot";
            serialized.FindProperty("_key").stringValue = DefaultTabs.Screenshot.ToString();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var view = root.GetComponent<ScreenshotTabController>();
            // The debugger's owning content container already provides safe-area bounds.
            var scrollRoot = Node(root.transform, "Screenshot scroll", typeof(Image), typeof(ScrollRect)); Stretch(scrollRoot);
            scrollRoot.GetComponent<Image>().color = Theme.Background;
            var viewport = Node(scrollRoot, "Viewport", typeof(RectMask2D)); Stretch(viewport, 8);
            var content = Vertical(viewport, "Content");
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = scrollRoot.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            var intro = Vertical(content, "Intro"); intro.GetComponent<VerticalLayoutGroup>().padding = new RectOffset();
            view.Intro = intro.gameObject;
            Label(intro, "Heading", "Capture gameplay", 23, 34);
            Label(intro, "Subtitle", "Debugger is always hidden in the image.", 15, 30, true);
            view.HiddenShutter = Button(content, "Screenshot mode", "Screenshot mode: OFF"); view.HiddenShutterLabel = view.HiddenShutter.GetComponentInChildren<Text>();
            Label(content, "UI heading", "UI IN CAPTURED PNG", 14, 26, true);
            var presets = Node(content, "UI presets", typeof(GridLayoutGroup)); view.PresetArea = presets;
            var grid = presets.GetComponent<GridLayoutGroup>(); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 1;
            grid.padding = new RectOffset(12, 12, 8, 8); grid.spacing = new Vector2(8, 8); grid.cellSize = new Vector2(280, 56);
            var group = presets.gameObject.AddComponent<ToggleGroup>();
            view.Presets = new[] {
                Toggle(presets, "Keep UI", "Keep gameplay UI", group),
                Toggle(presets, "Selected UI", "Hide selected UI groups", group),
                Toggle(presets, "All UI", "Hide all Canvas UI", group)
            };
            view.Presets[2].transform.SetSiblingIndex(1); view.Presets[0].SetIsOnWithoutNotify(true);
            var targets = Vertical(content, "Selected groups"); view.TargetSection = targets.gameObject;
            view.TargetHelp = Label(targets, "Target help", "No UI groups registered.", 15, 52, true);
            view.TargetList = Vertical(targets, "Target list");
            view.TargetTemplate = Toggle(view.TargetList, "Target template", "UI group"); view.TargetTemplate.gameObject.SetActive(false);
            view.DelayHeading = Label(content, "Delay heading", "CAPTURE DELAY", 14, 26, true).gameObject;
            var delayRow = Row(content, "Delay choices");
            view.Delays = new[] { Button(delayRow, "Delay now", "Now"), Button(delayRow, "Delay 3", "3s"), Button(delayRow, "Delay 5", "5s") };
            view.DelayLabels = view.Delays.Select(button => button.GetComponentInChildren<Text>()).ToArray();
            view.Capture = Button(content, "Capture PNG", "Capture in 3 seconds", true); view.CaptureLabel = view.Capture.GetComponentInChildren<Text>();
            Label(content, "External heading", "HIDE UI • DOES NOT TAKE A PHOTO", 14, 30, true);
            var hideRow = Row(content, "Hide policy"); var hideGroup = hideRow.gameObject.AddComponent<ToggleGroup>();
            view.HidePresets = new[] { Toggle(hideRow, "Hide all", "Hide all UI", hideGroup), Toggle(hideRow, "Hide selected", "Hide selected", hideGroup) };
            view.HidePresets[0].SetIsOnWithoutNotify(true);
            var timerRow = Row(content, "Auto restore choices");
            view.RestoreTimers = new[] { Button(timerRow, "Restore 15", "15s"), Button(timerRow, "Restore 30", "30s"), Button(timerRow, "Restore 60", "60s") };
            view.RestoreTimerLabels = view.RestoreTimers.Select(button => button.GetComponentInChildren<Text>()).ToArray();
            view.Hide = Button(content, "Hide UI", "Hide UI • 15s"); view.HideLabel = view.Hide.GetComponentInChildren<Text>();
            view.Help = Label(content, "Recovery instructions", "Hold the top-left corner for 1.5s to restore UI.", 15, 132, true);
            view.Restore = Button(content, "Restore UI", "Restore UI / cancel capture");
            view.PlatformHint = Label(content, "Platform help", "", 15, 68, true);
            view.StatusLabel = Label(content, "Status", "Ready", 15, 46, true);
            var result = Vertical(content, "Last screenshot"); view.ResultSection = result.gameObject;
            view.ResultSummary = Label(result, "Result summary", "Last screenshot", 18, 36);
            var preview = Node(result, "Preview area"); Height(preview, 190); view.Preview = ImagePreview(preview, "Preview");
            var actions = Row(result, "Review actions"); view.View = Button(actions, "View image", "View image"); view.Repeat = Button(actions, "Capture again", "Capture again");
            view.OpenFolder = Button(result, "Open folder", "Open screenshot folder");
            view.SaveGallery = Button(result, "Save gallery", "Save to gallery"); view.Share = Button(result, "Share", "Share image");
            result.gameObject.SetActive(false); targets.gameObject.SetActive(false);
            var storage = Vertical(content, "Screenshot storage");
            Label(storage, "Storage heading", "LOCAL SCREENSHOT STORAGE", 14, 30, true);
            view.StorageSummary = Label(storage, "Storage summary", "Reading screenshots…", 15, 64, true);
            view.RefreshStorage = Button(storage, "Refresh storage", "Refresh storage usage");
            view.DeleteStorage = Button(storage, "Delete screenshots", "Delete local screenshots");
            view.DeleteStorageLabel = view.DeleteStorage.GetComponentInChildren<Text>();
            view.CancelDelete = Button(storage, "Cancel delete", "Keep screenshots");
            view.CancelDelete.gameObject.SetActive(false);
            // Keep both entry actions reachable in portrait and short landscape viewports.
            var footer = Vertical(root.transform, "Actions footer"); Box(footer, Theme.Surface);
            footer.anchorMin = Vector2.zero; footer.anchorMax = new Vector2(1, 0); footer.pivot = new Vector2(.5f, 0);
            footer.offsetMin = new Vector2(8, 0); footer.offsetMax = new Vector2(-8, 112);
            var footerActions = Row(footer, "Capture actions");
            view.Capture.transform.SetParent(footerActions, false); view.Hide.transform.SetParent(footerActions, false);
            Label(footer, "Quick restore help", "Hold top-left for 1.5s to restore UI", 13, 24, true);
            scrollRoot.offsetMin = new Vector2(0, 116);
            return root;
        }

        private static GameObject BuildOverlay(Scene scene)
        {
            var root = new GameObject("Screenshot review", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(ScreenshotOverlayView));
            SceneManager.MoveGameObjectToScene(root, scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 32766;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(540, 960); scaler.matchWidthOrHeight = 0;
            var safe = Node(root.transform, "Safe area", typeof(SafeArea)); Stretch(safe);
            var view = root.GetComponent<ScreenshotOverlayView>();
            var card = Vertical(safe, "Result card"); card.gameObject.AddComponent<Image>().color = Theme.Surface;
            card.anchorMin = new Vector2(0, 0); card.anchorMax = new Vector2(1, 0); card.pivot = new Vector2(.5f, 0);
            card.offsetMin = new Vector2(12, 12); card.offsetMax = new Vector2(-12, 242);
            view.ResultCard = card.gameObject;
            view.Summary = Label(card, "Saved label", "Screenshot saved", 18, 30);
            var thumb = Node(card, "Thumbnail area"); Height(thumb, 96); view.Thumbnail = ImagePreview(thumb, "Thumbnail");
            var actions = Row(card, "Result actions");
            view.View = Button(actions, "View image", "View"); view.Repeat = Button(actions, "Capture again", "Again"); view.Close = Button(actions, "Close result", "Close");
            var full = Node(safe, "Full preview", typeof(Image)); Stretch(full); full.GetComponent<Image>().color = Theme.Background;
            view.FullPreview = full.gameObject;
            var fullArea = Node(full, "Image area"); Stretch(fullArea, 12); fullArea.offsetMax = new Vector2(-12, -82);
            view.FullImage = ImagePreview(fullArea, "Full image");
            view.ClosePreview = Button(full, "Close preview", "Back"); var closeRect = (RectTransform)view.ClosePreview.transform;
            closeRect.anchorMin = new Vector2(0, 1); closeRect.anchorMax = Vector2.one; closeRect.pivot = new Vector2(.5f, 1);
            closeRect.offsetMin = new Vector2(12, -72); closeRect.offsetMax = new Vector2(-12, -12);
            card.gameObject.SetActive(false); full.gameObject.SetActive(false);
            return root;
        }

        private static RectTransform Node(Transform parent, string name, params Type[] components)
        {
            var go = new GameObject(name, new[] { typeof(RectTransform) }.Concat(components).ToArray()); go.layer = 5;
            go.transform.SetParent(parent, false); return (RectTransform)go.transform;
        }
        private static void Stretch(RectTransform rect, float inset = 0) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.one * inset; rect.offsetMax = -Vector2.one * inset; }
        private static void Height(RectTransform rect, float height) { var size = rect.gameObject.GetComponent<LayoutElement>() ?? rect.gameObject.AddComponent<LayoutElement>(); size.preferredHeight = height; size.minHeight = height; size.flexibleWidth = 1; }
        private static RectTransform Vertical(Transform parent, string name)
        {
            var rect = Node(parent, name, typeof(VerticalLayoutGroup)); var layout = rect.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 8, 8); layout.spacing = 10; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return rect;
        }
        private static RectTransform Row(Transform parent, string name)
        {
            var rect = Node(parent, name, typeof(HorizontalLayoutGroup)); Height(rect, 56);
            var layout = rect.GetComponent<HorizontalLayoutGroup>(); layout.spacing = 8; layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false; return rect;
        }
        private static Text Label(Transform parent, string name, string text, int fontSize, float height, bool muted = false)
        {
            var rect = Node(parent, name, typeof(Text)); Height(rect, height); var label = rect.GetComponent<Text>();
            label.text = text; label.font = Font; label.fontSize = fontSize; label.color = muted ? Theme.Muted : Theme.Ivory;
            label.raycastTarget = false; label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap; return label;
        }
        private static Image Box(RectTransform rect, Color color)
        {
            var image = rect.gameObject.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.sprite = Surface; image.type = Image.Type.Sliced; image.color = color; return image;
        }
        private static Button Button(Transform parent, string name, string caption, bool primary = false)
        {
            var rect = Node(parent, name, typeof(Image), typeof(Button)); Height(rect, 56);
            var button = rect.GetComponent<Button>(); button.targetGraphic = Box(rect, primary ? Theme.Accent : Theme.Control);
            var label = Label(rect, "Label", caption, 17, 0); label.alignment = TextAnchor.MiddleCenter;
            Object.DestroyImmediate(label.GetComponent<LayoutElement>()); Stretch(label.rectTransform, 8);
            return button;
        }
        private static Toggle Toggle(Transform parent, string name, string caption, ToggleGroup group = null)
        {
            var rect = Node(parent, name, typeof(Image), typeof(Toggle)); Height(rect, 56);
            var toggle = rect.GetComponent<Toggle>(); toggle.targetGraphic = Box(rect, Theme.Control); toggle.group = group;
            var selected = Node(rect, "Selected", typeof(Image)); Stretch(selected); Box(selected, Theme.Selected); selected.GetComponent<Image>().raycastTarget = false;
            toggle.graphic = selected.GetComponent<Image>();
            var text = Label(rect, "Label", caption, 16, 0); Object.DestroyImmediate(text.GetComponent<LayoutElement>()); Stretch(text.rectTransform, 12);
            return toggle;
        }
        private static RawImage ImagePreview(Transform parent, string name)
        {
            var rect = Node(parent, name, typeof(RawImage), typeof(AspectRatioFitter)); Stretch(rect);
            var aspect = rect.GetComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1;
            var image = rect.GetComponent<RawImage>(); image.raycastTarget = false; return image;
        }
    }
}
