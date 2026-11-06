using System;
using System.Collections;
using System.IO;
using Dreamy.Debugger.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace Dreamy.Debugger.Tests
{
    public sealed class StartupEditorTests
    {
        [Test]public void BuildPayloadIsTemporaryAndReleaseHasNoUi()
        {
            var settings=DebuggerProjectSettings.instance;var previous=settings.Configuration;
            var config=ScriptableObject.CreateInstance<DebuggerConfiguration>();settings.Configuration=config;
            try{
                DebuggerStartupBuild.Prepare(true);
                var payload=AssetDatabase.LoadAssetAtPath<DebuggerStartupAssets>(DebuggerStartupBuild.Stage+"/Resources/DreamyDebugger/Startup.asset");
                Assert.That(payload,Is.Not.Null);Assert.That(payload.Catalog,Is.Not.Null);Assert.That(payload.Configuration,Is.Not.SameAs(config));
                DebuggerStartupBuild.Cleanup();Assert.That(Directory.Exists(DebuggerStartupBuild.Stage),Is.False);
                DebuggerStartupBuild.Prepare(false);Assert.That(Directory.Exists(DebuggerStartupBuild.Stage),Is.False);
                config.Mode=DebuggerMode.ReleaseDiagnostics;DebuggerStartupBuild.Prepare(false);
                payload=AssetDatabase.LoadAssetAtPath<DebuggerStartupAssets>(DebuggerStartupBuild.Stage+"/Resources/DreamyDebugger/Startup.asset");Assert.That(payload.Catalog,Is.Null);
                DebuggerStartupBuild.Cleanup();
                DebuggerStartupBuild.Prepare(true);DebuggerStartupBuild.Prepare(false);DebuggerStartupBuild.Cleanup(); // recover a previous interrupted stage
            }finally{DebuggerStartupBuild.Cleanup();settings.Configuration=previous;UnityEngine.Object.DestroyImmediate(config);}
        }
        [Test]public void StagingNeverDeletesAnUnownedDirectory()
        {
            Directory.CreateDirectory(DebuggerStartupBuild.Stage);File.WriteAllText(DebuggerStartupBuild.Stage+"/user.txt","keep");
            try{Assert.Throws<UnityEditor.Build.BuildFailedException>(()=>DebuggerStartupBuild.Prepare(true));Assert.That(File.ReadAllText(DebuggerStartupBuild.Stage+"/user.txt"),Is.EqualTo("keep"));}
            finally{AssetDatabase.DeleteAsset(DebuggerStartupBuild.Stage);if(Directory.Exists(DebuggerStartupBuild.Stage))Directory.Delete(DebuggerStartupBuild.Stage,true);}
        }
        [UnityTest]public IEnumerator AutomaticStartupAcrossPlayCyclesWithoutDomainOrSceneReload()
        {
            var previous=EditorSettings.enterPlayModeOptions;var enabled=EditorSettings.enterPlayModeOptionsEnabled;
            var profile=DebuggerProjectSettings.instance;var loading=profile.Loading;var config=profile.Configuration;
            profile.Loading=DebuggerLoadingMode.Automatic;profile.Configuration=null;
            EditorSettings.enterPlayModeOptionsEnabled=true;EditorSettings.enterPlayModeOptions=EnterPlayModeOptions.DisableDomainReload|EnterPlayModeOptions.DisableSceneReload;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            for(var i=0;i<2;i++){
                yield return new EnterPlayMode();
                Assert.That(DebuggerRuntime.Instance,Is.Not.Null);Assert.That(DebuggerRuntime.Instance.Catalog,Is.Not.Null);
                yield return new ExitPlayMode();
            }
            EditorSettings.enterPlayModeOptions=previous;EditorSettings.enterPlayModeOptionsEnabled=enabled;profile.Loading=loading;profile.Configuration=config;
        }
    }
}
