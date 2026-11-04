using System;
using System.IO;
using System.Linq;
using Dreamy.Debugger.Presentation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
namespace Dreamy.Debugger.Tests {
    public sealed class PackageAcceptanceTests {
        [Test]public void AllPackagePrefabsHaveValidScripts(){Assert.That(Dreamy.Debugger.Editor.DebuggerAssetBuilder.ValidatePrefabs(),Does.Contain("no missing scripts"));}
        [Test]public void CatalogResolvesEveryTabKeyWithoutResources(){var catalog=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(Dreamy.Debugger.Editor.DebuggerAssetBuilder.CatalogPath);Assert.That(catalog,Is.Not.Null);foreach(var tab in new[]{"Console","Options","Profiler","System","BugReporter"})Assert.That(catalog.Entries.Any(e=>e.Key=="DreamyDebugger/UI/Prefabs/Tabs/"+tab && e.Asset!=null),Is.True,tab);Assert.That(Directory.GetDirectories("Packages/com.dreamy.debugger/Runtime","Resources",SearchOption.AllDirectories).Length,Is.Zero);}
        [Test]public void ProjectSampleInstallKeepsCustomization(){Dreamy.Debugger.Editor.DebuggerAssetBuilder.InstallSample();var path=Dreamy.Debugger.Editor.DebuggerAssetBuilder.DemoFolder+"/DebuggerConfiguration.asset";var config=AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(path);var pin=config.Credential;try{config.Credential="00998877";EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);Dreamy.Debugger.Editor.DebuggerAssetBuilder.InstallSample();Assert.That(AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(path).Credential,Is.EqualTo("00998877"));}finally{config.Credential=pin;EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);}}
        [Test]public void PublicRuntimeHasNoSrTypeNames(){foreach(var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a=>a.GetName().Name.StartsWith("Dreamy.Debugger")&&!a.GetName().Name.Contains("Test")))Assert.That(assembly.GetTypes().Any(t=>t.Name.StartsWith("SR")),Is.False,assembly.FullName);}
    }
}
