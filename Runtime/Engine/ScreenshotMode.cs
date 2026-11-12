using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Presentation;
using UnityEngine;

namespace Dreamy.Debugger
{
    public sealed class ScreenshotResult
    {
        public bool Success => !string.IsNullOrEmpty(Path);
        public string Path { get; internal set; }
        public string Reason { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
    }

    /// <summary>Local, full-resolution PNG export. Never uploads or enters the report outbox.</summary>
    [DisallowMultipleComponent]
    public sealed partial class ScreenshotMode : MonoBehaviour
    {
        private sealed class Operation
        {
            internal Task<byte[]> Frame;
            internal CancellationTokenSource Cancellation;
            internal IDisposable Visibility;
            internal double Deadline;
            internal int Width, Height;
        }

        public ScreenshotGameplayUi GameplayUi { get; set; }
        public int DelaySeconds { get; set; }
        public KeyCode RestoreKey { get; set; } = KeyCode.F8;
        public float RestoreTouchHoldSeconds { get; set; } = 1;
        public bool IsUiHidden => manualVisibility != null;
        public bool IsBusy => active != null;
        public string Status { get; private set; } = "Ready";
        public string OutputDirectory => System.IO.Path.Combine(Application.persistentDataPath, "DreamyDebugger", "Screenshots");
        public string RestoreInstructions => "Hold the top-left corner for 1.5s to restore UI. " + RestoreKey + " also restores / cancels.";

        private DebuggerRuntime owner;
        private ScreenshotUiVisibility visibility;
        private IDisposable manualVisibility;
        private Operation active;
        private Task<ScreenshotResult> captureTask;
        private Coroutine hideRoutine;
        private double touchStarted = -1;
        private string overlay;
        private double overlayUntil;

        internal void Configure(DebuggerRuntime runtime, Func<System.Collections.Generic.IEnumerable<GameObject>> roots)
        {
            owner = runtime;
            visibility = new ScreenshotUiVisibility(roots, () => HoldFeedbackRoot);
            ConfigureWorkflow();
        }

        internal IDisposable HideDebuggerForReport() => visibility.Acquire();

        public void HideUi()
        {
            if (!CanStart() || IsUiHidden || hideRoutine != null) return;
            HideWorkflowResult();
            ClosePanel();
            hideRoutine = StartCoroutine(HideNextFrame());
        }

        private IEnumerator HideNextFrame()
        {
            // Always yield once so StartCoroutine cannot finish before hideRoutine is assigned.
            yield return null;
            // Capture delay belongs to PNG capture. External capture starts immediately.
            overlay = null;
            cornerStarted = touchStarted = -1;
            manualVisibility = visibility.Acquire(GameplayUi, true);
            hiddenUntil = Time.realtimeSinceStartupAsDouble + Mathf.Clamp(AutoRestoreSeconds, 1, 60);
            Status = "UI hidden. " + RestoreInstructions;
            hideRoutine = null;
        }

        public void RestoreUi()
        {
            if (hideRoutine != null) { StopCoroutine(hideRoutine); hideRoutine = null; }
            AbortFrame();
            manualVisibility?.Dispose();
            manualVisibility = null;
            touchStarted = -1;
            cornerStarted = -1;
            HideRestoreHoldIndicator();
            hiddenUntil = 0;
            HideWorkflowResult();
            overlay = null;
            Status = "UI restored";
        }

        public Task<ScreenshotResult> CapturePngAsync()
        {
            if (active != null) return captureTask;
            if (!CanStart() || hideRoutine != null || IsUiHidden)
                return Reject("Screenshot mode unavailable, locked or UI already hidden");
            if (!Application.isEditor && !Application.isFocused || Screen.width <= 0 || Screen.height <= 0)
                return Reject("App not rendering");
            HideWorkflowResult();
            ClosePanel();
            var operation = new Operation { Cancellation = new CancellationTokenSource(), Deadline = Time.realtimeSinceStartupAsDouble + Mathf.Clamp(DelaySeconds, 0, 30) + 10 };
            active = operation;
            captureTask = CaptureAndSaveAsync(operation);
            return captureTask;
        }

        private bool CanStart() => isActiveAndEnabled && owner != null && owner.CanCheat && visibility != null && active == null && !IsStorageBusy;
        private static void ClosePanel() => DreamyDebug.Instance?.HideDebugPanel();
        private Task<ScreenshotResult> Reject(string reason)
        {
            ShowMessage(reason);
            return Task.FromResult(new ScreenshotResult { Reason = reason });
        }

        private async Task<ScreenshotResult> CaptureAndSaveAsync(Operation operation)
        {
            try
            {
                operation.Frame = CaptureFrameAsync(operation);
                var bytes = await operation.Frame;
                Status = "Saving PNG";
                var token = operation.Cancellation.Token;
                token.ThrowIfCancellationRequested();
                var directory = OutputDirectory;
                var path = System.IO.Path.Combine(directory, "Screenshot-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".png");
                await Task.Run(() => WritePng(path, bytes, token), token);
                token.ThrowIfCancellationRequested();
                var result = new ScreenshotResult { Path = path, Reason = "Saved", Width = operation.Width, Height = operation.Height };
                CompleteWorkflow(result, bytes);
                ShowMessage("Screenshot saved • " + result.Width + " × " + result.Height);
                return result;
            }
            catch (OperationCanceledException) { Status = "Screenshot canceled"; return new ScreenshotResult { Reason = "Canceled" }; }
            catch (Exception error) { ShowMessage("Screenshot failed: " + error.Message); return new ScreenshotResult { Reason = error.Message }; }
            finally
            {
                operation.Visibility?.Dispose();
                operation.Cancellation.Dispose();
                if (ReferenceEquals(active, operation)) active = null;
                if (this != null && CanUse) await RefreshStorageAsync();
            }
        }

        private async Task<byte[]> CaptureFrameAsync(Operation operation)
        {
            var token = operation.Cancellation.Token;
            Status = "Preparing screenshot";
            await Awaitable.NextFrameAsync(token);
            var until = Time.realtimeSinceStartupAsDouble + Mathf.Clamp(DelaySeconds, 0, 30);
            while (Time.realtimeSinceStartupAsDouble < until)
            {
                Countdown(until);
                await Awaitable.NextFrameAsync(token);
            }
            overlay = null;
            operation.Visibility = visibility.Acquire(GameplayUi);
            Status = "Hiding UI";
            await Awaitable.NextFrameAsync(token);
            if (!Application.isEditor && !Application.isFocused) throw new InvalidOperationException("App not rendering");
            if ((long)Screen.width * Screen.height * 16 > 128L * 1024 * 1024)
                throw new InvalidOperationException("Screenshot exceeds 128 MiB working-memory budget");
            var directory = OutputDirectory;
            var pending = System.IO.Path.Combine(directory, ".capture-" + Guid.NewGuid().ToString("N") + ".png");
            await Task.Run(() => PrepareDirectory(directory), token);
            token.ThrowIfCancellationRequested();
            try
            {
                visibility.Refresh();
                Status = "Waiting for rendered PNG";
                // Unity schedules this request on the render path, including overlay UI.
                // Do not read the framebuffer from Update or depend on Editor end-of-frame callbacks.
                ScreenCapture.CaptureScreenshot(pending);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var bytes = await Task.Run(() => ReadCompletePng(pending), token);
                    if (bytes != null)
                    {
                        token.ThrowIfCancellationRequested();
                        operation.Width = PngInt(bytes, 16);
                        operation.Height = PngInt(bytes, 20);
                        operation.Visibility.Dispose();
                        operation.Visibility = null;
                        return bytes;
                    }
                    await Task.Delay(25, token);
                }
            }
            finally
            {
                // A native capture cannot be canceled once submitted. A late file stays private
                // under .capture-* and is swept on the next capture, never published as an export.
                await Task.Run(() => { try { if (File.Exists(pending)) File.Delete(pending); } catch (IOException) { } catch (UnauthorizedAccessException) { } });
            }
        }

        private static void PrepareDirectory(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var file in Directory.GetFiles(directory, ".capture-*.png"))
                if (File.GetLastWriteTimeUtc(file) < DateTime.UtcNow.AddMinutes(-1))
                    try { File.Delete(file); } catch (IOException) { } catch (UnauthorizedAccessException) { }
        }

        private static int PngInt(byte[] bytes, int offset) =>
            bytes[offset] << 24 | bytes[offset + 1] << 16 | bytes[offset + 2] << 8 | bytes[offset + 3];

        private static byte[] ReadCompletePng(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 45) return null;
                    if (stream.Length > 64L * 1024 * 1024) throw new InvalidOperationException("PNG exceeds 64 MiB output budget");
                    var bytes = new byte[(int)stream.Length];
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        var read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0) return null;
                        offset += read;
                    }
                    // Header plus terminal IEND chunk: existence alone does not prove native IO finished.
                    var last = bytes.Length - 12;
                    if (bytes[0] != 137 || bytes[1] != 80 || bytes[2] != 78 || bytes[3] != 71 ||
                        PngInt(bytes, last) != 0 || bytes[last + 4] != 73 || bytes[last + 5] != 69 || bytes[last + 6] != 78 || bytes[last + 7] != 68)
                        return null;
                    return bytes;
                }
            }
            catch (IOException) { return null; }
        }

        private static void WritePng(string path, byte[] bytes, CancellationToken token)
        {
            var temporary = path + ".tmp";
            try
            {
                token.ThrowIfCancellationRequested();
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                File.WriteAllBytes(temporary, bytes);
                token.ThrowIfCancellationRequested();
                File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private void Update()
        {
            if (active == null && manualVisibility == null && hideRoutine == null) return;
            if (owner == null || !owner.CanCheat) { RestoreUi(); return; }
            if (IsUiHidden && Time.realtimeSinceStartupAsDouble >= hiddenUntil) { RestoreUi(); ShowMessage("UI restored"); return; }
            if (CheckRestoreCorner()) { RestoreUi(); ShowMessage("UI restored"); return; }
            if (InputBridge.Down(RestoreKey)) { RestoreUi(); ShowMessage("UI restored"); return; }
            if (InputBridge.ActiveTouchCount() == 3)
            {
                if (touchStarted < 0) touchStarted = Time.realtimeSinceStartupAsDouble;
                if (Time.realtimeSinceStartupAsDouble - touchStarted >= Mathf.Max(.25f, RestoreTouchHoldSeconds))
                { RestoreUi(); ShowMessage("UI restored"); return; }
            }
            else touchStarted = -1;
            if (active != null && active.Frame != null && !active.Frame.IsCompleted && Time.realtimeSinceStartupAsDouble > active.Deadline)
            {
                var stage = Status;
                AbortFrame();
                ShowMessage("Screenshot timed out: " + stage + ". Keep Game View visible while capturing.");
            }
        }

        private void LateUpdate() { UpdateRestoreHoldIndicator(); visibility?.Refresh(); }
        private void OnApplicationFocus(bool focused) { if (!focused && !PreserveOnFocusLoss) RestoreUi(); }
        private void OnApplicationPause(bool paused) { if (paused) RestoreUi(); }

        private void AbortFrame()
        {
            var operation = active;
            if (operation == null) return;
            operation.Cancellation.Cancel();
            operation.Visibility?.Dispose();
            operation.Visibility = null;
        }

        private void Countdown(double until)
        {
            overlay = "Screenshot in " + Mathf.CeilToInt((float)(until - Time.realtimeSinceStartupAsDouble)) + " • " + RestoreInstructions;
            overlayUntil = until;
            Status = overlay;
        }

        private void ShowMessage(string message)
        {
            if (this == null || !isActiveAndEnabled) return;
            Status = message;
            overlay = message;
            overlayUntil = Time.realtimeSinceStartupAsDouble + 6;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(overlay) || Time.realtimeSinceStartupAsDouble > overlayUntil || visibility == null || visibility.IsSuppressed) return;
            // Non-interactive, safe-area status only. Cleared before the captured frame.
            var safe = Screen.safeArea;
            var width = Mathf.Min(620, safe.width - 24);
            var style = new GUIStyle(GUI.skin.box) { wordWrap = true, fontSize = 16, alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(safe.x + (safe.width - width) / 2, Screen.height - safe.yMax + 12, width, 76), overlay, style);
        }

        internal void Shutdown()
        {
            RestoreUi();
            visibility?.Dispose();
            visibility = null;
            ReleaseWorkflow();
            ReleaseRestoreHoldIndicator();
        }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
