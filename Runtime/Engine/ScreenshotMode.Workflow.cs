using System;
using Dreamy.Debugger.Presentation;
using UnityEngine;

namespace Dreamy.Debugger
{
    public sealed partial class ScreenshotMode
    {
        public int AutoRestoreSeconds { get; set; } = 15;
        public bool IsPreparingToHide => hideRoutine != null;
        public bool PreserveOnFocusLoss => IsUiHidden || IsPreparingToHide;
        public int RestoreSecondsRemaining => IsUiHidden ? Mathf.Max(0, Mathf.CeilToInt((float)(hiddenUntil - Time.realtimeSinceStartupAsDouble))) : 0;
        public ScreenshotResult LastCapture { get; private set; }
        public Texture2D Preview { get; private set; }
        public GameObject OverlayRoot => workflowOverlay != null ? workflowOverlay.gameObject : null;
        public bool CanUse => isActiveAndEnabled && owner != null && owner.CanCheat;
        public bool ShowResultAfterCapture { get; set; } = true;
        private ScreenshotOverlayView workflowOverlay;
        private double hiddenUntil, cornerStarted = -1;

        private void ConfigureWorkflow()
        {
            GameplayUi = ScreenshotGameplayUi.Keep;
            DelaySeconds = 3;
            AutoRestoreSeconds = 15;
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
            try
            {
                ReplacePreview(png);
                if (ShowResultAfterCapture) EnsureOverlay()?.ShowResult();
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
