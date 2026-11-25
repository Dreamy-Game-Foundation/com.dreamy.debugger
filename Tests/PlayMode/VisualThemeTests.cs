#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Presentation.UI.Controls.Data;
using Dreamy.Debugger.Reporting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace Dreamy.Debugger.Tests {
    public sealed class VisualThemeTests {
        private GameObject root;
        private DebuggerConfiguration config;
        private DebuggerRuntime runtime;
        [Test] public void SkinKeepsFlatSurfacesAndTransparentHitAreasSeparateFromRoundedControls(){
            var previous=PresentationAssets.Catalog;
            var host=new GameObject("Surface policy test");
            try {
                PresentationAssets.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
                var scroll=new GameObject("ScrollBackground",typeof(RectTransform),typeof(Image));scroll.transform.SetParent(host.transform);
                var hit=scroll.GetComponent<Image>();hit.color=new Color(0,0,0,0);hit.raycastTarget=true;
                var background=new GameObject("Background",typeof(RectTransform),typeof(Image));background.transform.SetParent(host.transform);
                var button=new GameObject("Button",typeof(RectTransform),typeof(Image),typeof(Button));button.transform.SetParent(host.transform);
                var buttonBackground=new GameObject("BG",typeof(RectTransform),typeof(Image));buttonBackground.transform.SetParent(button.transform);
                PresentationAssets.ApplySkin(host);PresentationAssets.ApplySkin(host);
                Assert.That(hit.sprite,Is.Null);Assert.That(hit.color.a,Is.Zero);Assert.That(hit.raycastTarget,Is.True);
                Assert.That(background.GetComponent<Image>().sprite,Is.Null);Assert.That(background.GetComponent<Image>().color.a,Is.EqualTo(1));
                Assert.That(button.GetComponent<Image>().sprite,Is.SameAs(PresentationAssets.Load<Sprite>("DreamyDebugger/Theme/Button")));
                Assert.That(buttonBackground.GetComponent<Image>().type,Is.EqualTo(Image.Type.Sliced));
            } finally {PresentationAssets.Catalog=previous;Object.DestroyImmediate(host);}
        }
        [Test] public void RoundedSurfaceHasOpaqueStraightEdgesAndTransparentCorners(){
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false);
            try {
                texture.LoadImage(System.IO.File.ReadAllBytes(System.IO.Path.Combine(UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.dreamy.debugger/package.json").resolvedPath,"Runtime/Branding/Material/rounded-fill.png")));
                foreach(var point in new[]{new Vector2Int(0,16),new Vector2Int(31,16),new Vector2Int(16,0),new Vector2Int(16,31)})
                    Assert.That(texture.GetPixel(point.x,point.y).a,Is.EqualTo(1),point.ToString());
                Assert.That(texture.GetPixel(0,0).a,Is.Zero);
                var sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Packages/com.dreamy.debugger/Runtime/Branding/Material/rounded-fill.png");
                Assert.That(sprite.border,Is.EqualTo(new Vector4(8,8,8,8)));
            } finally {Object.DestroyImmediate(texture);}
        }
        private IEnumerator Create(){
            if(DebuggerRuntime.Instance!=null)Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null;
            root=new GameObject("Visual theme test");root.SetActive(false);
            config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.PersistRuntimePreferences=false;config.Credential="0000";
            runtime=root.AddComponent<DebuggerRuntime>();runtime.ConfigurationAsset=config;
            runtime.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");
            root.SetActive(true);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(root!=null)Object.Destroy(root);if(config!=null)Object.Destroy(config);yield return null;yield return null;}
        private IEnumerator UnlockOptions(){
            runtime.Open(DefaultTabs.Options);yield return null;
            var pin=Object.FindFirstObjectByType<PinEntryControl>();if(pin!=null)for(var i=0;i<4;i++)pin.PushNumber(0);
            yield return null;yield return null;
        }
        [UnityTest] public IEnumerator ValuesRefreshAfterActionAndExternalChangesWithoutRecreatingControls(){
            yield return Create();long coins=100,amount=25;bool owned=false;string status="ready";
            using(var lease=runtime.Cheats.Register(new[]{
                CheatOption.Value("live.coins","Test","Live coins",()=>coins),
                CheatOption.Value("live.amount","Test","Live amount",()=>amount,v=>amount=v),
                CheatOption.Value("live.owned","Test","Live owned",()=>owned),
                CheatOption.Value("live.status","Test","Live status",()=>status),
                CheatOption.Button("live.add","Test","Live add",()=>{coins+=amount;owned=true;status="saved";})})){
                yield return UnlockOptions();
                Assert.That(DreamyDebug.Instance.IsDebugPanelVisible,Is.True,"Options must open");
                Assert.That(Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Select(c=>c.PropertyName),Does.Contain("Live coins"),"Active="+Dreamy.Debugger.Presentation.Internal.Service.Panel.ActiveTab+" Registry="+runtime.Cheats.Snapshot().Count+" Presentation="+Dreamy.Debugger.Presentation.Internal.Service.Options.Options.Count+" Controls present: "+string.Join(",",Object.FindObjectsByType<DataBoundControl>(FindObjectsSortMode.None).Select(c=>c.PropertyName)));
                var coin=Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Live coins");
                var state=Object.FindObjectsByType<ReadOnlyControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Live status");
                var ownership=Object.FindObjectsByType<BoolControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Live owned");
                var id=coin.GetInstanceID();runtime.Cheats.Execute("live.add");
                Assert.That(coin.NumberSpinner.text,Is.EqualTo("125"));Assert.That(state.ValueText.text,Is.EqualTo("saved"));Assert.That(ownership.Toggle.isOn,Is.True);
                coins=250;owned=false;status="external";yield return new WaitForSecondsRealtime(.4f);
                Assert.That(coin.NumberSpinner.text,Is.EqualTo("250"));Assert.That(ownership.Toggle.isOn,Is.False);Assert.That(state.ValueText.text,Is.EqualTo("external"));
                Assert.That(coin.GetInstanceID(),Is.EqualTo(id));
            }
        }
        [UnityTest] public IEnumerator ThrowingSetterIsContainedRollsBackAndCanRecover(){
            yield return Create();bool value=false,reject=true,healthy=false;int calls=0;
            using(var lease=runtime.Cheats.Register(new[]{
                CheatOption.Value("write.bad","Test","Unavailable God Mode",()=>value,v=>{
                    calls++;if(reject)throw new System.Reflection.TargetInvocationException(new System.InvalidOperationException("Command unavailable: battle.god-mode.on"));value=v;
                }),
                CheatOption.Value("write.healthy","Test","Healthy toggle",()=>healthy,v=>healthy=v)})){
                yield return UnlockOptions();
                var bad=Object.FindObjectsByType<BoolControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Unavailable God Mode");
                var good=Object.FindObjectsByType<BoolControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Healthy toggle");
                LogAssert.Expect(LogType.Warning,"[DreamyDebugger] Failed to set 'Unavailable God Mode': Command unavailable: battle.god-mode.on");
                Assert.DoesNotThrow(()=>bad.Toggle.isOn=true);
                Assert.That(value,Is.False);Assert.That(bad.Toggle.isOn,Is.False);Assert.That(calls,Is.EqualTo(1));
                Assert.That(bad.BindingError,Is.EqualTo("InvalidOperationException"));
                good.Toggle.isOn=true;Assert.That(healthy,Is.True);
                reject=false;runtime.Cheats.NotifyValuesChanged();Assert.That(bad.BindingError,Is.Null);
                bad.Toggle.isOn=true;Assert.That(value,Is.True);Assert.That(bad.Toggle.isOn,Is.True);Assert.That(calls,Is.EqualTo(2));
            }
        }
        [UnityTest] public IEnumerator RejectedWritesRenderActualValueAndRefreshDoesNotEmitInput(){
            yield return Create();bool value=false,available=false;int calls=0,inputEvents=0;
            using(var lease=runtime.Cheats.Register(new[]{CheatOption.Value("write.reject","Test","Rejected toggle",()=>value,v=>{calls++;if(available)value=v;})})){
                yield return UnlockOptions();
                var control=Object.FindObjectsByType<BoolControl>(FindObjectsSortMode.None).Single(c=>c.PropertyName=="Rejected toggle");
                control.Toggle.isOn=true;
                Assert.That(value,Is.False);Assert.That(control.Toggle.isOn,Is.False);Assert.That(calls,Is.EqualTo(1));
                control.Toggle.onValueChanged.AddListener(_=>inputEvents++);
                available=true;value=true;runtime.Cheats.NotifyValuesChanged();
                Assert.That(control.Toggle.isOn,Is.True);Assert.That(inputEvents,Is.Zero);Assert.That(calls,Is.EqualTo(1));
                control.Toggle.isOn=false;Assert.That(value,Is.False);Assert.That(calls,Is.EqualTo(2));Assert.That(inputEvents,Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator BrokenGetterIsIsolatedAndCanRecover(){
            yield return Create();bool broken=false;string value="before";
            using(var lease=runtime.Cheats.Register(new[]{CheatOption.Value<string>("bad","Test","Bad getter",()=>{if(broken)throw new System.Exception("unavailable");return value;}),CheatOption.Value("good","Test","Good getter",()=>value)})){
                yield return UnlockOptions();var controls=Object.FindObjectsByType<ReadOnlyControl>(FindObjectsSortMode.None);
                var bad=controls.Single(c=>c.PropertyName=="Bad getter");var good=controls.Single(c=>c.PropertyName=="Good getter");
                broken=true;value="after";runtime.Cheats.NotifyValuesChanged();
                Assert.That(bad.BindingError,Is.Not.Null);Assert.That(good.ValueText.text,Is.EqualTo("after"));
                broken=false;runtime.Cheats.NotifyValuesChanged();Assert.That(bad.BindingError,Is.Null);Assert.That(bad.ValueText.text,Is.EqualTo("after"));
            }
        }
        [UnityTest] public IEnumerator MissingGraphShaderFallsBackToTextAndDisabledCaptureNeverHoldsBusy(){
            yield return Create();runtime.SetHud(HudMode.FpsOnly);yield return null;
            var graph=runtime.Hud.GetComponentInChildren<GraphyHudGraph>(true);Assert.That(graph.IsInitialized,Is.False);
            var serialized=new SerializedObject(graph);serialized.FindProperty("graphShader").objectReferenceValue=null;serialized.ApplyModifiedPropertiesWithoutUndo();
            runtime.SetHud(HudMode.Detailed);yield return null;
            Assert.That(graph.gameObject.activeSelf,Is.False);Assert.That(runtime.Hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("fps")),Is.True);
            var capture=runtime.GetComponent<CaptureCoordinator>();capture.enabled=false;
            var task=capture.CaptureAsync(960,System.Threading.CancellationToken.None);while(!task.IsCompleted)yield return null;
            Assert.That(task.Result.Reason,Is.EqualTo("capture disabled"));Assert.That(capture.IsBusy,Is.False);
        }
        [UnityTest] public IEnumerator ServiceManagerRestartRebindsCheatsHudAndConsole(){
            yield return Create();yield return UnlockOptions();DreamyDebug.Instance.HideDebugPanel();runtime.SetHud(HudMode.Detailed);
            Assert.That(Dreamy.Debugger.Foundation.Service.ServiceManager.HasService<Dreamy.Debugger.Presentation.Services.Implementation.KeyboardShortcutListenerService>(),Is.True);
            var manager=Dreamy.Debugger.Foundation.Service.ServiceManager.Instance;
            var before=Dreamy.Debugger.Foundation.Service.ServiceManager.GetService<Dreamy.Debugger.Presentation.Services.IConsoleService>();
            using(var lease=runtime.Cheats.Register(new[]{CheatOption.Value("restart.value","Test","Restart value",()=>42)})){
                manager.enabled=false;manager.enabled=true;
                yield return null;yield return null;
                var after=Dreamy.Debugger.Foundation.Service.ServiceManager.GetService<Dreamy.Debugger.Presentation.Services.IConsoleService>();
                Assert.That(after,Is.Not.Null);Assert.That(after,Is.Not.SameAs(before));
                yield return UnlockOptions();
                Assert.That(Object.FindObjectsByType<NumberControl>(FindObjectsSortMode.None).Any(c=>c.PropertyName=="Restart value"),Is.True);
                DreamyDebug.Instance.HideDebugPanel();yield return null;
                Assert.That(runtime.Hud.GetComponent<Canvas>().enabled,Is.True);
            }
        }
        [UnityTest] public IEnumerator ReportTabIsAvailableAndHudIsOffByDefault(){
            yield return Create();
            Assert.That(runtime.Hud,Is.Null);
            Assert.That(Dreamy.Debugger.Presentation.Profiler.DreamyProfilerService.CollectForHud,Is.False);
            yield return UnlockOptions();
            var tabs=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.Other.DebugTabController>();
            Assert.That(tabs.Tabs.Any(t=>t.Key==DefaultTabs.BugReporter.ToString() && t.Title=="Report"),Is.True);
            Assert.That(runtime.Cheats.Snapshot().Any(o=>o.Id=="debugger.fake-report"),Is.False);
        }
        [UnityTest] public IEnumerator ReportPanelSavesManualDraftAndAutoGroupsRepeatedErrors(){
            yield return Create();
            yield return UnlockOptions();
            runtime.Open(DefaultTabs.BugReporter);
            yield return null;yield return null;
            var sheet=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.Other.BugReportSheetController>();
            Assert.That(sheet,Is.Not.Null);
            Assert.That(sheet.ButtonText.text,Is.EqualTo("Save report"));
            Assert.That(sheet.EmailField.gameObject.activeInHierarchy,Is.False);
            var manualMessage="Manual QA "+System.Guid.NewGuid().ToString("N");
            sheet.DescriptionField.text=manualMessage;
            sheet.SubmitButton.onClick.Invoke();
            var deadline=Time.realtimeSinceStartupAsDouble+5;
            while(string.IsNullOrEmpty(runtime.LastManualReportId) && Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(runtime.LastManualReportId,Is.Not.Null);
            var manualId=runtime.LastManualReportId;
            Assert.That(runtime.AutoReportingEnabled,Is.False);
            Assert.That(runtime.TrySetAutoReporting(true),Is.True);
            var autoMessage="Automatic QA "+System.Guid.NewGuid().ToString("N");
            for(var index=0;index<3;index++){
                LogAssert.Expect(LogType.Error,autoMessage);
                Debug.LogError(autoMessage);
            }
            yield return new WaitForSecondsRealtime(.65f);
            var read=runtime.Outbox.ReadLocalAsync();
            while(!read.IsCompleted)yield return null;
            var entries=read.GetAwaiter().GetResult();
            var manual=entries.Single(item=>item.Bundle.Report.Id==manualId);
            Assert.That(manual.Bundle.Report.Message,Is.EqualTo(manualMessage));
            Assert.That(manual.Bundle.ImageBytes,Is.Zero);
            var automatic=entries.Single(item=>item.Bundle.Report.Message==autoMessage);
            Assert.That(automatic.Bundle.Details.OccurrenceCount,Is.EqualTo(3));
            Assert.That(automatic.Bundle.Details.Severity,Is.EqualTo("Error"));
            Assert.That(automatic.Bundle.Details.StackTrace,Is.Not.Empty);
            Assert.That(runtime.TrySetAutoReporting(false),Is.True);
            var cleanup=System.Threading.Tasks.Task.WhenAll(runtime.Outbox.DeleteAsync(manualId),runtime.Outbox.DeleteAsync(automatic.Bundle.Report.Id));
            while(!cleanup.IsCompleted)yield return null;
        }

        [UnityTest]
        public IEnumerator ConsoleContextOpensHierarchyAndSelectsTheLoggedObject()
        {
            yield return Create();
            yield return UnlockOptions();
            runtime.Open(DefaultTabs.Console);
            yield return null;
            yield return null;

            var target = new GameObject("Context target " + System.Guid.NewGuid().ToString("N"));
            try
            {
                var tab = Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.Tabs.ConsoleTabController>();
                var console = Dreamy.Debugger.Foundation.Service.ServiceManager.GetService<Dreamy.Debugger.Presentation.Services.IConsoleService>();
                Debug.Log("Context-free log");
                Debug.Log("Log with component context", target.transform);
                yield return null;
                yield return null;
                var inspect = tab.GetComponentsInChildren<Button>(true).Single(item => item.name == "Inspect object");
                tab.ConsoleLogControl.SelectEntry(console.AllEntries.Last(item => item.Message == "Context-free log"));
                Assert.That(inspect.gameObject.activeInHierarchy, Is.False);
                tab.ConsoleLogControl.SelectEntry(console.AllEntries.Last(item => item.Message == "Log with component context"));
                Assert.That(inspect.gameObject.activeInHierarchy, Is.True);
                inspect.onClick.Invoke();
                for (var frame=0; frame<12; frame++) yield return null;
                var inspector = Object.FindFirstObjectByType<Dreamy.Debugger.SceneInspection.SceneTabController>();
                Assert.That(inspector,Is.Not.Null);
                Assert.That(inspector.Header.text,Does.Contain(target.name));
            }
            finally { Object.Destroy(target); }
        }

        [UnityTest]
        public IEnumerator ReportPanelTestExceptionCapturesAnAutomaticReportWithStackTrace()
        {
            yield return Create();
            yield return UnlockOptions();
            runtime.Open(DefaultTabs.BugReporter);
            yield return null;
            yield return null;

            var tab = Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.Tabs.BugReportTabController>();
            var button = tab.GetComponentsInChildren<Button>().Single(item => item.name == "Test exception");
            var original = runtime.Outbox.ReadLocalAsync();
            while (!original.IsCompleted) yield return null;
            var originalIds = original.GetAwaiter().GetResult().Select(item => item.Bundle.Report.Id).ToHashSet();
            Assert.That(runtime.TrySetAutoReporting(true), Is.True);
            var console=Dreamy.Debugger.Foundation.Service.ServiceManager.GetService<Dreamy.Debugger.Presentation.Services.IConsoleService>();
            var longTrace=string.Join("\n",Enumerable.Range(0,420).Select(index=>"Game.Frame"+index+"() (at Assets/Example.cs:"+index+")"))+"\nTRACE_TAIL";
            console.GetType().GetMethod("UnityLogCallback",BindingFlags.NonPublic|BindingFlags.Instance)
                .Invoke(console,new object[]{"Previous warning password=fixture-private",longTrace,LogType.Warning});
            yield return null;

            const string message = "Dreamy Debugger test exception (requested from Report).";
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: " + message);
            button.onClick.Invoke();
            PendingReport captured = null;
            var deadline = Time.realtimeSinceStartupAsDouble + 5;
            while (captured == null && Time.realtimeSinceStartupAsDouble < deadline)
            {
                var read = runtime.Outbox.ReadLocalAsync();
                while (!read.IsCompleted) yield return null;
                captured = read.GetAwaiter().GetResult().FirstOrDefault(item =>
                    !originalIds.Contains(item.Bundle.Report.Id) && item.Bundle.Report.Message.Contains(message));
                yield return null;
            }

            Assert.That(captured, Is.Not.Null);
            var cleanup = runtime.Outbox.DeleteAsync(captured.Bundle.Report.Id);
            while (!cleanup.IsCompleted) yield return null;
            Assert.That(cleanup.GetAwaiter().GetResult(), Is.True);
            Assert.That(captured.Bundle.Details.Source, Is.EqualTo("auto"));
            Assert.That(captured.Bundle.Details.Severity, Is.EqualTo("Exception"));
            Assert.That(captured.Bundle.Details.StackTrace, Does.Contain("LogReportTestException"));
            Assert.That(captured.Bundle.Context,Does.Contain("TRACE_TAIL").And.Contain("[REDACTED]").And.Not.Contain("fixture-private"));
        }

        [UnityTest]
        public IEnumerator ReportPanelSendsInOneTapAndRetainsOfflineReports()
        {
            yield return Create();
            var relay = new ReportRelayFake();
            typeof(DebuggerRuntime).GetField("pump", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(runtime, new OutboxPump(runtime.Outbox, relay, keepAccepted: true));
            Assert.That(runtime.SetReportUploadPermission(true), Is.True);
            yield return UnlockOptions();
            runtime.Open(DefaultTabs.BugReporter);
            yield return null;
            yield return null;

            var sheet = Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.Other.BugReportSheetController>();
            Assert.That(sheet, Is.Not.Null);
            Assert.That(sheet.ButtonText.text, Is.EqualTo("Send report"));
            foreach (var online in new[] { true, false })
            {
                relay.Online = online;
                var message = "Report QA " + System.Guid.NewGuid().ToString("N");
                sheet.DescriptionField.text = message;
                sheet.SubmitButton.onClick.Invoke();
                if (!online) sheet.gameObject.SetActive(false);
                var deadline = Time.realtimeSinceStartupAsDouble + 5;
                while (!sheet.SubmitButton.interactable && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.That(sheet.SubmitButton.interactable, Is.True);
                sheet.gameObject.SetActive(true);
                Assert.That(sheet.DescriptionField.text, Is.Empty);
                Assert.That(relay.LastReportId, Is.EqualTo(runtime.LastManualReportId));

                var read = runtime.Outbox.ReadLocalAsync();
                while (!read.IsCompleted) yield return null;
                var report = read.GetAwaiter().GetResult().Single(item => item.Bundle.Report.Id == relay.LastReportId);
                Assert.That(report.Bundle.Report.Message, Is.EqualTo(message));
                Assert.That(report.State, Is.EqualTo(online ? LocalReportState.RelayAccepted : LocalReportState.Pending));
                Assert.That(report.Attempts, Is.EqualTo(online ? 0 : 1));
                var cleanup = runtime.Outbox.DeleteAsync(report.Bundle.Report.Id);
                while (!cleanup.IsCompleted) yield return null;
                Assert.That(cleanup.GetAwaiter().GetResult(), Is.True);
            }

            Assert.That(runtime.SetReportUploadPermission(false), Is.True);
            yield return null;
            Assert.That(sheet.ButtonText.text, Is.EqualTo("Save report"));
        }

        private sealed class ReportRelayFake : IBundleTransport
        {
            public bool Online;
            public string LastReportId;

            public Task<RelayResult> SendBundleAsync(ReportBundle bundle, CancellationToken token)
            {
                LastReportId = bundle.Report.Id;
                return Task.FromResult(new RelayResult(Online ? DeliveryStatus.Accepted : DeliveryStatus.Retry));
            }
        }

        private void ClickMenuThroughRaycast(Button button){
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)button.transform;
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){
                position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),
                button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits,Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button));
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        [UnityTest] public IEnumerator HamburgerOpensAndClosesSidebarWithPausedGame(){
            yield return Create();yield return UnlockOptions();
            var menu=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.MobileMenuController>();
            Assert.That(menu.enabled,Is.True);Assert.That(menu.OpenButton.gameObject.activeInHierarchy,Is.True);
            var previous=Time.timeScale;
            try{
                Time.timeScale=0;menu.Close();ClickMenuThroughRaycast(menu.OpenButton);Assert.That(menu.IsOpen,Is.True);
                Assert.That(menu.SidebarLogo.activeInHierarchy,Is.True);Assert.That(menu.HeaderLogo.activeInHierarchy,Is.False);
                yield return new WaitForSecondsRealtime(.5f);
                var animatedOffset=menu.IsNarrow?menu.Menu.anchoredPosition.x:menu.Content.anchoredPosition.x;
                Assert.That(animatedOffset,Is.EqualTo(menu.Menu.rect.width).Within(2));
                Canvas.ForceUpdateCanvases();
                var close=(RectTransform)menu.Content.Find("Main/Header/MenuBar/CloseButton");
                var corners=new Vector3[4];close.GetWorldCorners(corners);
                Assert.That(corners.Max(p=>p.x),Is.LessThanOrEqualTo(Screen.width+1));
                ClickMenuThroughRaycast(menu.IsNarrow?menu.DrawerClose:menu.OpenButton);Assert.That(menu.IsOpen,Is.False);
                Assert.That(menu.HeaderLogo.activeInHierarchy,Is.True);Assert.That(menu.SidebarLogo.activeInHierarchy,Is.False);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(menu.Content.anchoredPosition.x,Is.EqualTo(0).Within(2));
            }finally{Time.timeScale=previous;}
        }
        [UnityTest] public IEnumerator HudGripDragsWithinViewportAndOtherGraphicsPassThrough(){
            yield return Create();yield return UnlockOptions();DreamyDebug.Instance.HideDebugPanel();
            runtime.SetHud(HudMode.Detailed);yield return null;Canvas.ForceUpdateCanvases();
            var hud=runtime.Hud;var handle=hud.GetComponentInChildren<HudDragHandle>();
            Assert.That(handle,Is.Not.Null);
            var gripCorners=new Vector3[4];((RectTransform)handle.transform.Find("Drag hit area")).GetWorldCorners(gripCorners);Assert.That(Vector3.Distance(gripCorners[0],gripCorners[3]),Is.GreaterThanOrEqualTo(47.9f));
            Assert.That(hud.GetComponentsInChildren<Graphic>(true).Where(g=>g.raycastTarget).All(g=>g.transform.IsChildOf(handle.transform) || g.transform.IsChildOf(hud.CycleButton.transform) || g.transform.IsChildOf(hud.CloseButton.transform)),Is.True);
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,handle.transform.position)};
            handle.OnBeginDrag(data);Assert.That(hud.IsDragging,Is.True);
            var card=(RectTransform)handle.transform.parent;var before=card.anchoredPosition;
            data.position+=new Vector2(-150,-100);handle.OnDrag(data);handle.OnEndDrag(data);
            Assert.That(hud.Position,Is.EqualTo(HudPosition.Free));Assert.That(hud.IsDragging,Is.False);
            Assert.That(Vector2.Distance(before,card.anchoredPosition),Is.GreaterThan(1));
            data.position=new Vector2(-100000,-100000);handle.OnBeginDrag(data);data.position+=new Vector2(200000,200000);handle.OnDrag(data);handle.OnEndDrag(data);
            var corners=new Vector3[4];card.GetWorldCorners(corners);var parent=(RectTransform)card.parent;
            foreach(var world in corners){var point=(Vector2)parent.InverseTransformPoint(world);Assert.That(point.x,Is.InRange(parent.rect.xMin-.1f,parent.rect.xMax+.1f));Assert.That(point.y,Is.InRange(parent.rect.yMin-.1f,parent.rect.yMax+.1f));}
            Assert.That(hud.GetComponentsInChildren<GraphyHudGraph>().Length,Is.EqualTo(1));
            runtime.SetHud(HudMode.Off);runtime.SetHud(HudMode.Detailed);Assert.That(hud.Position,Is.EqualTo(HudPosition.Free));
        }
        [UnityTest] public IEnumerator HudPositionResetAndAudioOptInAreApplied(){
            yield return Create();
            config.InitialHudPosition=HudPosition.BottomLeft;
            config.HudOffset=new Vector2(18,24);
            config.HudAudio=false;
            runtime.Access.RequestPrompt();runtime.Access.SubmitPin("0000");
            runtime.SetHudPosition(HudPosition.BottomRight);
            runtime.SetHud(HudMode.Detailed);
            Assert.That(runtime.Hud.Position,Is.EqualTo(HudPosition.BottomRight));
            var hud=runtime.Hud;hud.Configure(config);yield return null;
            Assert.That(hud.Position,Is.EqualTo(HudPosition.BottomLeft));
            Assert.That(hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("AUDIO")),Is.False);
            hud.SetPosition(HudPosition.TopRight);Assert.That(hud.Position,Is.EqualTo(HudPosition.TopRight));
            hud.ResetPosition();Assert.That(hud.Position,Is.EqualTo(HudPosition.BottomLeft));
            var card=(RectTransform)hud.GetComponentsInChildren<Text>().First().transform.parent;
            Assert.That(card.anchorMin,Is.EqualTo(Vector2.zero));
            Assert.That(card.anchoredPosition,Is.EqualTo(new Vector2(18,24)));
        }
        [UnityTest] public IEnumerator ErrorIngressIsBoundedAndOnlyReportOriginIsSuppressed(){
            yield return Create();yield return UnlockOptions();
            Assert.That(runtime.TrySetAutoReporting(true),Is.True);
            var ingress=typeof(DebuggerRuntime).GetMethod("OnLog",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            for(var i=0;i<40;i++)ingress.Invoke(runtime,new object[]{"[Dreamy] game error "+i,"stack",LogType.Error});
            Assert.That(runtime.DroppedErrors,Is.EqualTo(8));
            ingress.Invoke(runtime,new object[]{"[DreamyDebugger.Report] delivery failed","",LogType.Error});
            Assert.That(runtime.DroppedErrors,Is.EqualTo(8));
        }
        [UnityTest] public IEnumerator RuntimeCleanupSurvivesPresentationServiceAlreadyQuitting(){
            yield return Create();runtime.SetHud(HudMode.FpsOnly);
            var lease=runtime.Cheats.Register(new[]{CheatOption.Value("shutdown.value","Test","Value",()=>1)});
            var manager=typeof(Dreamy.Debugger.Foundation.Service.ServiceManager);
            var quitting=manager.GetField("_hasQuit",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            quitting.SetValue(null,true);
            try{
                Object.Destroy(GameObject.Find("DreamyDebugger"));yield return null;
                lease.Dispose();
                Object.Destroy(root);yield return null;yield return null;
                Assert.That(DebuggerRuntime.Instance,Is.Null);
                Assert.That(GameObject.Find("DreamyDebugger"),Is.Null);
                Assert.That(Dreamy.Debugger.Presentation.Profiler.DreamyProfilerService.CollectForHud,Is.False);
            }finally{Dreamy.Debugger.Foundation.Service.ServiceManager.RuntimeInitialize();}
        }
        [UnityTest] public IEnumerator NumberValuesAndActionLabelsFitAfterFontAndRowChanges(){
            yield return Create();int value=123;
            runtime.Access.RequestPrompt();runtime.Access.SubmitPin("0000");
            var canvasObject=new GameObject("Visual control fixture",typeof(RectTransform),typeof(Canvas));
            canvasObject.transform.SetParent(root.transform,false);canvasObject.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            const string folder="Packages/com.dreamy.debugger/Runtime/Imported/Presentation/Assets/DreamyDebugger/UI/Prefabs/Options/";
            var number=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"NumberOption.prefab"),canvasObject.transform).GetComponent<NumberControl>();
            number.Bind("Number",OptionDefinition.Create("Number",()=>value,v=>value=v).Property);
            var rect=(RectTransform)number.transform;rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,380);rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,44);
            var action=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(folder+"ActionOption.prefab"),canvasObject.transform).GetComponent<ActionControl>();
            action.Title.text="Test action";
            yield return null;Canvas.ForceUpdateCanvases();
            var text=number.NumberSpinner.textComponent;
            Assert.That(text.text,Is.EqualTo("123"));Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(text.preferredHeight));
            Assert.That(text.rectTransform.rect.width,Is.GreaterThan(20));
            Assert.That(action.Title.verticalOverflow,Is.EqualTo(VerticalWrapMode.Overflow));
            Assert.That(action.Button.interactable,Is.True);
        }
        [UnityTest] public IEnumerator HudModesUseLiveMetricsAndHideBehindPanel(){
            yield return Create();runtime.SetHud(HudMode.Detailed);
            yield return null;yield return null;
            var hud=runtime.Hud;var canvas=hud.GetComponent<Canvas>();
            var lowSummary=hud.GetComponentsInChildren<Text>().Single(t=>t.name=="Summary");
            Assert.That(hud.GetComponentsInChildren<Text>().Any(t=>t.text.StartsWith("1%")),Is.True);
            Assert.That(lowSummary.rectTransform.rect.height,Is.GreaterThanOrEqualTo(lowSummary.preferredHeight));
            Assert.That(hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("Reserved")),Is.True);
            Assert.That(hud.GetComponentsInChildren<Text>().All(t=>!t.text.Contains("NaN")&&!t.text.Contains("Infinity")),Is.True);
            runtime.SetHud(HudMode.FpsOnly);yield return null;
            Assert.That(hud.GetComponentsInChildren<HudHistoryGraphic>().Length,Is.Zero);
            Assert.That(hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("MB")),Is.False);
            runtime.SetHud(HudMode.Compact);yield return null;
            Assert.That(hud.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("MB allocated")),Is.True);
            runtime.Open(DefaultTabs.Options);yield return null;
            var pin=Object.FindFirstObjectByType<PinEntryControl>();pin.Clear();for(var i=0;i<4;i++)pin.PushNumber(0);
            yield return null;yield return null;
            Assert.That(DreamyDebug.Instance.IsDebugPanelVisible,Is.True);Assert.That(canvas.enabled,Is.False);
            DreamyDebug.Instance.HideDebugPanel();yield return null;Assert.That(canvas.enabled,Is.True);
            runtime.SetHud(HudMode.Off);Assert.That(hud.gameObject.activeSelf,Is.False);
        }
    }
}
#endif
