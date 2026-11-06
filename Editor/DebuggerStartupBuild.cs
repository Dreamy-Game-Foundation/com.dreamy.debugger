using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Dreamy.Debugger.Editor
{
    /// <summary>Only a build-scoped Resources payload can pull the debugger UI into a player.</summary>
    [InitializeOnLoad]
    public sealed class DebuggerStartupBuild : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string Stage="Assets/__DreamyDebuggerBuild";
        private const string Marker=Stage+"/.dreamy-debugger-owned";
        private static bool staged;
        public int callbackOrder=>-10000;
        static DebuggerStartupBuild()
        {
            EditorApplication.delayCall+=Recover;
            EditorApplication.update+=Recover;
        }
        private static void Recover(){if(!BuildPipeline.isBuildingPlayer && (staged || File.Exists(Marker)))Cleanup();}
        public void OnPreprocessBuild(BuildReport report)=>Prepare((report.summary.options&BuildOptions.Development)!=0);
        public void OnPostprocessBuild(BuildReport report)=>Cleanup();
        public static void Prepare(bool development)
        {
            Cleanup();
            var source=DebuggerEditorStartup.Resolve();
            if(source.Configuration.Mode==DebuggerMode.Off || !development && source.Configuration.Mode!=DebuggerMode.ReleaseDiagnostics)return;
            if(Directory.Exists(Stage) || File.Exists(Stage))throw new BuildFailedException("Debugger build staging path is occupied: "+Stage);
            Directory.CreateDirectory(Stage);File.WriteAllText(Marker,"Dreamy.Debugger.Startup/1");staged=true;
            try
            {
                Directory.CreateDirectory(Stage+"/Resources/DreamyDebugger");AssetDatabase.Refresh();
                var payload=ScriptableObject.CreateInstance<DebuggerStartupAssets>();
                payload.Loading=source.Loading;
                payload.Configuration=UnityEngine.Object.Instantiate(source.Configuration);
                payload.Configuration.hideFlags=HideFlags.None;
                payload.Configuration.name="Build configuration";
                payload.Catalog=development?source.Catalog:null;
                var path=Stage+"/Resources/DreamyDebugger/Startup.asset";
                AssetDatabase.CreateAsset(payload,path);AssetDatabase.AddObjectToAsset(payload.Configuration,payload);AssetDatabase.SaveAssets();
            }
            catch{Cleanup();throw;}
        }
        public static void Cleanup()
        {
            if(!File.Exists(Marker))return;
            if(File.ReadAllText(Marker)!="Dreamy.Debugger.Startup/1")throw new BuildFailedException("Unrecognized debugger staging marker; leaving files intact.");
            AssetDatabase.DeleteAsset(Stage);
            // Handles a failure before AssetDatabase imported the directory.
            if(Directory.Exists(Stage))Directory.Delete(Stage,true);
            if(File.Exists(Stage+".meta"))File.Delete(Stage+".meta");staged=false;
        }
    }
}
