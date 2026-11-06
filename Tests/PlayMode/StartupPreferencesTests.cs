#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Dreamy.Debugger.Preferences;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Foundation.Service;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace Dreamy.Debugger.Tests
{
    public sealed class StartupPreferencesTests
    {
        private sealed class MemoryStore : IPreferenceStorage
        {
            public string Json; public int Writes;
            public TaskCompletionSource<string[]> Pending;
            public Task<string[]> ReadAsync()=>Pending!=null?Pending.Task:Task.FromResult(new[]{Json});
            public Task WriteAsync(string json){Json=json;Writes++;return Task.CompletedTask;}
        }
        private DebuggerConfiguration config;
        private GameObject root;
        private Func<DebuggerStartupAssets> provider;
        private DebuggerStartupAssets assets;private DebuggerConfiguration defaults;
        private IEnumerator Create(MemoryStore store)
        {
            root=new GameObject("Preferences test");root.SetActive(false);
            config=ScriptableObject.CreateInstance<DebuggerConfiguration>();
            var runtime=root.AddComponent<DebuggerRuntime>();runtime.ConfigurationAsset=config;runtime.PreferenceStorage=store;
            runtime.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");root.SetActive(true);yield return null;
        }
        [UnitySetUp]public IEnumerator SetUp(){provider=DebuggerBootstrap.AssetProvider;assets=ScriptableObject.CreateInstance<DebuggerStartupAssets>();defaults=ScriptableObject.CreateInstance<DebuggerConfiguration>();defaults.PersistRuntimePreferences=false;assets.Configuration=defaults;assets.Catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>("Packages/com.dreamy.debugger/Runtime/Prefabs/DebuggerAssets.asset");DebuggerBootstrap.AssetProvider=()=>assets;if(DebuggerRuntime.Instance!=null)Object.Destroy(DebuggerRuntime.Instance.gameObject);yield return null;yield return null;}
        [UnityTearDown]public IEnumerator TearDown(){DebuggerBootstrap.AssetProvider=provider;Object.Destroy(assets);Object.Destroy(defaults);if(root!=null)Object.Destroy(root);if(DebuggerRuntime.Instance!=null)Object.Destroy(DebuggerRuntime.Instance.gameObject);if(config!=null)Object.Destroy(config);yield return null;yield return null;}
        [UnityTest]public IEnumerator DefaultsNeedNoSceneHostAndManualInitializeIsIdempotent()
        {
            var runtime=DebuggerBootstrap.Initialize();Assert.That(runtime,Is.Not.Null);Assert.That(runtime.Catalog,Is.Not.Null);
            Assert.That(DebuggerBootstrap.Initialize(),Is.SameAs(runtime));Assert.That(runtime.ConfigurationAsset.RequiredTaps,Is.EqualTo(5));
            Assert.That(runtime.Hud==null || runtime.Hud.Mode==HudMode.Off,Is.True);
            runtime.Open(DefaultTabs.Options);yield return null;Assert.That(runtime.Access.IsPromptOpen,Is.True);
        }
        [UnityTest]public IEnumerator AuthoredInactiveAndOffHostsPreventFallback()
        {
            root=new GameObject("Inactive authored host");root.SetActive(false);root.AddComponent<DebuggerRuntime>();
            Assert.That(DebuggerBootstrap.Initialize(),Is.Null);root.SetActive(true);yield return null;
            Object.Destroy(root);yield return null;config=ScriptableObject.CreateInstance<DebuggerConfiguration>();config.Mode=DebuggerMode.Off;
            root=new GameObject("Off authored host");root.SetActive(false);root.AddComponent<DebuggerRuntime>().ConfigurationAsset=config;root.SetActive(true);
            Assert.That(DebuggerBootstrap.Initialize(),Is.Null);Assert.That(root,Is.Not.Null);
        }
        [UnityTest]public IEnumerator LateAuthoredHostTakesOverAutomaticOwnerWithoutDestroyingUserRoot()
        {
            var method=typeof(DebuggerBootstrap).GetMethod("AutomaticStart",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);method.Invoke(null,null);
            var old=DebuggerRuntime.Instance;Assert.That(old,Is.Not.Null);old.SetHud(HudMode.Compact);
            yield return Create(new MemoryStore());var runtime=DebuggerRuntime.Instance;
            Assert.That(runtime,Is.Not.SameAs(old));Assert.That(runtime.gameObject,Is.SameAs(root));Assert.That(runtime.Hud.Mode,Is.EqualTo(HudMode.Compact));
            yield return null;Assert.That(Object.FindObjectsByType<PerformanceHud>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length,Is.EqualTo(1));
            runtime.Open(DefaultTabs.Options);yield return null;Assert.That(runtime.Access.IsPromptOpen,Is.True);
            var duplicate=new GameObject("User duplicate");duplicate.AddComponent<DebuggerRuntime>();Assert.That(duplicate,Is.Not.Null);Assert.That(duplicate.GetComponent<DebuggerRuntime>().enabled,Is.False);Object.Destroy(duplicate);
        }
        [UnityTest]public IEnumerator UserChoiceWinsDelayedLoadAndResetCannotResurrectPins()
        {
            var store=new MemoryStore{Pending=new TaskCompletionSource<string[]>()};yield return Create(store);var runtime=DebuggerRuntime.Instance;
            Assert.That(runtime.Hud,Is.Null);runtime.SetHud(HudMode.Compact);Assert.That(runtime.Hud.Mode,Is.EqualTo(HudMode.Compact));
            runtime.ResetLocalPreferences();store.Pending.SetResult(new[]{"{\"schema\":1,\"entries\":[{\"key\":\"hud.mode\",\"value\":\"Detailed\"},{\"key\":\"pin.game.coins\",\"value\":\"1\"}]}"});yield return null;
            Assert.That(runtime.Hud.Mode,Is.EqualTo(HudMode.Off));Assert.That(runtime.Preferences.Has("pin.game.coins"),Is.False);
            runtime.SetHud(HudMode.FpsOnly);yield return null;Assert.That(runtime.Hud.Mode,Is.EqualTo(HudMode.FpsOnly));
        }
        [UnityTest]public IEnumerator HudConsoleAndCategorySurviveRestartButCheatsAndSecurityDoNot()
        {
            var store=new MemoryStore();yield return Create(store);var runtime=DebuggerRuntime.Instance;
            runtime.SetHud(HudMode.Detailed);runtime.SetHudPosition(HudPosition.BottomLeft);runtime.SetHudScale(1.25f);
            ServiceManager.GetService<IConsoleFilterState>().SetConsoleFilterState(LogType.Warning,false);
            PreferenceBridge.Set("category.Economy","1");runtime.Preferences.Set("Credential","1234");runtime.Preferences.Set("coins","9000");
            var flush=runtime.FlushPreferencesAsync();while(!flush.IsCompleted)yield return null;
            Assert.That(store.Json,Does.Not.Contain("Credential").And.Not.Contain("coins"));
            Object.Destroy(root);Object.Destroy(config);yield return null;yield return null;
            yield return Create(store);runtime=DebuggerRuntime.Instance;
            Assert.That(runtime.Hud.Mode,Is.EqualTo(HudMode.Detailed));Assert.That(runtime.Hud.Position,Is.EqualTo(HudPosition.BottomLeft));
            Assert.That(ServiceManager.GetService<IConsoleFilterState>().GetConsoleFilterState(LogType.Warning),Is.False);
            Assert.That(PreferenceBridge.Get("category.Economy","0"),Is.EqualTo("1"));Assert.That(runtime.Access.IsUnlocked,Is.False);
            runtime.SetHud(HudMode.Off);flush=runtime.FlushPreferencesAsync();while(!flush.IsCompleted)yield return null;
            Assert.That(store.Json,Does.Contain("Off"));
        }
        [UnityTest]public IEnumerator CorruptPrimaryUsesBackupAndUnknownSchemaIsIgnored()
        {
            var store=new MemoryStore{Pending=new TaskCompletionSource<string[]>()};var preferences=new RuntimePreferences(store);
            store.Pending.SetResult(new[]{"broken", "{\"schema\":1,\"entries\":[{\"key\":\"hud.mode\",\"value\":\"Compact\"}]}"});
            while(!preferences.Ready.IsCompleted)yield return null;Assert.That(preferences.GetEnum("hud.mode",HudMode.Off),Is.EqualTo(HudMode.Compact));preferences.Dispose();
            using(var next=new RuntimePreferences(new MemoryStore{Json="{\"schema\":500,\"entries\":[{\"key\":\"hud.mode\",\"value\":\"Compact\"}]}"}))Assert.That(next.GetEnum("hud.mode",HudMode.Off),Is.EqualTo(HudMode.Off));
        }
        [UnityTest]public IEnumerator ManualLoadingWaitsForExplicitInitialization()
        {
            assets.Loading=DebuggerLoadingMode.Manual;
            typeof(DebuggerBootstrap).GetMethod("AutomaticStart",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,null);
            Assert.That(DebuggerRuntime.Instance,Is.Null);Assert.That(DebuggerBootstrap.Initialize(),Is.Not.Null);yield return null;
        }
        [UnityTest]public IEnumerator PendingPinsRestoreOnlyAfterUnlockAndLateRegistrationWithoutActionReplay()
        {
            var store=new MemoryStore{Json="{\"schema\":1,\"entries\":[{\"key\":\"pin.game.action\",\"value\":\"1\"}]}"};yield return Create(store);
            var runtime=DebuggerRuntime.Instance;int actions=0;
            var pins=ServiceManager.GetService<IPinnedUIService>();
            runtime.Access.RequestPrompt();runtime.Access.SubmitPin("0000");yield return null;
            using(var lease=runtime.Cheats.Register(new[]{Dreamy.Debugger.Options.CheatOption.Button("game.action","Game","Fake action",()=>actions++)})){
                yield return null;yield return null;
                var definition=ServiceManager.GetService<IOptionsService>().Options.Single(o=>o.StableId=="game.action");
                Assert.That(pins.HasPinned(definition),Is.True);Assert.That(actions,Is.Zero);
                pins.Unpin(definition);Assert.That(runtime.Preferences.Get("pin.game.action",""),Is.EqualTo("0"));
            }
        }
        [UnityTest]public IEnumerator CaptureHidingDoesNotPersistOffAndFreePositionSurvivesResize()
        {
            var store=new MemoryStore();yield return Create(store);var runtime=DebuggerRuntime.Instance;runtime.SetHud(HudMode.Detailed);
            runtime.SetHudPosition(HudPosition.Free);runtime.Preferences.Set("hud.x",.8f);runtime.Preferences.Set("hud.y",.7f);
            var hud=runtime.Hud;Assert.That(hud.NormalizedPosition.x,Is.EqualTo(.8f).Within(.001f));
            var capture=typeof(DebuggerRuntime).GetMethod("CaptureHiddenAsync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var pending=(Task<CaptureResult>)capture.Invoke(runtime,new object[]{System.Threading.CancellationToken.None});while(!pending.IsCompleted)yield return null;
            Assert.That(pending.IsFaulted,Is.False);Assert.That(hud.Mode,Is.EqualTo(HudMode.Detailed));
            var flush=runtime.FlushPreferencesAsync();while(!flush.IsCompleted)yield return null;Assert.That(store.Json,Does.Contain("Detailed").And.Not.Contain("Off"));
            var rect=(RectTransform)hud.GetComponentInChildren<UnityEngine.UI.Text>().transform.parent;
            var parent=(RectTransform)rect.parent;var old=parent.sizeDelta;parent.sizeDelta=new Vector2(240,400);yield return null;
            Assert.That(hud.NormalizedPosition.x,Is.EqualTo(.8f).Within(.001f));parent.sizeDelta=old;
        }
        [UnityTest]public IEnumerator DisabledOwnerCanRestartWithoutDuplicateServices()
        {
            yield return Create(new MemoryStore());var runtime=DebuggerRuntime.Instance;runtime.SetHud(HudMode.Compact);runtime.enabled=false;runtime.enabled=true;
            yield return null;yield return null;Assert.That(DebuggerRuntime.Instance,Is.SameAs(runtime));
            Assert.That(Object.FindObjectsByType<Dreamy.Debugger.Presentation.Profiler.ProfilerServiceImpl>(FindObjectsSortMode.None).Length,Is.LessThanOrEqualTo(1));
            runtime.Open();yield return null;Assert.That(runtime.Access.IsPromptOpen,Is.True);
        }
    }
}
#endif
