using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Dreamy.Debugger.Editor {
    public sealed class ReleaseSceneStripper : IProcessSceneWithReport {
        public int callbackOrder=>1000;
        public void OnProcessScene(Scene scene,BuildReport report){
            if(report==null || (report.summary.options&BuildOptions.Development)!=0)return;
            foreach(var root in scene.GetRootGameObjects())foreach(var runtime in root.GetComponentsInChildren<DebuggerRuntime>(true)){
                var mode=runtime.ConfigurationAsset==null?DebuggerMode.Development:runtime.ConfigurationAsset.Mode;
                if(mode==DebuggerMode.ReleaseDiagnostics)runtime.Catalog=null;
                else if(mode!=DebuggerMode.ReleaseDiagnostics)UnityEngine.Object.DestroyImmediate(runtime);
            }
        }
    }
    public static class DebuggerBuildVerification {
        public static void RelaySmoke(){
            var endpoint=Environment.GetEnvironmentVariable("DREAMY_TEST_RELAY")??"http://127.0.0.1:18765/reports";
            var bundle=new Dreamy.Debugger.Reporting.ReportBundle(new Dreamy.Debugger.Reporting.DiagnosticReport("[FAKE] C# HTTP integration","consumer-test",true),"C# to Python relay");
            using(var transport=new Dreamy.Debugger.Reporting.RelayTransport(endpoint,"qa-test-only")){
                var first=transport.SendBundleAsync(bundle,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                var duplicate=transport.SendBundleAsync(bundle,System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText("/tmp/dreamy-relay-smoke.txt",bundle.Report.Id+"|"+first.Status+"|"+duplicate.Status);
                if(first.Status!=Dreamy.Debugger.Reporting.DeliveryStatus.Accepted || duplicate.Status!=first.Status)throw new Exception("Relay smoke failed");
            }
        }
        public static void BuildLinuxDevelopment()=>Build(BuildTarget.StandaloneLinux64,true);
        public static void BuildLinuxRelease()=>Build(BuildTarget.StandaloneLinux64,false);
        public static void BuildAndroidDevelopment()=>Build(BuildTarget.Android,true);
        public static void BuildAutomaticLinuxDevelopment()=>Build(BuildTarget.StandaloneLinux64,true,true);
        public static void BuildAutomaticLinuxRelease()=>Build(BuildTarget.StandaloneLinux64,false,true);
        public static void BuildAutomaticAndroidDevelopment()=>Build(BuildTarget.Android,true,true);
        private static void Build(BuildTarget target,bool development,bool automatic=false){
            string scenePath;
            if(automatic){
                var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
                scenePath="Assets/DebuggerAutomaticBuildScene.unity";
                UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,scenePath);
                DebuggerProjectSettings.instance.Loading=DebuggerLoadingMode.Automatic;DebuggerProjectSettings.instance.Configuration=null;
            }else{DebuggerAssetBuilder.InstallSample();}
            scenePath=automatic?"Assets/DebuggerAutomaticBuildScene.unity":DebuggerAssetBuilder.DemoFolder+"/DebuggerDemo.unity";
            var directory=Environment.GetEnvironmentVariable("DREAMY_BUILD_OUTPUT")??Path.Combine(Path.GetTempPath(),"dreamy-debugger-build-"+target+(development?"-dev":"-release"));
            Directory.CreateDirectory(directory);
            if(target==BuildTarget.Android){PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;EditorUserBuildSettings.buildAppBundle=false;}
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{scenePath},target=target,locationPathName=Path.Combine(directory,target==BuildTarget.Android?"DreamyDebugger.apk":"DreamyDebugger.x86_64"),options=development?BuildOptions.Development:BuildOptions.None});
            var content=report.packedAssets.SelectMany(a=>a.contents).Select(c=>c.sourceAssetPath).Where(p=>p.Contains("com.dreamy.debugger") && !p.EndsWith(".cs")).Distinct().ToArray();
            File.WriteAllText(Path.Combine(directory,"receipt.json"),JsonUtility.ToJson(new Receipt{result=report.summary.result.ToString(),target=target.ToString(),development=development,totalBytes=(long)report.summary.totalSize,durationSeconds=report.summary.totalTime.TotalSeconds,debuggerAssets=content},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Debugger consumer player build failed: "+report.summary.result);
        }
        [Serializable]private sealed class Receipt {public string result,target;public bool development;public long totalBytes;public double durationSeconds;public string[] debuggerAssets;}
    }
}
