#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI.Controls;
using Dreamy.Debugger.Presentation.UI.Controls.Data;
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
        [UnityTest] public IEnumerator DefaultHudIsOffAndPackageReportingIsAbsentFromRuntimePanel(){
            yield return Create();
            Assert.That(runtime.Hud,Is.Null);
            Assert.That(Dreamy.Debugger.Presentation.Profiler.ProfilerServiceImpl.CollectForHud,Is.False);
            yield return UnlockOptions();
            var tabs=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.Other.DebugTabController>();
            Assert.That(tabs.Tabs.Any(t=>t.Key==DefaultTabs.BugReporter.ToString()),Is.False);
            Assert.That(runtime.Cheats.Snapshot().Any(o=>o.Id=="debugger.fake-report"),Is.False);
        }
        private void ClickMenuThroughRaycast(Dreamy.Debugger.Presentation.UI.MobileMenuController menu){
            Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)menu.OpenButton.transform;
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){
                position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)),
                button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            var hits=new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer,hits);
            Assert.That(hits,Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(menu.OpenButton));
            UnityEngine.EventSystems.ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
        }
        [UnityTest] public IEnumerator HamburgerOpensAndClosesSidebarWithPausedGame(){
            yield return Create();yield return UnlockOptions();
            var menu=Object.FindFirstObjectByType<Dreamy.Debugger.Presentation.UI.MobileMenuController>();
            Assert.That(menu.enabled,Is.True);Assert.That(menu.OpenButton.gameObject.activeInHierarchy,Is.True);
            var previous=Time.timeScale;
            try{
                Time.timeScale=0;menu.Close();ClickMenuThroughRaycast(menu);Assert.That(menu.IsOpen,Is.True);
                Assert.That(menu.SidebarLogo.activeInHierarchy,Is.True);Assert.That(menu.HeaderLogo.activeInHierarchy,Is.False);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(menu.Content.anchoredPosition.x,Is.EqualTo(menu.Menu.rect.width).Within(2));
                Canvas.ForceUpdateCanvases();
                var close=(RectTransform)menu.Content.Find("Main/Header/MenuBar/CloseButton");
                var corners=new Vector3[4];close.GetWorldCorners(corners);
                Assert.That(corners.Max(p=>p.x),Is.LessThanOrEqualTo(Screen.width+1));
                ClickMenuThroughRaycast(menu);Assert.That(menu.IsOpen,Is.False);
                Assert.That(menu.HeaderLogo.activeInHierarchy,Is.True);Assert.That(menu.SidebarLogo.activeInHierarchy,Is.False);
                yield return new WaitForSecondsRealtime(.5f);
                Assert.That(menu.Content.anchoredPosition.x,Is.EqualTo(0).Within(2));
            }finally{Time.timeScale=previous;}
        }
        [UnityTest] public IEnumerator HudGripDragsWithinSafeAreaAndOtherGraphicsPassThrough(){
            yield return Create();yield return UnlockOptions();DreamyDebug.Instance.HideDebugPanel();
            runtime.SetHud(HudMode.Detailed);yield return null;Canvas.ForceUpdateCanvases();
            var hud=runtime.Hud;var handle=hud.GetComponentInChildren<HudDragHandle>();
            Assert.That(handle,Is.Not.Null);
            var gripCorners=new Vector3[4];((RectTransform)handle.transform).GetWorldCorners(gripCorners);Assert.That(Vector3.Distance(gripCorners[0],gripCorners[3]),Is.GreaterThanOrEqualTo(47.9f));
            Assert.That(hud.GetComponentsInChildren<Graphic>(true).Where(g=>g.raycastTarget).All(g=>g.gameObject==handle.gameObject),Is.True);
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
            runtime.Cheats.Set("debugger.hud-position",HudPosition.BottomRight);
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
            yield return Create();
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
                Assert.That(Dreamy.Debugger.Presentation.Profiler.ProfilerServiceImpl.CollectForHud,Is.False);
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
