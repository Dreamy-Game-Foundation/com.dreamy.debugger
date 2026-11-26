using System;
using System.Linq;
using System.Threading.Tasks;
using Dreamy.Debugger.Foundation.Service;
using Dreamy.Debugger.Reporting;

namespace Dreamy.Debugger.Presentation.UI.Tabs
{
    using Services;
    using Other;
    using Dreamy.Debugger.Foundation;
    using Dreamy.Debugger.Foundation.UI;
    using UnityEngine;
    using UnityEngine.UI;

    public partial class BugReportTabController : ComponentBehaviour, IEnableTab
    {
        [RequiredField] public BugReportSheetController BugReportSheetPrefab;

        [RequiredField] public RectTransform Container; 

        public sealed class Controls
        {
            public Func<Task<PendingReport[]>> Read;
            public Func<string, Task<string>> Export;
            public Func<string, Task<bool>> Delete;
            public Func<string, Task> Send;
            public Func<string, Task<string>> OpenExport;
            public Func<bool> AutoEnabled, UploadEnabled;
            public Func<bool, bool> SetAuto;
            public Action LogTestException;
            public Func<string> SendingId;
            public Func<int> DeliveryRevision;
            public Func<string> AcceptedLabel;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            public Func<int, bool> SelectDirect;
            public Func<int, bool> HasDirectDestination;
            public Func<Task<string>> TestDirect;
            public Func<Task<bool>> TestDirectReport;
            public Func<string> DirectStatus;
            public Func<int> DirectProvider;
            public Func<string, Task> ConfirmDirectRetry;
#endif
        }

        public static Func<Controls> GetControls { get; set; }
        private BugReportSheetController sheet;
        private Button attach, remove, auto, testException, previous, next, export, delete, send;
        private Text status, selection;
        private RawImage preview;
        private Transform column;
        private LayoutElement descriptionLayout;
        private VerticalLayoutGroup formLayout;
        private Transform descriptionLabel, contextLabel;
        private RectTransform viewport;
        private Texture2D previewTexture;
        private PendingReport[] reports = Array.Empty<PendingReport>();
        private int selected;
        private bool busy, reading, selectNewestAfterSave;
        private string confirmingDelete;
        private int generation;
        private int deliveryRevision;
        private string sendingId;
        private DateTime retryUnlockUtc;
        private float nextDeliveryStatusCheck;

        public bool IsEnabled
        {
            get { return ServiceManager.GetService<IBugReportService>().IsUsable; }
        }
        
        protected override void Start()
        {
            base.Start();

            var stack = new GameObject("Report content", typeof(RectTransform), typeof(VerticalLayoutGroup)).GetComponent<VerticalLayoutGroup>();
            stack.transform.SetParent(Container, false);
            stack.spacing = 4;
            stack.childControlWidth = stack.childControlHeight = true;
            stack.childForceExpandWidth = true;
            stack.childForceExpandHeight = false;
            column = stack.transform;
            sheet = ObjectFactory.Instantiate(BugReportSheetPrefab);
            sheet.UseDraftScreenshot = true;
            sheet.CachedTransform.SetParent(column, false);
            descriptionLayout = sheet.DescriptionField.GetComponent<LayoutElement>();
            viewport = GetComponent<ScrollRect>().viewport;
            StyleField();
            sheet.CancelButton.GetComponentInChildren<Text>().text = "Clear";
            sheet.CancelPressed = () => { sheet.DescriptionField.text = ""; SetPreview(null); };
            sheet.CanSend = () => GetControls?.Invoke()?.UploadEnabled?.Invoke() == true;
            sheet.SubmitComplete = (saved, error) =>
            {
                if (!saved) return;
                SetPreview(null);
                selectNewestAfterSave = true;
                _ = RefreshReports();
            };

            var form = sheet.ButtonContainer.transform.parent;
            formLayout = form.GetComponent<VerticalLayoutGroup>();
            formLayout.spacing = 8;
            contextLabel = form.Find("Text");
            descriptionLabel = sheet.DescriptionField.transform.parent.Find("Text");
            if (sheet.DescriptionField.placeholder is Text hint) hint.text = "Describe what happened";
            var top = Row(form, "Screenshot actions");
            top.SetSiblingIndex(sheet.ButtonContainer.transform.GetSiblingIndex());
            attach = ActionButton(top, "Attach screenshot", () => CaptureDraft());
            remove = ActionButton(top, "Remove", () => SetPreview(null));
            var previewSlot = new GameObject("Screenshot preview slot", typeof(RectTransform), typeof(LayoutElement));
            previewSlot.transform.SetParent(form, false);
            previewSlot.transform.SetSiblingIndex(sheet.ButtonContainer.transform.GetSiblingIndex() + 1);
            previewSlot.GetComponent<LayoutElement>().preferredHeight = 112;
            preview = new GameObject("Screenshot preview", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter)).GetComponent<RawImage>();
            preview.transform.SetParent(previewSlot.transform, false);
            preview.raycastTarget = false;
            preview.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            previewSlot.SetActive(false);
            PinSaveActions();
            ApplyCompactLayout();

            var toolbar = Row(column, "Reports");
            auto = ActionButton(toolbar, "Auto report", ToggleAuto);
            testException = ActionButton(toolbar, "Test exception", LogTestException);
            ActionButton(toolbar, "Refresh", () => _ = RefreshReports());
            selection = Label("Saved reports");
            var navigation = Row(column, "Report navigation");
            previous = ActionButton(navigation, "Previous", () => Select(-1));
            next = ActionButton(navigation, "Next", () => Select(1));
            status = Label("Report status");
            var actions = Row(column, "Saved report actions");
            export = ActionButton(actions, "Export", () => ManageReport(false));
            delete = ActionButton(actions, "Delete", () => ManageReport(true));
            send = ActionButton(actions, "Send", SendReport);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(GetControls?.Invoke()?.SelectDirect != null) BuildDirectUi();
#endif
            Render();
            _ = RefreshReports();
        }

        private void OnEnable()
        {
            nextDeliveryStatusCheck = 0;
            if (sheet != null) _ = RefreshReports();
        }

        private void LateUpdate()
        {
            if (sheet == null) return;
            if (Time.unscaledTime < nextDeliveryStatusCheck) return;
            nextDeliveryStatusCheck = Time.unscaledTime + 0.15f;
            var controls = GetControls?.Invoke();
            var active = controls?.SendingId?.Invoke();
            var revision = controls?.DeliveryRevision?.Invoke() ?? 0;
            if (sendingId != active)
            {
                sendingId = active;
                Render();
            }
            if (retryUnlockUtc != default && DateTime.UtcNow >= retryUnlockUtc) Render();
            if (!reading && deliveryRevision != revision)
            {
                deliveryRevision = revision;
                _ = RefreshReports();
            }
        }

        private void OnRectTransformDimensionsChange() => ApplyCompactLayout();

        private void ApplyCompactLayout()
        {
            if (descriptionLayout == null || viewport == null) return;
            var compact = viewport.rect.height < 120;
            var height = compact ? Mathf.Clamp(viewport.rect.height - 12, 38, 62) : viewport.rect.height < 300 ? 62 : 124;
            if (!Mathf.Approximately(descriptionLayout.preferredHeight, height))
                descriptionLayout.preferredHeight = height;
            if (contextLabel != null && contextLabel.gameObject.activeSelf == compact) contextLabel.gameObject.SetActive(!compact);
            if (descriptionLabel != null && descriptionLabel.gameObject.activeSelf == compact) descriptionLabel.gameObject.SetActive(!compact);
            if (formLayout != null && formLayout.padding.top != (compact ? 2 : 10))
                formLayout.padding = new RectOffset(10, 10, compact ? 2 : 10, compact ? 2 : 10);
        }

        private void PinSaveActions()
        {
            const int footerHeight = 48;
            var footer = new GameObject("Report footer", typeof(RectTransform)).GetComponent<RectTransform>();
            footer.SetParent(viewport.parent, false);
            footer.anchorMin = Vector2.zero;
            footer.anchorMax = Vector2.right;
            footer.offsetMin = new Vector2(12, 4);
            footer.offsetMax = new Vector2(-12, footerHeight - 4);
            var inset = viewport.offsetMin;
            viewport.offsetMin = new Vector2(inset.x, inset.y + footerHeight);
            if (viewport.GetComponent<RectMask2D>() == null) viewport.gameObject.AddComponent<RectMask2D>();
            Fit((RectTransform)sheet.ButtonContainer.transform);
            Fit((RectTransform)sheet.ProgressBar.transform);

            void Fit(RectTransform widget)
            {
                widget.SetParent(footer, false);
                widget.anchorMin = Vector2.zero;
                widget.anchorMax = Vector2.one;
                widget.offsetMin = widget.offsetMax = Vector2.zero;
            }
        }

        private void StyleField()
        {
            var input = sheet.DescriptionField;
            ApplyControlColors(input, DebuggerPalette.Control, DebuggerPalette.Border);
            input.textComponent.color = DebuggerPalette.Ivory;
            input.textComponent.supportRichText = false;
            if (input.placeholder is Text hint) hint.color = DebuggerPalette.Muted;
            input.customCaretColor = true;
            input.caretColor = DebuggerPalette.Ivory;
            ApplyControlColors(sheet.SubmitButton, DebuggerPalette.Accent, DebuggerPalette.Accent * 1.10f);
        }

        private static void ApplyControlColors(Selectable control, Color normal, Color highlighted)
        {
            var style = control.GetComponent<StyleComponent>();
            if (style != null) style.enabled = false;
            if (control.targetGraphic != null)
            {
                var graphicStyle = control.targetGraphic.GetComponent<StyleComponent>();
                if (graphicStyle != null) graphicStyle.enabled = false;
                control.targetGraphic.color = Color.white;
            }
            highlighted.a = 1;
            var colors = control.colors;
            colors.normalColor = normal;
            colors.highlightedColor = colors.selectedColor = highlighted;
            colors.pressedColor = Color.Lerp(normal, Color.black, .15f);
            colors.disabledColor = DebuggerPalette.Surface;
            control.colors = colors;
        }

        private void OnDisable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CloseDirectActions();
#endif
            generation++;
            confirmingDelete = null;
            if (sheet != null) SetPreview(null);
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if(directPanelService!=null)directPanelService.VisibilityChanged-=OnDirectPanelVisibilityChanged;
#endif
            if (previewTexture != null) Destroy(previewTexture);
        }

        private static RectTransform Row(Transform parent, string title)
        {
            var row = new GameObject(title, typeof(RectTransform), typeof(HorizontalLayoutGroup)).GetComponent<HorizontalLayoutGroup>();
            row.transform.SetParent(parent, false);
            row.spacing = 8;
            row.padding = new RectOffset(8, 8, 4, 4);
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = true;
            row.childForceExpandHeight = false;
            return (RectTransform)row.transform;
        }

        private Button ActionButton(Transform row, string title, Action action)
        {
            var button = Instantiate(sheet.CancelButton, row);
            button.name = title;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => action());
            button.GetComponentInChildren<Text>().text = title;
            var layout = button.GetComponent<LayoutElement>() ?? button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0;
            layout.minHeight = layout.preferredHeight = 42;
            return button;
        }

        private Text Label(string title)
        {
            var label = Instantiate(sheet.ResultMessageText, column);
            label.name = title;
            label.text = "";
            label.supportRichText = false;
            label.raycastTarget = false;
            label.gameObject.SetActive(true);
            return label;
        }

        private void ToggleAuto()
        {
            var controls = GetControls?.Invoke();
            if (controls?.SetAuto == null) return;
            controls.SetAuto(!controls.AutoEnabled());
            Render();
        }

        private void LogTestException()
        {
            var controls = GetControls?.Invoke();
            if (controls?.LogTestException == null) return;
            controls.LogTestException();
            status.text = controls.AutoEnabled() ? "Test exception logged. Check Console or refresh saved reports."
                : "Test exception logged. Turn on Auto report before testing delivery.";
        }

        private async void CaptureDraft()
        {
            if (busy || ScreenshotBridge.Capture == null) return;
            var request = generation;
            busy = true;
            status.text = "";
            Render();
            try
            {
                var bytes = await ScreenshotBridge.Capture();
                if (this == null || generation != request || !isActiveAndEnabled) return;
                SetPreview(bytes);
                status.text = bytes == null ? "Screenshot unavailable. You can still save the report." : "";
            }
            catch (Exception) { if (this != null && generation == request) status.text = "Could not attach screenshot."; }
            finally { if (this != null) { busy = false; Render(); } }
        }

        private void SetPreview(byte[] bytes)
        {
            if (sheet == null || preview == null) return;
            if (previewTexture != null) Destroy(previewTexture);
            previewTexture = null;
            if (bytes != null)
            {
                var texture = new Texture2D(2, 2);
                if (ImageConversion.LoadImage(texture, bytes, true)) previewTexture = texture;
                else Destroy(texture);
            }
            sheet.DraftScreenshot = previewTexture != null ? bytes : null;
            preview.texture = previewTexture;
            if (previewTexture != null)
                preview.GetComponent<AspectRatioFitter>().aspectRatio = (float)previewTexture.width / previewTexture.height;
            preview.transform.parent.gameObject.SetActive(previewTexture != null);
            Render();
        }

        private void Select(int step)
        {
            selected = Mathf.Clamp(selected + step, 0, Mathf.Max(0, reports.Length - 1));
            confirmingDelete = null;
            Render();
        }

        private async Task RefreshReports()
        {
            var controls = GetControls?.Invoke();
            if (controls == null || reading) return;
            reading = true;
            try
            {
                do
                {
                    var selectNewest = selectNewestAfterSave;
                    selectNewestAfterSave = false;
                    var entries = await controls.Read();
                    if (this == null) return;
                    var previousId = selectNewest || reports.Length == 0 ? null : reports[selected].Bundle.Report.Id;
                    reports = entries.Reverse().ToArray();
                    selected = Mathf.Max(0, Array.FindIndex(reports, item => item.Bundle.Report.Id == previousId));
                } while (selectNewestAfterSave);
            }
            catch (Exception) { if (this != null) status.text = "Could not load saved reports."; }
            finally { if (this != null) { reading = false; Render(); } }
        }

        private async void ManageReport(bool erase)
        {
            var controls = GetControls?.Invoke();
            if (controls == null || busy || reports.Length == 0) return;
            var id = reports[selected].Bundle.Report.Id;
            if (erase && confirmingDelete != id)
            {
                confirmingDelete = id;
                status.text = "Delete this local report? Exported and delivered copies are kept.";
                Render();
                return;
            }
            confirmingDelete = null;
            busy = true;
            Render();
            try
            {
                if (erase)
                {
                    await controls.Delete(id);
                    if (this != null) status.text = "Local report removed.";
                    await RefreshReports();
                }
                else
                {
                    var path = await controls.Export(id);
                    if (this == null || !isActiveAndEnabled) return;
                    var message = path == null ? "Report expired or is unavailable." : await controls.OpenExport(path);
                    if (this != null) status.text = message;
                }
            }
            catch (Exception) { if (this != null) status.text = erase ? "Could not delete report." : "Could not export report."; }
            finally { if (this != null) { busy = false; Render(); } }
        }

        private async void SendReport()
        {
            var controls = GetControls?.Invoke();
            if (controls == null || busy || reports.Length == 0) return;
            busy = true;
            status.text = "";
            Render();
            try
            {
                await controls.Send(reports[selected].Bundle.Report.Id);
                await RefreshReports();
            }
            catch (Exception) { if (this != null) status.text = "Could not send. The report remains on this device."; }
            finally { if (this != null) { busy = false; Render(); } }
        }

        private void Render()
        {
            if (auto == null) return;
            var controls = GetControls?.Invoke();
            auto.interactable = controls?.SetAuto != null;
            testException.gameObject.SetActive(controls?.LogTestException != null);
            auto.GetComponentInChildren<Text>().text = "Auto report: " + (controls?.AutoEnabled() == true ? "On" : "Off");
            attach.transform.parent.gameObject.SetActive(Settings.Instance.EnableBugReportScreenshot);
            attach.interactable = !busy;
            attach.GetComponentInChildren<Text>().text = previewTexture != null ? "Replace screenshot" : "Attach screenshot";
            remove.gameObject.SetActive(previewTexture != null);
            remove.interactable = !busy;
            previous.interactable = !busy && selected > 0;
            next.interactable = !busy && selected + 1 < reports.Length;
            export.interactable = delete.interactable = !busy && reports.Length > 0;
            var item = reports.Length > 0 ? reports[selected] : null;
            var sending = item != null && item.Bundle.Report.Id == sendingId;
            var pending = item?.State == LocalReportState.Pending;
            var failed = item?.State == LocalReportState.Failed;
            send.gameObject.SetActive(controls?.UploadEnabled() == true && (pending || failed));
            send.GetComponentInChildren<Text>().text = failed || item?.Attempts > 0 ? "Retry" : "Send";
            send.interactable = !busy && !sending && (failed || pending && item.NextAttemptUtc <= DateTime.UtcNow);
            retryUnlockUtc = send.gameObject.activeSelf && pending && item.NextAttemptUtc > DateTime.UtcNow
                ? item.NextAttemptUtc : default;
            if (sending) delete.interactable = false;
            delete.GetComponentInChildren<Text>().text = confirmingDelete == null ? "Delete" : "Confirm delete";
            selection.text = "No saved reports";
            if (item == null) return;
            var label = sending ? "Sending" :
                item.State == LocalReportState.RelayAccepted ? controls?.AcceptedLabel?.Invoke() ?? "Sent" :
                item.State == LocalReportState.Failed ? "Failed" :
                controls?.UploadEnabled() == true ? "Pending" : "Saved locally";
            if (!sending && pending && item.Attempts > 0 && controls?.UploadEnabled() == true && item.NextAttemptUtc > DateTime.UtcNow)
                label += " • Retry " + item.NextAttemptUtc.ToLocalTime().ToString("g");
            var message = item.Bundle.Report.Message.Replace('\n', ' ').Replace('\r', ' ');
            selection.text = (selected + 1) + " / " + reports.Length + " • " + label + "\n" +
                (message.Length > 100 ? message.Substring(0, 100) + "…" : message);
        }
    }
}
