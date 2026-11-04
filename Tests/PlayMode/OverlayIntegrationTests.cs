#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.UI.Controls;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
namespace Dreamy.Debugger.Tests {
    public sealed class OverlayIntegrationTests {
        private GameObject root;
        private DebuggerConfiguration config;
        private DebuggerRuntime runtime;
        private IEnumerator Create(bool passphrase=false){
            if(DebuggerRuntime.Instance!=null)UnityEngine.Object.Destroy(DebuggerRuntime.Instance.gameObject);
            yield return null;
            root=new GameObject("Dreamy integration test");root.SetActive(false);
            config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.Passphrase=passphrase;config.Credential=passphrase?"Dreamy QA secret":"0000";
            runtime=root.AddComponent<DebuggerRuntime>();runtime.ConfigurationAsset=config;runtime.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");root.SetActive(true);yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(root!=null)UnityEngine.Object.Destroy(root);if(config!=null)UnityEngine.Object.Destroy(config);yield return null;yield return null;}
        [UnityTest]public IEnumerator ActualTriggerRequiresConfiguredFiveTaps(){
            yield return Create();
            var trigger=UnityEngine.Object.FindFirstObjectByType<MultiTapButton>();Assert.That(trigger,Is.Not.Null);
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){button=UnityEngine.EventSystems.PointerEventData.InputButton.Left};
            for(var i=0;i<4;i++){trigger.OnPointerClick(data);yield return null;Assert.That(runtime.Access.IsPromptOpen,Is.False);}
            trigger.OnPointerClick(data);yield return null;Assert.That(runtime.Access.IsPromptOpen,Is.True);
        }
        [UnityTest]public IEnumerator NumericKeypadUnlocksOriginalPanelAndAllTabsOpen(){
            yield return Create();runtime.Open();yield return null;
            var pin=UnityEngine.Object.FindFirstObjectByType<PinEntryControl>();Assert.That(pin,Is.Not.Null);
            for(var i=0;i<4;i++)pin.PushNumber(0);yield return null;
            Assert.That(runtime.Access.IsUnlocked,Is.True);Assert.That(DreamyDebug.Instance.IsDebugPanelVisible,Is.True);
            foreach(DefaultTabs tab in Enum.GetValues(typeof(DefaultTabs))){runtime.Open(tab);yield return null;}
            Assert.That(UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).Length,Is.GreaterThan(0));
        }
        [UnityTest]public IEnumerator PassphrasePromptAndSessionRelockWork(){
            yield return Create(true);runtime.Open();yield return null;var pin=UnityEngine.Object.FindFirstObjectByType<PinEntryControl>();
            Assert.That(pin.CredentialInput.gameObject.activeSelf,Is.True);pin.CredentialInput.text="Dreamy QA secret";pin.CredentialSubmit.onClick.Invoke();yield return null;
            Assert.That(runtime.Access.IsUnlocked,Is.True);runtime.Relock();Assert.That(runtime.Access.IsUnlocked,Is.False);Assert.That(DreamyDebug.Instance.IsDebugPanelVisible,Is.False);
        }
        [UnityTest]public IEnumerator SceneChangeCancelsPinPromptAndOptionsCanReopen(){
            yield return Create();runtime.Open(DefaultTabs.Options);yield return null;
            var pin=UnityEngine.Object.FindFirstObjectByType<PinEntryControl>();Assert.That(runtime.Access.IsPromptOpen,Is.True);
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var next=UnityEngine.SceneManagement.SceneManager.CreateScene("Dreamy prompt transition");
            try{
                UnityEngine.SceneManagement.SceneManager.SetActiveScene(next);yield return null;
                Assert.That(runtime.Access.IsPromptOpen,Is.False);Assert.That(pin.CanvasGroup.blocksRaycasts,Is.False);
                runtime.Open(DefaultTabs.Options);yield return null;Assert.That(runtime.Access.IsPromptOpen,Is.True);
                for(var i=0;i<4;i++)pin.PushNumber(0);yield return null;
                Assert.That(runtime.Access.IsUnlocked,Is.True);Assert.That(DreamyDebug.Instance.IsDebugPanelVisible,Is.True);
                Assert.That(Dreamy.Debugger.Foundation.Service.ServiceManager.GetService<Dreamy.Debugger.Presentation.Services.IDebugPanelService>().ActiveTab,Is.EqualTo(DefaultTabs.Options));
            }finally{UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(next);}
        }
        [UnityTest]public IEnumerator HudTogglesDoNotDuplicateSamplers(){
            yield return Create();runtime.SetHud(HudMode.Detailed);yield return null;runtime.SetHud(HudMode.Off);runtime.SetHud(HudMode.FpsOnly);yield return null;
            Assert.That(UnityEngine.Object.FindObjectsByType<PerformanceHud>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(1));
            Assert.That(UnityEngine.Object.FindObjectsByType<Dreamy.Debugger.Presentation.Profiler.ProfilerServiceImpl>(FindObjectsSortMode.None).Length,Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator Int64OptionsRenderEditStepPreciselyAndKeepBounds(){
            yield return Create();long amount=100,large=9007199254740993L;
            using var lease=runtime.Cheats.Register(new[]{
                Dreamy.Debugger.Options.CheatOption.Value("test.amount","Test","Int64 amount",()=>amount,v=>amount=v,1,1000000),
                Dreamy.Debugger.Options.CheatOption.Value("test.large","Test","Int64 large",()=>large,v=>large=v)});
            runtime.Open(DefaultTabs.Options);yield return null;
            var pin=UnityEngine.Object.FindFirstObjectByType<PinEntryControl>();for(var i=0;i<4;i++)pin.PushNumber(0);yield return null;
            var controls=UnityEngine.Object.FindObjectsByType<Dreamy.Debugger.Presentation.UI.Controls.Data.NumberControl>(FindObjectsSortMode.None);
            var input=controls.Single(c=>c.PropertyName=="Int64 amount").NumberSpinner;
            Assert.That(input.MinValue,Is.EqualTo(1));Assert.That(input.MaxValue,Is.EqualTo(1000000));
            input.text="125";input.onEndEdit.Invoke(input.text);Assert.That(amount,Is.EqualTo(125));
            input.TryStepInt64(1);input.onEndEdit.Invoke(input.text);Assert.That(amount,Is.EqualTo(126));
            input.text="0";input.onEndEdit.Invoke(input.text);Assert.That(amount,Is.EqualTo(126));Assert.That(input.text,Is.EqualTo("126"));
            var exact=controls.Single(c=>c.PropertyName=="Int64 large").NumberSpinner;
            Assert.That(exact.text,Is.EqualTo("9007199254740993"));
            exact.TryStepInt64(1);exact.onEndEdit.Invoke(exact.text);Assert.That(large,Is.EqualTo(9007199254740994L));
            exact.text=long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);exact.onEndEdit.Invoke(exact.text);
            exact.TryStepInt64(1);exact.onEndEdit.Invoke(exact.text);Assert.That(large,Is.EqualTo(long.MaxValue));
            lease.Dispose();yield return null;
            Assert.That(runtime.Cheats.Snapshot().Any(o=>o.Id.StartsWith("test.")),Is.False);
        }
        [UnityTest]public IEnumerator SyntheticReportPersistsWithoutNetwork(){
            yield return Create();var task=runtime.QueueReportAsync("[FAKE] PlayMode report",true,true);while(!task.IsCompleted)yield return null;
            Assert.That(task.Result,Is.True);var pending=runtime.Outbox.ReadAsync();while(!pending.IsCompleted)yield return null;
            Assert.That(pending.Result.First(p=>p.Bundle.Report.Message.Contains("PlayMode report")).Bundle.ImageBytes,Is.Zero);
            Assert.That(pending.Result.Any(p=>p.Bundle.Report.IsSynthetic && p.Bundle.Report.Message.Contains("PlayMode report")),Is.True);
        }
        [UnityTest]public IEnumerator InvalidAccessConfigurationFailsClosed(){
            root=new GameObject("Invalid configuration test");root.SetActive(false);config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.Credential="invalid PIN";
            var disabled=root.AddComponent<DebuggerRuntime>();disabled.ConfigurationAsset=config;root.SetActive(true);yield return null;
            Assert.That(disabled.enabled,Is.False);Assert.That(DebuggerRuntime.Instance,Is.Null);Assert.That(disabled.Status,Does.Contain("Invalid"));
        }
        [UnityTest]public IEnumerator ReleaseOffDoesNotStartServices(){
            root=new GameObject("Release off test");root.SetActive(false);config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.Mode=DebuggerMode.Off;
            var disabled=root.AddComponent<DebuggerRuntime>();disabled.ConfigurationAsset=config;root.SetActive(true);yield return null;
            Assert.That(disabled.enabled,Is.False);Assert.That(DebuggerRuntime.Instance,Is.Null);
        }
    }
}

#endif
