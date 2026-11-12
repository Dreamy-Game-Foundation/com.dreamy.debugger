#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Dreamy.Debugger.Tests
{
    public sealed class ScreenshotModeTests
    {
        private readonly List<GameObject> objects = new List<GameObject>();
        private readonly List<string> files = new List<string>();
        private readonly List<RenderTexture> renders = new List<RenderTexture>();
        private Func<DebuggerStartupAssets> previousProvider;
        private Func<KeyCode, bool> previousPressed;
        private Func<int> previousTouches;
        private Func<Vector2?> previousPointer;
        private DebuggerStartupAssets assets;
        private DebuggerConfiguration config;
        private DebuggerRuntime runtime;
        private ScreenshotMode mode;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousProvider = DebuggerBootstrap.AssetProvider;
            previousPressed = InputBridge.Pressed;
            previousTouches = InputBridge.TouchCount;
            previousPointer = InputBridge.PrimaryPointer;
            assets = ScriptableObject.CreateInstance<DebuggerStartupAssets>();
            config = ScriptableObject.CreateInstance<DebuggerConfiguration>();
            config.PersistRuntimePreferences = false;
            assets.Configuration = config;
            assets.Catalog = AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
            DebuggerBootstrap.AssetProvider = () => assets;
            if (DebuggerRuntime.Instance != null) Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null;
            yield return null;
            runtime = DebuggerBootstrap.Initialize();
            Assert.That(runtime, Is.Not.Null);
            Assert.That(runtime.Access.RequestPrompt(), Is.True);
            Assert.That(runtime.Access.SubmitPin("0000"), Is.EqualTo(UnlockResult.Unlocked));
            mode = runtime.Screenshots;
            mode.DelaySeconds = 0;
            InputBridge.Pressed = key => false;
            InputBridge.TouchCount = () => 0;
            InputBridge.PrimaryPointer = () => null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (runtime != null) Object.Destroy(runtime.gameObject);
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear();
            yield return null;
            foreach (var render in renders) { render.Release(); Object.Destroy(render); }
            renders.Clear();
            // File IO is owned and observed even if a test assertion aborts mid-capture.
            while (mode != null && mode.IsBusy) yield return null;
            foreach (var file in files) if (File.Exists(file)) File.Delete(file);
            files.Clear();
            InputBridge.Pressed = previousPressed;
            InputBridge.TouchCount = previousTouches;
            InputBridge.PrimaryPointer = previousPointer;
            DebuggerBootstrap.AssetProvider = previousProvider;
            Object.Destroy(assets);
            Object.Destroy(config);
            yield return null;
        }

        private GameObject Ui(string name, Transform parent = null, bool canvas = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            objects.Add(go);
            if (parent != null) go.transform.SetParent(parent, false);
            if (canvas)
            {
                var component = go.AddComponent<Canvas>();
                component.renderMode = RenderMode.ScreenSpaceOverlay;
                component.sortingOrder = 32000;
                go.AddComponent<GraphicRaycaster>();
            }
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }

        private IEnumerator Hide(ScreenshotGameplayUi selection)
        {
            mode.GameplayUi = selection;
            mode.HideGameplayUi = selection == ScreenshotGameplayUi.RegisteredTargets ? ScreenshotHideUi.RegisteredTargets : ScreenshotHideUi.AllCanvases;
            mode.HideUi();
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            Assert.That(mode.IsUiHidden, Is.True);
        }

        [UnityTest]
        public IEnumerator SelectedTargetsRestoreOriginalValuesWithoutDisablingGameplay()
        {
            var root = Ui("Selected HUD", canvas: true);
            root.AddComponent<ScreenshotHideTarget>();
            var group = root.GetComponent<CanvasGroup>();
            group.alpha = .65f; group.interactable = false;
            var child = Ui("Nested popup", root.transform, true);
            child.GetComponent<Canvas>().overrideSorting = true;
            var nested = child.GetComponent<CanvasGroup>();
            nested.ignoreParentGroups = true; nested.alpha = .4f;
            var untouched = Ui("Unselected controls", canvas: true).GetComponent<CanvasGroup>();
            runtime.SetHud(HudMode.Detailed);
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(nested.alpha, Is.Zero);
            Assert.That(nested.ignoreParentGroups, Is.True);
            Assert.That(untouched.alpha, Is.EqualTo(1));
            Assert.That(root.activeInHierarchy, Is.True);
            Assert.That(runtime.Hud.gameObject.activeSelf, Is.True);
            Assert.That(runtime.Hud.GetComponent<CanvasGroup>().alpha, Is.Zero);
            mode.RestoreUi();
            Assert.That(group.alpha, Is.EqualTo(.65f));
            Assert.That(group.interactable, Is.False);
            Assert.That(group.blocksRaycasts, Is.True);
            Assert.That(nested.alpha, Is.EqualTo(.4f));
            Assert.That(runtime.Hud.Mode, Is.EqualTo(HudMode.Detailed));
        }

        [UnityTest]
        public IEnumerator NewTargetsAndPopupsAreHiddenAndClosedObjectsStayClosed()
        {
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            var root = Ui("Late HUD", canvas: true);
            root.AddComponent<ScreenshotHideTarget>();
            var popup = Ui("Late popup", root.transform);
            popup.GetComponent<CanvasGroup>().ignoreParentGroups = true;
            Canvas.ForceUpdateCanvases();
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(popup.GetComponent<CanvasGroup>().alpha, Is.Zero);
            popup.SetActive(false);
            mode.RestoreUi();
            Assert.That(popup.activeSelf, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator AllCanvasesIncludesUnregisteredAndLateCanvases()
        {
            var first = Ui("Unregistered HUD", canvas: true);
            yield return Hide(ScreenshotGameplayUi.AllCanvases);
            var second = Ui("New canvas", canvas: true);
            Canvas.ForceUpdateCanvases();
            Assert.That(first.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(second.GetComponent<CanvasGroup>().alpha, Is.Zero);
            mode.RestoreUi();
            Assert.That(first.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            Assert.That(second.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HidePolicyIsIndependentOfKeepCaptureAndCancelRestoresUi()
        {
            var group = Ui("Game HUD", canvas: true).GetComponent<CanvasGroup>();
            yield return Hide(ScreenshotGameplayUi.Keep);
            Assert.That(mode.GameplayUi, Is.EqualTo(ScreenshotGameplayUi.Keep));
            Assert.That(group.alpha, Is.Zero, "Hide defaults to ALL even while Capture keeps UI");
            mode.RestoreUi();
            mode.DelaySeconds = 30;
            mode.GameplayUi = ScreenshotGameplayUi.AllCanvases;
            mode.HideUi(); yield return null;
            mode.RestoreUi(); yield return null;
            Assert.That(mode.IsUiHidden, Is.False);
            Assert.That(group.alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator KeyboardAndThreeFingerHoldRestoreWithoutVisibleControls()
        {
            yield return Hide(ScreenshotGameplayUi.Keep);
            InputBridge.Pressed = key => key == KeyCode.F8;
            yield return null;
            Assert.That(mode.IsUiHidden, Is.False);
            InputBridge.Pressed = key => false;
            yield return Hide(ScreenshotGameplayUi.Keep);
            mode.RestoreTouchHoldSeconds = .25f;
            InputBridge.TouchCount = () => 3;
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(mode.IsUiHidden, Is.False);
        }

        [UnityTest]
        public IEnumerator RelockAndShutdownRestoreGameplay()
        {
            var root = Ui("Game HUD", canvas: true);
            root.AddComponent<ScreenshotHideTarget>();
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            runtime.Relock();
            Assert.That(mode.IsUiHidden, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            runtime.Access.RequestPrompt(); runtime.Access.SubmitPin("0000");
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            runtime.gameObject.SetActive(false);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator NestedReportCaptureDoesNotUnhideManualMode()
        {
            runtime.SetHud(HudMode.Detailed);
            yield return Hide(ScreenshotGameplayUi.Keep);
            var acquire = typeof(ScreenshotMode).GetMethod("HideDebuggerForReport", BindingFlags.Instance | BindingFlags.NonPublic);
            var lease = (IDisposable)acquire.Invoke(mode, null);
            mode.RestoreUi();
            Assert.That(runtime.Hud.GetComponent<CanvasGroup>().alpha, Is.Zero);
            lease.Dispose();
            Assert.That(runtime.Hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator CancelPendingCaptureIsSingleFlightAndRestoresUi()
        {
            mode.DelaySeconds = 30;
            var first = mode.CapturePngAsync();
            Assert.That(mode.CapturePngAsync(), Is.SameAs(first));
            yield return null;
            mode.RestoreUi();
            while (!first.IsCompleted) yield return null;
            Assert.That(first.Result.Success, Is.False);
            Assert.That(first.Result.Reason, Is.EqualTo("Canceled"));
            Assert.That(mode.IsBusy, Is.False);
        }

        [UnityTest]
        public IEnumerator LockedSessionCannotHideOrExport()
        {
            runtime.Relock();
            mode.HideUi(); yield return null;
            Assert.That(mode.IsUiHidden, Is.False);
            var task = mode.CapturePngAsync();
            Assert.That(task.IsCompleted, Is.True);
            Assert.That(task.Result.Success, Is.False);
        }

        [UnityTest]
        public IEnumerator SceneChangeAndFocusLossRestoreUiAndCancelPendingCapture()
        {
            var root = Ui("Game HUD", canvas: true);
            root.AddComponent<ScreenshotHideTarget>();
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var next = UnityEngine.SceneManagement.SceneManager.CreateScene("Screenshot scene transition test");
            try
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(next);
                Assert.That(mode.IsUiHidden, Is.False);
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);
                UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(next);
            }
            runtime.Access.RequestPrompt(); runtime.Access.SubmitPin("0000");
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            runtime.SendMessage("OnApplicationFocus", false);
            mode.SendMessage("OnApplicationFocus", false);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero, "External screenshot survives focus changes");
            Assert.That(runtime.Access.IsUnlocked, Is.True);
            runtime.SendMessage("OnApplicationPause", true);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            runtime.Access.RequestPrompt(); runtime.Access.SubmitPin("0000");
            mode.DelaySeconds = 30;
            var task = mode.CapturePngAsync();
            yield return null;
            mode.SendMessage("OnApplicationPause", true);
            while (!task.IsCompleted) yield return null;
            Assert.That(task.Result.Success, Is.False);
            Assert.That(mode.IsBusy, Is.False);
        }

        [Test]
        public void MobileNativeCaptureFilenameIsRelativeToPersistentData()
        {
            var method = typeof(ScreenshotMode).GetMethod("NativeCaptureFilename", BindingFlags.Static | BindingFlags.NonPublic);
            var full = Path.Combine(Application.persistentDataPath, "DreamyDebugger", "Screenshots", ".capture-test.png");
            Assert.That(method.Invoke(null, new object[] { full, true }), Is.EqualTo("DreamyDebugger/Screenshots/.capture-test.png"));
            Assert.That(method.Invoke(null, new object[] { full, false }), Is.EqualTo(full));
        }

        [UnityTest]
        public IEnumerator HiddenGameplayRemainsRaycastableAndShutterCapturesWithoutRestoring()
        {
            var cameraObject = new GameObject("Hidden shutter camera", typeof(Camera)); objects.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.green;
            var hud = Ui("Clickable hidden HUD", canvas: true);
            var buttonObject = Ui("Gameplay button", hud.transform);
            buttonObject.AddComponent<Image>().color = Color.magenta;
            var button = buttonObject.AddComponent<Button>(); var clicks = 0; button.onClick.AddListener(() => clicks++);
            mode.DelaySeconds = 30;
            mode.HiddenShutterEnabled = true;
            yield return Hide(ScreenshotGameplayUi.AllCanvases);
            var group = hud.GetComponent<CanvasGroup>();
            Assert.That(group.interactable && group.blocksRaycasts, Is.True);
            var events = UnityEngine.EventSystems.EventSystem.current;
            Assert.That(events, Is.Not.Null);
            var pointer = new UnityEngine.EventSystems.PointerEventData(events) { position = new Vector2(Screen.width / 2f, Screen.height / 2f) };
            var hits = new List<UnityEngine.EventSystems.RaycastResult>();
            hud.GetComponent<GraphicRaycaster>().Raycast(pointer, hits);
            Assert.That(hits.Any(hit => hit.gameObject == buttonObject), Is.True, "Transparent gameplay button must still raycast");
            UnityEngine.EventSystems.ExecuteEvents.Execute(buttonObject, pointer, UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
            Assert.That(clicks, Is.EqualTo(1));
            InputBridge.PrimaryPointer = () => new Vector2(Screen.safeArea.xMax - 4, Screen.safeArea.yMax - 4);
            yield return null;
            InputBridge.PrimaryPointer = () => null;
            yield return null;
            var until = Time.realtimeSinceStartupAsDouble + 8;
            var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            while ((mode.IsBusy || mode.LastCapture == null) && Time.realtimeSinceStartupAsDouble < until)
            { view.Repaint(); yield return null; }
            Assert.That(mode.LastCapture, Is.Not.Null, mode.Status);
            files.Add(mode.LastCapture.Path);
            Assert.That(mode.IsUiHidden, Is.True);
            Assert.That(group.alpha, Is.Zero);
            Assert.That(mode.OverlayRoot == null || !mode.OverlayRoot.activeInHierarchy || !mode.OverlayRoot.GetComponent<ScreenshotOverlayView>().ResultCard.activeSelf, Is.True);
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(mode.LastCapture.Path)), Is.True);
                var pixel = texture.GetPixel(texture.width / 2, texture.height / 2);
                Assert.That(pixel.g, Is.GreaterThan(.9f)); Assert.That(pixel.r, Is.LessThan(.1f));
            }
            finally { Object.Destroy(texture); }
            // Game-owned input state can change while alpha is hidden; restore must not overwrite it.
            group.interactable = false;
            mode.RestoreUi();
            Assert.That(group.alpha, Is.EqualTo(1)); Assert.That(group.interactable, Is.False);
        }

        [UnityTest]
        public IEnumerator ArmedModeCapturesWithoutHideUsingIndependentCapturePreset()
        {
            var cameraObject = new GameObject("Armed mode camera", typeof(Camera)); objects.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.green;
            var hud = Ui("Visible gameplay HUD", canvas: true);
            var marker = Ui("Visible marker", hud.transform); marker.AddComponent<Image>().color = Color.magenta;
            mode.DelaySeconds = 30;
            mode.HiddenShutterEnabled = true;
            mode.HideGameplayUi = ScreenshotHideUi.RegisteredTargets;
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            foreach (var preset in new[] { ScreenshotGameplayUi.Keep, ScreenshotGameplayUi.AllCanvases })
            {
                Assert.That(mode.IsUiHidden, Is.False);
                mode.GameplayUi = preset;
                var corner = new Vector2(Screen.safeArea.xMax - 4, Screen.safeArea.yMax - 4);
                if (preset == ScreenshotGameplayUi.AllCanvases)
                {
                    // Reading usage must not swallow the first tap after opening the tab.
                    _ = mode.RefreshStorageAsync();
                    Assert.That(mode.IsStorageBusy, Is.True);
                }
                // The event-backed adapter emits this even if both edges occur between frames.
                InputBridge.ReportPointerTap(corner, corner);
                Assert.That(mode.IsBusy, Is.True, "Armed tap must capture without Hide, including during a read-only storage scan");
                var until = Time.realtimeSinceStartupAsDouble + 8;
                while ((mode.IsBusy || mode.IsStorageBusy || mode.LastCapture == null) && Time.realtimeSinceStartupAsDouble < until)
                { gameView.Repaint(); yield return null; }
                Assert.That(mode.LastCapture, Is.Not.Null, mode.Status); Assert.That(mode.IsBusy, Is.False);
                files.Add(mode.LastCapture.Path);
                Assert.That(mode.IsUiHidden, Is.False);
                Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(mode.LastCapture.Path)), Is.True);
                    var pixel = texture.GetPixel(texture.width / 2, texture.height / 2);
                    Assert.That(pixel.r, Is.EqualTo(preset == ScreenshotGameplayUi.Keep ? 1 : 0).Within(.05f));
                    Assert.That(pixel.g, Is.EqualTo(preset == ScreenshotGameplayUi.Keep ? 0 : 1).Within(.05f));
                }
                finally { Object.Destroy(texture); }
                Assert.That(mode.OverlayRoot == null || !mode.OverlayRoot.GetComponent<ScreenshotOverlayView>().ResultCard.activeSelf, Is.True);
            }
            mode.HideGameplayUi = ScreenshotHideUi.AllCanvases;
            mode.GameplayUi = ScreenshotGameplayUi.Keep;
            mode.HideUi(); yield return null; yield return null;
            Assert.That(hud.GetComponent<CanvasGroup>().alpha, Is.Zero, "Manual Hide ignores Capture Keep preset");
            mode.RestoreUi();
        }

        [UnityTest]
        public IEnumerator SameFrameTapIsOptInAndFeedbackIsExcludedFromPng()
        {
            var cameraObject = new GameObject("Fast tap camera", typeof(Camera)); objects.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.green;
            Assert.That(mode.HiddenShutterEnabled, Is.False);
            yield return Hide(ScreenshotGameplayUi.AllCanvases);
            var point = new Vector2(Screen.safeArea.xMax - 4, Screen.safeArea.yMax - 4);
            InputBridge.ReportPointerTap(point, point);
            Assert.That(mode.IsBusy, Is.False, "Default OFF ignores the hidden shutter");
            mode.HiddenShutterEnabled = true;
            InputBridge.ReportPointerTap(Screen.safeArea.center, point);
            Assert.That(mode.IsBusy, Is.False, "Dragging into the corner is not a tap there");
            // No held pointer is ever visible to Update: both edges arrived in one input update.
            InputBridge.ReportPointerTap(point, point);
            Assert.That(mode.IsBusy, Is.True);
            Assert.That(mode.IsShutterFeedbackVisible, Is.True, "Tap acknowledgment is immediate");
            InputBridge.ReportPointerTap(point, point); // Duplicate while busy must not start another operation.
            var until = Time.realtimeSinceStartupAsDouble + 8;
            var gameView = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            while ((mode.IsBusy || mode.LastCapture == null) && Time.realtimeSinceStartupAsDouble < until)
            { gameView.Repaint(); yield return null; }
            Assert.That(mode.LastCapture, Is.Not.Null, mode.Status);
            files.Add(mode.LastCapture.Path);
            yield return null;
            Assert.That(mode.IsUiHidden, Is.True);
            Assert.That(mode.IsShutterFeedbackVisible, Is.True);
            var feedback = mode.HoldFeedbackRoot.GetComponent<RestoreHoldFeedbackView>();
            Assert.That(feedback.Title.text, Is.EqualTo("Saved"));
            Assert.That(feedback.Card.pivot.x, Is.EqualTo(1));
            var decoded = new Texture2D(2, 2);
            try
            {
                Assert.That(decoded.LoadImage(File.ReadAllBytes(mode.LastCapture.Path)), Is.True);
                var clean = decoded.GetPixel(Mathf.Max(0, decoded.width - 40), Mathf.Max(0, decoded.height - 35));
                Assert.That(clean.g, Is.GreaterThan(.9f)); Assert.That(clean.r, Is.LessThan(.1f), "Feedback must not appear inside captured PNG");
            }
            finally { Object.Destroy(decoded); }
            // Prove the saved feedback itself is visible in a subsequent native rendered frame.
            var directory = Environment.GetEnvironmentVariable("DREAMY_DEBUGGER_VISUAL_EVIDENCE");
            var proof = Path.Combine(string.IsNullOrEmpty(directory) ? Path.GetTempPath() : directory, "shutter-saved-" + Guid.NewGuid().ToString("N") + ".png");
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory); else files.Add(proof);
            ScreenCapture.CaptureScreenshot(proof);
            until = Time.realtimeSinceStartupAsDouble + 3;
            while (!File.Exists(proof) && Time.realtimeSinceStartupAsDouble < until) { gameView.Repaint(); yield return null; }
            yield return null; yield return null;
            var rendered = new Texture2D(2, 2);
            try
            {
                Assert.That(rendered.LoadImage(File.ReadAllBytes(proof)), Is.True);
                Assert.That(rendered.GetPixels32().Count(pixel => pixel.r > pixel.g + 30 && pixel.r > 100), Is.GreaterThan(8), "Saved ring must render to screen");
            }
            finally { Object.Destroy(rendered); }
            mode.RestoreUi();
            Assert.That(mode.IsShutterFeedbackVisible, Is.False);
        }

        [Test]
        public void StorageCountsAndDeletesOnlyOwnedTopLevelFiles()
        {
            var directory = Path.Combine(Path.GetTempPath(), "DreamyScreenshotStorage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                var owned = Path.Combine(directory, "Screenshot-20261112-123456-123-1234abcd.png");
                File.WriteAllBytes(owned, new byte[2048]);
                var foreign = Path.Combine(directory, "Screenshot-important.png");
                File.WriteAllBytes(foreign, new byte[100]);
                File.WriteAllBytes(Path.Combine(directory, ".capture-test.png"), new byte[50]);
                var nested = Directory.CreateDirectory(Path.Combine(directory, "nested"));
                var nestedFile = Path.Combine(nested.FullName, Path.GetFileName(owned));
                File.WriteAllBytes(nestedFile, new byte[200]);
                var process = typeof(ScreenshotMode).GetMethod("ProcessStorage", BindingFlags.Static | BindingFlags.NonPublic);
                var read = (long[])process.Invoke(null, new object[] { directory, false });
                Assert.That(read, Is.EqualTo(new long[] { 1, 2048, 0 }));
                var deleted = (long[])process.Invoke(null, new object[] { directory, true });
                Assert.That(deleted, Is.EqualTo(new long[] { 0, 0, 0 }));
                Assert.That(File.Exists(owned), Is.False);
                Assert.That(File.Exists(foreign) && File.Exists(nestedFile) && File.Exists(Path.Combine(directory, ".capture-test.png")), Is.True);
                Assert.That(process.Invoke(null, new object[] { directory, true }), Is.EqualTo(new long[] { 0, 0, 0 }));
            }
            finally { Directory.Delete(directory, true); }
        }

        [UnityTest]
        public IEnumerator DedicatedScreenshotTabHasCameraIconAndWorkingControls()
        {
            var root = Ui("Registered HUD", canvas: true);
            root.AddComponent<ScreenshotHideTarget>();
            runtime.Open(DefaultTabs.Screenshot);
            yield return null; yield return null;
            var tab = Object.FindFirstObjectByType<ScreenshotTabController>();
            Assert.That(tab, Is.Not.Null);
            var storageDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (mode.IsStorageBusy && Time.realtimeSinceStartupAsDouble < storageDeadline) yield return null;
            Assert.That(mode.IsStorageBusy, Is.False);
            tab.Refresh();
            Assert.That(tab.StorageSummary.text, Does.Contain("local screenshot(s)"));
            Assert.That(tab.HidePresets.Length, Is.EqualTo(2), "Hide has no Keep choice");
            Assert.That(tab.HiddenShutterLabel.text, Is.EqualTo("Screenshot mode: OFF"));
            tab.HiddenShutter.onClick.Invoke();
            Assert.That(mode.HiddenShutterEnabled, Is.True);
            Assert.That(tab.HiddenShutterLabel.text, Is.EqualTo("Screenshot mode: ON"));
            Assert.That(tab.DeleteStorage.interactable, Is.EqualTo(mode.StoredScreenshotCount > 0));
            if (mode.StoredScreenshotCount > 0)
            {
                var before = Directory.GetFiles(mode.OutputDirectory);
                tab.DeleteStorage.onClick.Invoke();
                Assert.That(tab.CancelDelete.gameObject.activeSelf, Is.True);
                Assert.That(tab.DeleteStorageLabel.text, Does.Contain("Confirm"));
                Assert.That(Directory.GetFiles(mode.OutputDirectory), Is.EquivalentTo(before));
                tab.CancelDelete.onClick.Invoke();
                Assert.That(tab.CancelDelete.gameObject.activeSelf, Is.False);
            }
            Assert.That(runtime.Cheats.Snapshot().Any(option => option.Id.StartsWith("screenshot.")), Is.False);
            var definition = tab.GetComponent<Dreamy.Debugger.Presentation.UI.Other.DebugTab>();
            Assert.That(definition.Key, Is.EqualTo("Screenshot"));
            Assert.That(definition.TabButton.IconStyleComponent.GetComponent<Image>().sprite.name, Is.EqualTo("camera"));
            tab.Presets[(int)ScreenshotGameplayUi.RegisteredTargets].isOn = true;
            Assert.That(mode.GameplayUi, Is.EqualTo(ScreenshotGameplayUi.RegisteredTargets));
            Assert.That(tab.Hide.interactable, Is.True);
            tab.Hide.onClick.Invoke();
            yield return null; yield return null;
            Assert.That(mode.IsUiHidden, Is.True);
            Assert.That(DreamyDebug.Instance.IsDebugPanelVisible, Is.False);
            Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.Zero);
            mode.RestoreUi();
        }

        [UnityTest]
        public IEnumerator ExternalCaptureStartsWithoutWaitingForPngDelay()
        {
            mode.DelaySeconds = 30;
            mode.HideUi();
            yield return null; yield return null;
            Assert.That(mode.IsUiHidden, Is.True);
            Assert.That(mode.RestoreSecondsRemaining, Is.InRange(14, 15));
            mode.RestoreUi();
        }

        [UnityTest]
        public IEnumerator HoldFeedbackCancelsResetsAndStaysOutOfDiagnosticCaptures()
        {
            yield return Hide(ScreenshotGameplayUi.Keep);
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.False);
            InputBridge.PrimaryPointer = () => new Vector2(Screen.safeArea.xMin + 4, Screen.safeArea.yMax - 4);
            yield return null; yield return null;
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.True);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(mode.HoldFeedbackRoot, Is.Not.Null);
            Assert.That(mode.HoldFeedbackRoot.activeInHierarchy, Is.True);
            Assert.That(mode.RestoreHoldProgress, Is.InRange(.1f, .8f));
            var acquire = typeof(ScreenshotMode).GetMethod("HideDebuggerForReport", BindingFlags.Instance | BindingFlags.NonPublic);
            var lease = (IDisposable)acquire.Invoke(mode, null);
            try
            {
                Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.False, "Feedback must not enter a diagnostic screenshot");
                Assert.That(mode.HoldFeedbackRoot.GetComponent<CanvasGroup>().alpha, Is.Zero);
            }
            finally { lease.Dispose(); }
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.True);
            InputBridge.PrimaryPointer = () => new Vector2(Screen.safeArea.center.x, Screen.safeArea.center.y);
            yield return null; yield return null;
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.False);
            Assert.That(mode.RestoreHoldProgress, Is.Zero);
            Assert.That(mode.HoldFeedbackRoot.activeSelf, Is.False);
            Assert.That(mode.IsUiHidden, Is.True);
            InputBridge.PrimaryPointer = () => new Vector2(Screen.safeArea.xMin + 4, Screen.safeArea.yMax - 4);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(mode.RestoreHoldProgress, Is.LessThan(.5f), "Re-entering starts a fresh hold");
            InputBridge.PrimaryPointer = () => null;
            yield return null; yield return null;
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.False);
            Assert.That(mode.RestoreHoldProgress, Is.Zero);
            Assert.That(mode.IsUiHidden, Is.True);
            mode.RestoreUi();
        }

        [UnityTest]
        public IEnumerator ThreeFingerHoldFeedbackRendersAndClearsOnRelease()
        {
            var cameraObject = new GameObject("Restore feedback camera", typeof(Camera)); objects.Add(cameraObject);
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            cameraObject.GetComponent<Camera>().backgroundColor = new Color(.07f, .17f, .2f);
            yield return Hide(ScreenshotGameplayUi.AllCanvases);
            mode.RestoreTouchHoldSeconds = 10;
            InputBridge.TouchCount = () => 3;
            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.True);
            Assert.That(mode.RestoreHoldProgress, Is.InRange(.2f, .7f));
            Assert.That(mode.HoldFeedbackRoot, Is.Not.Null);
            Assert.That(mode.HoldFeedbackRoot.activeInHierarchy, Is.True);
            Assert.That(mode.HoldFeedbackRoot.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1), "Feedback must remain visible while all gameplay canvases are hidden");
            var directory = Environment.GetEnvironmentVariable("DREAMY_DEBUGGER_VISUAL_EVIDENCE");
            {
                var keepEvidence = !string.IsNullOrEmpty(directory);
                if (!keepEvidence) directory = Path.GetTempPath();
                Directory.CreateDirectory(directory);
                var screenshot = Path.Combine(directory, keepEvidence ? "restore-hold-progress.png" : "restore-hold-" + Guid.NewGuid().ToString("N") + ".png");
                if (!keepEvidence) files.Add(screenshot);
                if (File.Exists(screenshot)) File.Delete(screenshot);
                ScreenCapture.CaptureScreenshot(screenshot);
                var view = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
                var deadline = Time.realtimeSinceStartupAsDouble + 3;
                while (!File.Exists(screenshot) && Time.realtimeSinceStartupAsDouble < deadline) { view.Repaint(); yield return null; }
                Assert.That(File.Exists(screenshot), Is.True, "Feedback visual evidence was not captured");
                view.Repaint(); yield return null; view.Repaint(); yield return null;
                var image = new Texture2D(2, 2);
                try
                {
                    Assert.That(image.LoadImage(File.ReadAllBytes(screenshot)), Is.True);
                    var accentPixels = image.GetPixels32().Count(pixel => pixel.r > pixel.g + 30 && pixel.r > 100);
                    Assert.That(accentPixels, Is.GreaterThan(8), "The visible progress arc must render above hidden canvases");
                }
                finally { Object.Destroy(image); }
            }
            InputBridge.TouchCount = () => 0;
            yield return null; yield return null;
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.False);
            Assert.That(mode.RestoreHoldProgress, Is.Zero);
            Assert.That(mode.IsUiHidden, Is.True);
            mode.RestoreUi();
            Assert.That(mode.IsRestoreHoldIndicatorVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator PointerHoldAndExpiryRestoreWithoutKeyboardOrMultitouch()
        {
            yield return Hide(ScreenshotGameplayUi.Keep);
            InputBridge.PrimaryPointer = () => new Vector2(Screen.safeArea.xMin + 4, Screen.safeArea.yMax - 4);
            yield return new WaitForSecondsRealtime(1.7f);
            Assert.That(mode.IsUiHidden, Is.False);
            InputBridge.PrimaryPointer = () => null;
            mode.AutoRestoreSeconds = 1;
            yield return Hide(ScreenshotGameplayUi.Keep);
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(mode.IsUiHidden, Is.False);
        }

        [UnityTest]
        public IEnumerator UnselectedRegisteredGroupsRemainVisible()
        {
            var first = Ui("Selected HUD", canvas: true);
            first.AddComponent<ScreenshotHideTarget>();
            var second = Ui("Keep controls", canvas: true);
            second.AddComponent<ScreenshotHideTarget>().Selected = false;
            yield return Hide(ScreenshotGameplayUi.RegisteredTargets);
            Assert.That(first.GetComponent<CanvasGroup>().alpha, Is.Zero);
            Assert.That(second.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            mode.RestoreUi();
        }

        [UnityTest]
        public IEnumerator PrimaryActionsStayVisibleInPortraitAndShortLandscape()
        {
            var cameraObject = new GameObject("Screenshot layout camera", typeof(Camera)); objects.Add(cameraObject);
            var camera = cameraObject.GetComponent<Camera>(); camera.enabled = false;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var canvasObject = Ui("Screenshot layout canvas", canvas: true);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = 1;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/com.dreamy.debugger/Runtime/Prefabs/ScreenshotTab.prefab");
            var instance = Object.Instantiate(prefab, canvasObject.transform); objects.Add(instance);
            var view = instance.GetComponent<ScreenshotTabController>();
            foreach (var size in new[] { new Vector2Int(320, 300), new Vector2Int(852, 260), new Vector2Int(393, 700) })
            {
                var render = new RenderTexture(size.x, size.y, 24); renders.Add(render); render.Create(); camera.targetTexture = render;
                yield return null; Canvas.ForceUpdateCanvases(); yield return null;
                var root = (RectTransform)instance.transform;
                foreach (var button in new[] { view.Capture, view.Hide })
                {
                    var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
                    var min = root.InverseTransformPoint(corners[0]); var max = root.InverseTransformPoint(corners[2]);
                    Assert.That(min.x, Is.GreaterThanOrEqualTo(root.rect.xMin - 1), size.ToString());
                    Assert.That(min.y, Is.GreaterThanOrEqualTo(root.rect.yMin - 1), size.ToString());
                    Assert.That(max.x, Is.LessThanOrEqualTo(root.rect.xMax + 1), size.ToString());
                    Assert.That(max.y, Is.LessThanOrEqualTo(root.rect.yMax + 1), size.ToString());
                    Assert.That(max.y - min.y, Is.GreaterThanOrEqualTo(48), size.ToString());
                }
                var scroll = view.GetComponentInChildren<ScrollRect>();
                Assert.That(view.Capture.transform.IsChildOf(scroll.viewport), Is.False);
                Assert.That(view.Hide.transform.IsChildOf(scroll.viewport), Is.False);
                foreach (var preset in view.Presets)
                {
                    var corners = new Vector3[4]; ((RectTransform)preset.transform).GetWorldCorners(corners);
                    Assert.That(root.InverseTransformPoint(corners[0]).x, Is.GreaterThanOrEqualTo(root.rect.xMin - 1), "Preset left edge " + size);
                    Assert.That(root.InverseTransformPoint(corners[2]).x, Is.LessThanOrEqualTo(root.rect.xMax + 1), "Preset right edge " + size);
                }
                var directory = Environment.GetEnvironmentVariable("DREAMY_DEBUGGER_VISUAL_EVIDENCE");
                if (!string.IsNullOrEmpty(directory))
                {
                    camera.Render(); var previous = RenderTexture.active; var image = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = render; image.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0); image.Apply();
                        Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "screenshot-tab-" + size.x + "x" + size.y + ".png"), image.EncodeToPNG());
                    }
                    finally { RenderTexture.active = previous; Object.Destroy(image); }
                }
            }
        }

        [UnityTest]
        public IEnumerator PngContainsGameplayWithUiKeptAndCleanPixelsWithTargetsHidden()
        {
            var cameraObject = new GameObject("Screenshot fixture camera", typeof(Camera));
            objects.Add(cameraObject);
            cameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            // Render the uGUI fixture in Game View before requesting the final-frame pixels.
            var gameView = EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show(); gameView.Focus(); gameView.Repaint();
            var background = Ui("Screenshot backdrop", canvas: true);
            background.AddComponent<Image>().color = Color.green;
            var marker = Ui("Gameplay overlay", background.transform);
            marker.AddComponent<Image>().color = Color.magenta;
            marker.AddComponent<ScreenshotHideTarget>();
            yield return null;
            foreach (var selection in new[] { ScreenshotGameplayUi.Keep, ScreenshotGameplayUi.RegisteredTargets })
            {
                mode.GameplayUi = selection;
                var task = mode.CapturePngAsync();
                var deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (!task.IsCompleted && Time.realtimeSinceStartupAsDouble < deadline)
                {
                    gameView.Repaint();
                    EditorApplication.QueuePlayerLoopUpdate();
                    yield return null;
                }
                Assert.That(task.IsCompleted, Is.True, "Capture did not terminate");
                var result = task.Result;
                Assert.That(result.Success, Is.True, result.Reason + " | " + mode.Status);
                files.Add(result.Path);
                Assert.That(mode.LastCapture, Is.SameAs(result));
                Assert.That(mode.Preview, Is.Not.Null);
                Assert.That(Mathf.Max(mode.Preview.width, mode.Preview.height), Is.LessThanOrEqualTo(512));
                Assert.That(mode.OverlayRoot, Is.Not.Null);
                var review = mode.OverlayRoot.GetComponent<ScreenshotOverlayView>();
                Assert.That(review.ResultCard.activeSelf, Is.True);
                review.View.onClick.Invoke();
                Assert.That(review.FullPreview.activeSelf, Is.True);
                var texture = new Texture2D(2, 2);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(result.Path)), Is.True);
                    // Game View may resize while native PNG IO is in flight; report the captured dimensions.
                    Assert.That(texture.width, Is.EqualTo(result.Width));
                    Assert.That(texture.height, Is.EqualTo(result.Height));
                    var pixel = texture.GetPixel(texture.width / 2, texture.height / 2);
                    var expected = selection == ScreenshotGameplayUi.Keep ? Color.magenta : Color.green;
                    Assert.That(pixel.r, Is.EqualTo(expected.r).Within(.05f));
                    Assert.That(pixel.g, Is.EqualTo(expected.g).Within(.05f));
                    Assert.That(pixel.b, Is.EqualTo(expected.b).Within(.05f));
                }
                finally { Object.Destroy(texture); }
                Assert.That(marker.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1));
            }
        }
    }
}
#endif
