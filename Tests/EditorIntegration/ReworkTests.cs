using System;
using System.Linq;
using System.ComponentModel;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Metrics;
using Dreamy.Debugger.Presentation;
using NUnit.Framework;
namespace Dreamy.Debugger.Tests {
    public sealed class ReworkTests {
        private class Fixture {
            [System.ComponentModel.Category("Resources"),DisplayName("Coins"),NumberRange(1,1000),Increment(10),Sort(3)]public long Amount{get;set;}=100;
            [Browsable(false)]public int Hidden=>42;
            public bool Owned{get;private set;}
            [CheatConfirmation]public void Unlock(){Owned=true;}
        }
        [Serializable] private sealed class ManifestVersion { public string version; }
        [Test]public void PublicRuntimeVersionMatchesPackageManifest(){
            var info=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.dreamy.debugger/package.json");
            var json=System.IO.File.ReadAllText(System.IO.Path.Combine(info.resolvedPath,"package.json"));
            Assert.That(UnityEngine.JsonUtility.FromJson<ManifestVersion>(json).version,Is.EqualTo(DreamyDebug.Version));
        }
        [Test]public void ProfilerOnlyAdvertisesMeasuredFrameSeries(){
            var root=UnityEditor.PrefabUtility.LoadPrefabContents("Packages/com.dreamy.debugger/Runtime/Imported/Presentation/Assets/DreamyDebugger/UI/Prefabs/Tabs/Profiler.prefab");
            try{
                var labels=root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                Assert.That(labels.Where(t=>t.text=="Render"||t.text=="Update").All(t=>!t.gameObject.activeInHierarchy),Is.True);
                Assert.That(labels.Any(t=>t.text=="Frame time"),Is.True);
            }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
        }
        [Test]public void MaintainedPanelHasExactlyOneCornerLogoAndNoAuthorTab(){
            const string path="Packages/com.dreamy.debugger/Runtime/Imported/Presentation/Assets/DreamyDebugger/UI/Prefabs/DebugPanel.prefab";
            var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
            try{
                foreach(var node in root.GetComponentsInChildren<UnityEngine.Transform>(true)){
                    if(node.name=="Logo" || node.name=="DreamyBanner" || node.name=="DreamyBrand")
                        Assert.That(node.gameObject.activeSelf,Is.False,node.name);
                }
                var corner=root.GetComponentsInChildren<UnityEngine.Transform>(true).Where(t=>t.name=="DreamyCornerLogo").ToArray();
                Assert.That(corner.Length,Is.EqualTo(1));Assert.That(corner[0].gameObject.activeSelf,Is.True);
                var logo=new UnityEditor.SerializedObject(corner[0].GetComponent("Image"));
                Assert.That(logo.FindProperty("m_Sprite").objectReferenceValue,Is.Not.Null);
                Assert.That(logo.FindProperty("m_RaycastTarget").boolValue,Is.False);
                var controller=root.GetComponentInChildren<Dreamy.Debugger.Presentation.Scripts.DebuggerTabController>(true);
                Assert.That(controller.AboutTab,Is.Null);
                var header=root.transform.Find("Canvas/Content/SideBar/Content/Header").GetComponent("Image");
                var color=new UnityEditor.SerializedObject(header).FindProperty("m_Color").colorValue;
                Assert.That(color,Is.EqualTo(Dreamy.Debugger.Foundation.UI.DebuggerPalette.Brand));
            }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
        }
        [Test]public void CategoryUsesFlowInsteadOfForcingEveryControlToOneRow(){
            const string path="Packages/com.dreamy.debugger/Runtime/Imported/Presentation/UI/Prefabs/Options/Category.prefab";
            var root=UnityEditor.PrefabUtility.LoadPrefabContents(path);
            try{
                var category=root.GetComponent<Dreamy.Debugger.Presentation.UI.Other.CategoryGroup>();
                Assert.That(category.Container.GetComponent("FlowLayoutGroup"),Is.Not.Null);
                Assert.That(category.Container.GetComponent("VerticalLayoutGroup"),Is.Null);
            }finally{UnityEditor.PrefabUtility.UnloadPrefabContents(root);}
        }
        [Test]public void ReflectedAttributesGoThroughRegistryAuthorizationAndConfirmation(){var fixture=new Fixture();bool allowed=false;var registry=new CheatRegistry(()=>allowed);using(var lease=registry.Register(AttributedCheatOptions.From(fixture,"test"))){Assert.That(registry.Snapshot().Any(o=>o.Id.EndsWith("Hidden")),Is.False);Assert.Throws<UnauthorizedAccessException>(()=>registry.Set("test.Amount",200L));allowed=true;var amount=registry.Snapshot().Single(o=>o.Id=="test.Amount");Assert.That(amount.Order,Is.EqualTo(3));Assert.That(amount.Increment,Is.EqualTo(10));Assert.That(amount.Category,Is.EqualTo("Resources"));Assert.Throws<ArgumentOutOfRangeException>(()=>registry.Set("test.Amount",0L));registry.Set("test.Amount",200L);Assert.That(fixture.Amount,Is.EqualTo(200));Assert.Throws<InvalidOperationException>(()=>registry.Execute("test.Unlock"));registry.Execute("test.Unlock",true);Assert.That(fixture.Owned,Is.True);Assert.That(registry.Snapshot().Single(o=>o.Id=="test.Owned").IsReadOnly,Is.True);}Assert.That(registry.Snapshot(),Is.Empty);}
        [Test]public void BoundNumericTypesRetainTheirExactTypeAndRejectInvalidRanges(){
            foreach(var type in new[]{typeof(byte),typeof(sbyte),typeof(short),typeof(ushort),typeof(uint),typeof(int),typeof(long),typeof(float),typeof(double)}){
                object value=Convert.ChangeType(4,type);
                var registry=new CheatRegistry(()=>true);
                using(var lease=registry.Register(new[]{CheatOption.BoundValue("test.value","Types","Value",type,()=>value,v=>value=v,1,8)})){
                    registry.Set("test.value",Convert.ChangeType(7,type));
                    Assert.That(registry.Read("test.value").GetType(),Is.EqualTo(type));
                    Assert.Throws<ArgumentOutOfRangeException>(()=>registry.Set("test.value",Convert.ChangeType(9,type)));
                }
            }
            Assert.Throws<ArgumentException>(()=>CheatOption.BoundValue("test.bool","Types","Bool",typeof(bool),()=>false,null,0,1));
        }
        [Test]public void GraphyWindowUsesOneFedStreamAndRejectsInvalidSamples(){var window=new Dreamy.Debugger.Presentation.Profiler.GraphyFpsWindow();for(var i=0;i<60;i++)window.Add(.02f);Assert.That(window.AverageSeconds,Is.EqualTo(.02).Within(.000001));Assert.That(window.AverageFPS,Is.EqualTo(50));Assert.That(window.OnePercentFPS,Is.EqualTo(50));window.Add(float.NaN);window.Add(0);Assert.That(window.Count,Is.EqualTo(60));}
        [Test]public void FpsRuleRequiresSustainRecoveryAndCooldown(){var rule=new FpsThresholdRule(30,3,60);Assert.That(rule.Step(1,10),Is.False);Assert.That(rule.Step(5,20),Is.False);Assert.That(rule.Step(8,20),Is.True);Assert.That(rule.Step(9,20),Is.False);Assert.That(rule.Step(10,40),Is.False);Assert.That(rule.Step(12,40),Is.False);Assert.That(rule.Step(13,20),Is.False);Assert.That(rule.Step(16,20),Is.False);Assert.That(rule.Step(69,20),Is.True);}
    }
}
