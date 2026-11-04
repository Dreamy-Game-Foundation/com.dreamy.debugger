using System;
using System.IO;
using System.Linq;
using Dreamy.Debugger.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Dreamy.Debugger.Tests {
    public sealed class PackageAcceptanceTests {
        [Test]public void SceneBootstrapInstallIsExplicitAndIdempotent(){
            var previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(string.IsNullOrEmpty(previous.path) && previous.isDirty)Assert.Ignore("Save the untitled scene before running scene installation tests.");
            var replaceEmpty=string.IsNullOrEmpty(previous.path);
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,replaceEmpty?UnityEditor.SceneManagement.NewSceneMode.Single:UnityEditor.SceneManagement.NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
            try{
                var first=Dreamy.Debugger.Editor.DebuggerAssetBuilder.AddBootstrapToActiveScene();
                var second=Dreamy.Debugger.Editor.DebuggerAssetBuilder.AddBootstrapToActiveScene();
                Assert.That(second,Is.SameAs(first));Assert.That(first.ConfigurationAsset,Is.Not.Null);Assert.That(first.Catalog,Is.Not.Null);
                Assert.That(first.gameObject.scene,Is.EqualTo(scene));Assert.That(scene.isDirty,Is.True);
                Assert.That(scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DebuggerRuntime>(true)).Count(),Is.EqualTo(1));
                Undo.ClearUndo(first.gameObject);
            }finally{if(replaceEmpty)UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);else{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);}}
        }
        [Test]public void AllPackagePrefabsHaveValidScripts(){Assert.That(Dreamy.Debugger.Editor.DebuggerAssetBuilder.ValidatePrefabs(),Does.Contain("no missing scripts"));}
        [Test]public void CatalogResolvesEveryTabKeyWithoutResources(){var catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(Dreamy.Debugger.Editor.DebuggerAssetBuilder.CatalogPath);Assert.That(catalog,Is.Not.Null);foreach(var tab in new[]{"Console","Options","Profiler","System","BugReporter"})Assert.That(catalog.Entries.Any(e=>e.Key=="DreamyDebugger/UI/Prefabs/Tabs/"+tab && e.Asset!=null),Is.True,tab);Assert.That(Directory.GetDirectories("Packages/com.dreamy.debugger/Runtime","Resources",SearchOption.AllDirectories).Length,Is.Zero);}
        [Test]public void ProjectSampleInstallKeepsCustomization(){Dreamy.Debugger.Editor.DebuggerAssetBuilder.InstallSample();var path=Dreamy.Debugger.Editor.DebuggerAssetBuilder.DemoFolder+"/DebuggerConfiguration.asset";var config=AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(path);var pin=config.Credential;try{config.Credential="00998877";EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);Dreamy.Debugger.Editor.DebuggerAssetBuilder.InstallSample();Assert.That(AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(path).Credential,Is.EqualTo("00998877"));}finally{config.Credential=pin;EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);}}
        [Test]public void PublicRuntimeHasNoSrTypeNames(){foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("Dreamy.Debugger")&&!a.GetName().Name.Contains("Test")))Assert.That(assembly.GetTypes().Any(t=>t.Name.StartsWith("SR")),Is.False,assembly.FullName);}
    }
}
