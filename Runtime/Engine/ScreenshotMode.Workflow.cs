using System;
using Dreamy.Debugger.Presentation;
using UnityEngine;

namespace Dreamy.Debugger
{
    public sealed partial class ScreenshotMode
    {
        public int AutoRestoreSeconds { get; set; } = 0;
        public bool IsPreparingToHide => hideRoutine != null;
        public bool PreserveOnFocusLoss => IsUiHidden || IsPreparingToHide || HiddenShutterEnabled;
        public int RestoreSecondsRemaining => IsUiHidden && AutoRestoreSeconds > 0 ? Mathf.Max(0, Mathf.CeilToInt((float)(hiddenUntil - Time.realtimeSinceStartupAsDouble))) : 0;
        public ScreenshotResult LastCapture { get; private set; }
        public Texture2D Preview { get; private set; }
        public GameObject OverlayRoot => workflowOverlay != null ? workflowOverlay.gameObject : null;
        public bool CanUse => isActiveAndEnabled && owner != null && owner.CanCheat;
        public bool ShowResultAfterCapture { get; set; } = true;
        private ScreenshotOverlayView workflowOverlay;
        private double hiddenUntil, cornerStarted = -1;
        public bool HiddenShutterEnabled { get; set; }
        public ScreenshotHideUi HideGameplayUi { get; set; } = ScreenshotHideUi.AllCanvases;
        private double shutterAcknowledgeUntil;
        private void OnPointerTapped(Vector2 start, Vector2 end)
        {
            if (!CanUse || !HiddenShutterEnabled || IsBusy || IsDeletingScreenshots || IsPreparingToHide) return;
            var safe = Screen.safeArea;
            var scale = Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160 : 1, 1, 3);
            var corner = new Rect(safe.xMax - 64 * scale, safe.yMax - 64 * scale, 64 * scale, 64 * scale);
            if (!corner.Contains(start) || !corner.Contains(end) || Vector2.Distance(start, end) > 20 * scale) return;
            shutterStarted = -1;
            shutterPointerWasHeld = false;
            shutterAcknowledgeUntil = Time.realtimeSinceStartupAsDouble + .2;
            ShowShutterFeedback("Tap received", "Capturing…", .2, 0);
            _ = CapturePngAsync(true);
        }
        private double shutterStarted = -1;
        private Vector2 shutterOrigin;

        private void CheckHiddenShutter()
        {
            if (!HiddenShutterEnabled || IsBusy || IsDeletingScreenshots || IsPreparingToHide)
            { shutterStarted = -1; shutterPointerWasHeld = InputBridge.HeldPointerPosition().HasValue; return; }
            var pointer = InputBridge.HeldPointerPosition();
            var safe = Screen.safeArea;
            var scale = Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160 : 1, 1, 3);
            var size = 64 * scale;
            var corner = new Rect(safe.xMax - size, safe.yMax - size, size, size);
            if (pointer.HasValue)
            {
                if (shutterStarted < 0)
                {
                    // A pointer entering from gameplay while held cannot arm the shutter.
                    if (!shutterPointerWasHeld && corner.Contains(pointer.Value))
                    { shutterStarted = Time.realtimeSinceStartupAsDouble; shutterOrigin = pointer.Value; }
                }
                else if (!corner.Contains(pointer.Value) || Vector2.Distance(shutterOrigin, pointer.Value) > 20 * scale)
                    shutterStarted = -1;
                shutterPointerWasHeld = true;
            }
            else
            {
                var click = shutterStarted >= 0 && Time.realtimeSinceStartupAsDouble - shutterStarted < .5;
                shutterStarted = -1; shutterPointerWasHeld = false;
                if (click) OnPointerTapped(shutterOrigin, shutterOrigin);
            }
        }
        private bool shutterPointerWasHeld;

        private void ConfigureWorkflow()
        {
            GameplayUi = ScreenshotGameplayUi.Keep;
            DelaySeconds = 3;
            AutoRestoreSeconds = 0;
            HiddenShutterEnabled = false;
        }

        public void CaptureFromUi() { if (CanUse) _ = CapturePngAsync(); }
        public void ShowPreview()
        {
            if (!CanUse || Preview == null) return;
            EnsureOverlay()?.ShowPreview();
        }

        private ScreenshotOverlayView EnsureOverlay()
        {
            if (workflowOverlay != null) return workflowOverlay;
            var prefab = PresentationAssets.Load<GameObject>("DreamyDebugger/ScreenshotOverlay");
            if (prefab == null) return null;
            workflowOverlay = Instantiate(prefab, transform).GetComponent<ScreenshotOverlayView>();
            workflowOverlay.Configure(this);
            return workflowOverlay;
        }

        private void CompleteWorkflow(ScreenshotResult result, byte[] png)
        {
            LastCapture = result;
            if (IsUiHidden || HiddenShutterEnabled) ShowShutterFeedback("Saved", "PNG saved • UI stays hidden", 1.5, 1);
            try
            {
                ReplacePreview(png);
                if (ShowResultAfterCapture && !IsUiHidden && !HiddenShutterEnabled) EnsureOverlay()?.ShowResult();
            }
            catch (Exception)
            {
                // Never present a previous image as the newly saved screenshot.
                if (Preview != null) Destroy(Preview);
                Preview = null;
                HideWorkflowResult();
            }
        }

        private void ReplacePreview(byte[] png)
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            RenderTexture scaled = null;
            var previous = RenderTexture.active;
            Texture2D next = null;
            try
            {
                if (!source.LoadImage(png)) return;
                var scale = Mathf.Min(1, 512f / Mathf.Max(source.width, source.height));
                var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                scaled = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, scaled);
                RenderTexture.active = scaled;
                next = new Texture2D(width, height, TextureFormat.RGBA32, false);
                next.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                next.Apply(false, true);
                if (Preview != null) Destroy(Preview);
                Preview = next;
                next = null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (scaled != null) RenderTexture.ReleaseTemporary(scaled);
                if (next != null) Destroy(next);
                Destroy(source);
            }
        }

        private bool CheckRestoreCorner()
        {
            var pointer = InputBridge.HeldPointerPosition();
            var safe = Screen.safeArea;
            var size = 64 * Mathf.Clamp(Screen.dpi > 0 ? Screen.dpi / 160 : 1, 1, 3);
            var corner = new Rect(safe.xMin, safe.yMax - size, size, size);
            if (!pointer.HasValue || !corner.Contains(pointer.Value)) { cornerStarted = -1; return false; }
            if (cornerStarted < 0) cornerStarted = Time.realtimeSinceStartupAsDouble;
            return Time.realtimeSinceStartupAsDouble - cornerStarted >= 1.5;
        }

        private void HideWorkflowResult() { if (workflowOverlay != null) workflowOverlay.HideAll(); }
        private void ReleaseWorkflow()
        {
            if (workflowOverlay != null) Destroy(workflowOverlay.gameObject);
            workflowOverlay = null;
            if (Preview != null) Destroy(Preview);
            Preview = null;
            LastCapture = null;
        }
    }
}
