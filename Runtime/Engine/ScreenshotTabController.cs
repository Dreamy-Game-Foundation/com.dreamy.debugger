using System;
using System.Collections.Generic;
using System.Linq;
using Dreamy.Debugger.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamy.Debugger
{
    public sealed class ScreenshotTabController : MonoBehaviour
    {
        public Button RefreshStorage, DeleteStorage, CancelDelete, HiddenShutter;
        public Text HiddenShutterLabel;
        public Text StorageSummary, DeleteStorageLabel;
        private bool confirmingDelete;
        private double confirmUntil;
        public Toggle[] Presets, HidePresets;
        public Button[] Delays, RestoreTimers;
        public Text[] DelayLabels, RestoreTimerLabels;
        public Button Capture, Hide, Restore, View, Repeat, OpenFolder, SaveGallery, Share;
        public Text CaptureLabel, HideLabel, Help, StatusLabel, ResultSummary, TargetHelp, PlatformHint;
        public GameObject Intro, DelayHeading;
        public RectTransform PresetArea;
        public GameObject TargetSection, ResultSection;
        public RectTransform TargetList;
        public Toggle TargetTemplate;
        public RawImage Preview;
        private readonly Dictionary<ScreenshotHideTarget, Toggle> targetViews = new Dictionary<ScreenshotHideTarget, Toggle>();
        private readonly int[] delays = { 0, 3, 5 }, timers = { 0, 15, 30, 60 };
        private ScreenshotResult shown;
        private double nextTargets;
        private bool refreshing;
        private ScreenshotMode Mode => DebuggerRuntime.Instance != null ? DebuggerRuntime.Instance.Screenshots : null;

        private void Start()
        {
            for (var i = 0; i < Presets.Length; i++)
            {
                var index = i;
                Presets[i].onValueChanged.AddListener(value => { if (value && !refreshing && Mode != null) { Mode.GameplayUi = (ScreenshotGameplayUi)index; Refresh(); } });
            }
            for (var i = 0; i < HidePresets.Length; i++)
            {
                var index = i;
                HidePresets[i].onValueChanged.AddListener(value => { if (value && !refreshing && Mode != null) { Mode.HideGameplayUi = (ScreenshotHideUi)index; Refresh(); } });
            }
            for (var i = 0; i < Delays.Length; i++)
            {
                var index = i; Delays[i].onClick.AddListener(() => { if (Mode != null) Mode.DelaySeconds = delays[index]; Refresh(); });
            }
            for (var i = 0; i < RestoreTimers.Length; i++)
            {
                var index = i;
                RestoreTimers[i].onClick.AddListener(() => { if (Mode != null) Mode.AutoRestoreSeconds = timers[index]; Refresh(); });
            }
            HiddenShutter.onClick.AddListener(() => { if (Mode != null) Mode.HiddenShutterEnabled = !Mode.HiddenShutterEnabled; Refresh(); });
            Capture.onClick.AddListener(() => Mode?.CaptureFromUi());
            Repeat.onClick.AddListener(() => Mode?.CaptureFromUi());
            Hide.onClick.AddListener(() => Mode?.HideUi());
            Restore.onClick.AddListener(() => Mode?.RestoreUi());
            View.onClick.AddListener(() => Mode?.ShowPreview());
            OpenFolder.onClick.AddListener(() => Export(() => { ScreenshotExport.OpenFolder(Mode.OutputDirectory); return "Screenshot folder opened"; }));
            SaveGallery.onClick.AddListener(() => Export(() => { ScreenshotExport.SaveToGallery(Mode.LastCapture.Path); return "Saved to Pictures / DreamyDebugger"; }));
            Share.onClick.AddListener(() => Export(() => { ScreenshotExport.Share(Mode.LastCapture.Path); return "Share sheet opened"; }));
            RefreshStorage.onClick.AddListener(() => { confirmingDelete = false; if (Mode != null) _ = Mode.RefreshStorageAsync(); });
            DeleteStorage.onClick.AddListener(DeleteScreenshots);
            CancelDelete.onClick.AddListener(() => { confirmingDelete = false; Refresh(); });
            if (Mode != null) _ = Mode.RefreshStorageAsync();
            RefreshTargets(); Refresh();
        }

        private void OnEnable()
        {
            nextTargets = 0; shown = null; confirmingDelete = false;
            if (Mode != null) _ = Mode.RefreshStorageAsync();
        }
        private void OnDisable() { confirmingDelete = false; }
        private void DeleteScreenshots()
        {
            if (Mode == null || !Mode.CanUse || Mode.IsBusy || Mode.IsStorageBusy || Mode.StoredScreenshotCount == 0) return;
            if (!confirmingDelete || Time.realtimeSinceStartupAsDouble > confirmUntil)
            { confirmingDelete = true; confirmUntil = Time.realtimeSinceStartupAsDouble + 10; }
            else { confirmingDelete = false; _ = Mode.DeleteStoredScreenshotsAsync(); }
            Refresh();
        }
        private void Update()
        {
            if (Time.realtimeSinceStartupAsDouble >= nextTargets) { nextTargets = Time.realtimeSinceStartupAsDouble + 1; RefreshTargets(); }
            Refresh();
        }

        private void RefreshTargets()
        {
            if (TargetTemplate == null) return;
            var targets = UnityEngine.Object.FindObjectsByType<ScreenshotHideTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(target => target.enabled).ToArray();
            foreach (var stale in targetViews.Keys.Where(target => target == null || !targets.Contains(target)).ToArray())
            {
                Destroy(targetViews[stale].gameObject); targetViews.Remove(stale);
            }
            foreach (var target in targets.OrderBy(target => target.DisplayName))
            {
                if (!targetViews.TryGetValue(target, out var toggle))
                {
                    toggle = Instantiate(TargetTemplate, TargetList);
                    targetViews.Add(target, toggle);
                    var captured = target;
                    toggle.onValueChanged.AddListener(value => { if (captured != null) captured.Selected = value; });
                    toggle.gameObject.SetActive(true);
                }
                toggle.GetComponentInChildren<Text>().text = target.DisplayName;
                toggle.SetIsOnWithoutNotify(target.Selected);
            }
        }

        public void Refresh()
        {
            var mode = Mode;
            if (mode == null || Capture == null) return;
            RefreshResponsiveLayout();
            refreshing = true;
            for (var i = 0; i < Presets.Length; i++) Presets[i].SetIsOnWithoutNotify((int)mode.GameplayUi == i);
            for (var i = 0; i < HidePresets.Length; i++) HidePresets[i].SetIsOnWithoutNotify((int)mode.HideGameplayUi == i);
            refreshing = false;
            var selected = mode.GameplayUi == ScreenshotGameplayUi.RegisteredTargets;
            var selectedHide = mode.HideGameplayUi == ScreenshotHideUi.RegisteredTargets;
            TargetSection.SetActive(selected || selectedHide);
            var count = targetViews.Keys.Count(target => target != null && target.Selected);
            TargetHelp.text = targetViews.Count == 0 ? "No UI groups registered. Choose Hide all Canvas UI, or register groups in your project." : count + " UI group(s) selected";
            var available = mode.CanUse && !mode.IsBusy && !mode.IsDeletingScreenshots && !mode.IsPreparingToHide;
            Capture.interactable = available && (!selected || count > 0);
            Hide.interactable = available && !mode.IsUiHidden && (!selectedHide || count > 0);
            Restore.interactable = mode.IsUiHidden || mode.IsBusy || mode.IsPreparingToHide;
            CaptureLabel.text = mode.DelaySeconds == 0 ? "Capture now" : "Capture in " + mode.DelaySeconds + " seconds";
            HideLabel.text = (selectedHide ? "Hide selected UI • " : "Hide all UI • ") + (mode.AutoRestoreSeconds <= 0 ? "Until restored" : mode.AutoRestoreSeconds + "s");
            HiddenShutterLabel.text = "Screenshot mode: " + (mode.HiddenShutterEnabled ? "ON" : "OFF");
            Help.text = "Capture uses its own UI preset. Hide UI only hides all / selected groups; hidden controls stay clickable. " + (mode.HiddenShutterEnabled ? "Mode ON: tap top-right to capture immediately, no Hide UI needed. " : "Enable mode for corner capture. ") + mode.RestoreInstructions;
            StatusLabel.text = mode.Status;
            for (var i = 0; i < Delays.Length; i++)
            {
                DelayLabels[i].text = (mode.DelaySeconds == delays[i] ? "• " : "") + (delays[i] == 0 ? "Now" : delays[i] + "s");
            }
            for (var i = 0; i < RestoreTimerLabels.Length; i++)
            {
                RestoreTimerLabels[i].text = (mode.AutoRestoreSeconds == timers[i] ? "• " : "") + (timers[i] == 0 ? "Until restored" : timers[i] + "s");
            }
            PlatformHint.text = Application.isEditor ? "Editor: capture here or use Window > Dreamy Debugger > Screenshot. External tools work while UI is hidden." :
                Application.platform == RuntimePlatform.Android ? "Android / emulator: capture a PNG, or hide UI and use the device / emulator screenshot button." :
                "Capture a PNG, or hide UI and use your device screenshot controls.";
            OpenFolder.gameObject.SetActive(ScreenshotExport.CanOpenFolder);
            var gallery = ScreenshotExport.CanSaveToGallery;
            SaveGallery.gameObject.SetActive(gallery); Share.gameObject.SetActive(gallery);
            if (confirmingDelete && (Time.realtimeSinceStartupAsDouble > confirmUntil || !available)) confirmingDelete = false;
            StorageSummary.text = mode.IsStorageBusy ? "Reading / cleaning screenshots…" :
                mode.StorageError ?? (mode.StoredScreenshotCount + " local screenshot(s) • " + FormatBytes(mode.StoredScreenshotBytes) + "\nGallery copies are managed separately.");
            RefreshStorage.interactable = available && !mode.IsStorageBusy;
            DeleteStorage.interactable = available && !mode.IsStorageBusy && mode.StoredScreenshotCount > 0;
            DeleteStorageLabel.text = confirmingDelete ? "Confirm delete all local screenshots" : "Delete local screenshots";
            CancelDelete.gameObject.SetActive(confirmingDelete);
            if (mode.LastCapture == null) { Preview.texture = null; shown = null; }
            ResultSection.SetActive(mode.LastCapture != null);
            if (mode.LastCapture != null)
            {
                View.interactable = mode.Preview != null;
                Repeat.interactable = available;
                if (!ReferenceEquals(shown, mode.LastCapture))
                {
                    shown = mode.LastCapture;
                    ResultSummary.text = "Last screenshot • PNG • " + shown.Width + " × " + shown.Height;
                    Preview.texture = mode.Preview;
                    var aspect = Preview.GetComponent<AspectRatioFitter>();
                    if (aspect != null && mode.Preview != null) aspect.aspectRatio = (float)mode.Preview.width / mode.Preview.height;
                }
            }
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024d).ToString("0.0") + " KB";
            return (bytes / (1024d * 1024)).ToString("0.0") + " MB";
        }

        private void Export(Func<string> action)
        {
            if (Mode == null || !Mode.CanUse) return;
            try { ResultSummary.text = action(); }
            catch (Exception error) { ResultSummary.text = "Could not export: " + error.Message; }
        }

        private void RefreshResponsiveLayout()
        {
            var size = ((RectTransform)transform).rect.size;
            var compact = size.x >= 480 && size.y < 400;
            if (Intro.activeSelf == compact) Intro.SetActive(!compact);
            if (DelayHeading.activeSelf == compact) DelayHeading.SetActive(!compact);
            var grid = PresetArea.GetComponent<GridLayoutGroup>();
            var parentLayout = PresetArea.parent.GetComponent<VerticalLayoutGroup>();
            var columns = compact ? 3 : 1;
            // Grid min-width depends on cell width. Read the constrained parent width to
            // avoid retaining the previous orientation's larger minimum width.
            var available = ((RectTransform)PresetArea.parent).rect.width - parentLayout.padding.horizontal;
            var cellWidth = Mathf.Max(1, (available - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns);
            var height = compact ? 48 : 56;
            if (grid.constraintCount != columns) grid.constraintCount = columns;
            if (Mathf.Abs(grid.cellSize.x - cellWidth) > .5f || grid.cellSize.y != height) grid.cellSize = new Vector2(cellWidth, height);
            var padding = compact ? 0 : 8;
            if (grid.padding.top != padding) grid.padding = new RectOffset(12, 12, padding, padding);
            if (parentLayout.spacing != (compact ? 8 : 10)) parentLayout.spacing = compact ? 8 : 10;
            var delayRow = Delays[0].transform.parent.GetComponent<LayoutElement>();
            if (delayRow.preferredHeight != height) delayRow.minHeight = delayRow.preferredHeight = height;
            foreach (var button in Delays)
            {
                var element = button.GetComponent<LayoutElement>();
                if (element.preferredHeight != height) element.minHeight = element.preferredHeight = height;
            }
        }
    }
}
