using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Reporting;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Presentation.Profiler;
using Dreamy.Debugger.Foundation;
using Dreamy.Debugger.Foundation.Service;
using UnityEngine;
namespace Dreamy.Debugger {
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class DebuggerRuntime : MonoBehaviour {
        public static DebuggerRuntime Instance {get;private set;}
        [SerializeField]private DebuggerConfiguration configuration;
        [SerializeField]private DebuggerAssetCatalog catalog;
        public DebuggerConfiguration ConfigurationAsset {get=>configuration;set=>configuration=value;}
        public DebuggerAssetCatalog Catalog {get=>catalog;set=>catalog=value;}
        public AccessSession Access {get;private set;}
        public CheatRegistry Cheats {get;private set;}
        public DiskOutbox Outbox {get;private set;}
        public PerformanceHud Hud {get;private set;}
        public string Status {get;private set;}="Ready";
        public bool GameplayBusy {get;set;}=true;
        private DebuggerConfiguration runtimeConfig;
        private CancellationTokenSource lifetime;
        private RelayTransport relay;private OutboxPump pump;private OptionsPresentationBridge options;
        private CaptureCoordinator capture;
        private Task currentReport,currentUpload;
        private IDisposable builtins;
        private Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService consoleDrain;
        private readonly HashSet<string> recentErrors=new HashSet<string>(StringComparer.Ordinal);
        private string pendingError;private double nextReport,nextUpload;private int reportsThisSession;
        private bool hasPresentation,stopping;
        public bool CanCheat => hasPresentation && runtimeConfig!=null && runtimeConfig.Mode!=DebuggerMode.ReleaseDiagnostics && Access.IsUnlocked;
        private void Awake(){
            if(Instance!=null && Instance!=this){Destroy(gameObject);return;}
            var authored=configuration;
            if(authored==null){authored=ScriptableObject.CreateInstance<DebuggerConfiguration>();runtimeConfig=authored;}else runtimeConfig=Instantiate(authored);
            if(runtimeConfig.Mode==DebuggerMode.Off || runtimeConfig.Mode!=DebuggerMode.ReleaseDiagnostics && !Debug.isDebugBuild){Destroy(runtimeConfig);runtimeConfig=null;enabled=false;return;}
            AccessPolicy policy;
            try{policy=new AccessPolicy(runtimeConfig.RequiredTaps,runtimeConfig.Credential,tapGapSeconds:runtimeConfig.TapGapSeconds,sequenceSeconds:runtimeConfig.SequenceTimeoutSeconds,sessionSeconds:runtimeConfig.SessionSeconds,isPassphrase:runtimeConfig.Passphrase);}
            catch(ArgumentException){Status="Invalid access configuration; debugger disabled.";Destroy(runtimeConfig);runtimeConfig=null;enabled=false;return;}
            Instance=this;DontDestroyOnLoad(gameObject);
            lifetime=new CancellationTokenSource();
            Access=new AccessSession(policy,()=>Time.realtimeSinceStartupAsDouble);
            Cheats=new CheatRegistry(()=>CanCheat);
            Outbox=new DiskOutbox(System.IO.Path.Combine(Application.persistentDataPath,"DreamyDebugger","Reports"));
            capture=gameObject.AddComponent<CaptureCoordinator>();
            if(runtimeConfig.UploadsEnabled && !string.IsNullOrWhiteSpace(runtimeConfig.RelayUrl)){
                try{relay=new RelayTransport(runtimeConfig.RelayUrl,runtimeConfig.LimitedClientKey);pump=new OutboxPump(Outbox,relay);}
                catch(ArgumentException){Status="Relay configuration invalid; local reports remain available.";}
                catch(UriFormatException){Status="Relay configuration invalid; local reports remain available.";}
            }
            if(runtimeConfig.Mode!=DebuggerMode.ReleaseDiagnostics && catalog!=null){
                PresentationAssets.Catalog=catalog;var settings=Settings.Instance;
                settings.RequiredTapCount=runtimeConfig.RequiredTaps;settings.TapGapSeconds=runtimeConfig.TapGapSeconds;settings.SequenceTimeoutSeconds=runtimeConfig.SequenceTimeoutSeconds;settings.TriggerBehaviour=Settings.TriggerBehaviours.ConfigurableTap;
                settings.RequireCode=true;settings.EntryCode=(runtimeConfig.Passphrase?"0000":runtimeConfig.Credential).Select(c=>c-'0').ToArray();
                settings.EnableBugReporter=true;settings.EnableBugReportScreenshot=runtimeConfig.AllowScreenshots;
                if(!runtimeConfig.ShowCornerTrigger)settings.EnableTrigger=Settings.TriggerEnableModes.Off;
                AccessGate.IsUnlocked=()=>Access.IsUnlocked;AccessGate.BeginPrompt=Access.RequestPrompt;AccessGate.Validate=Access.SubmitPin;AccessGate.Passphrase=runtimeConfig.Passphrase;
                ServiceManager.RuntimeInitialize();DreamyDebug.Init();hasPresentation=true;
                options=new OptionsPresentationBridge(this,Cheats);
                consoleDrain=ServiceManager.GetService<IConsoleService>() as Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService;
                builtins=Cheats.Register(new[]{
                    CheatOption.Value("debugger.hud","Debugger","Performance HUD",()=>Hud!=null?Hud.Mode:HudMode.Off,v=>SetHud(v)),
                    CheatOption.Value("debugger.build","Debugger","Build",()=>Application.buildGUID??""),
                    CheatOption.Button("debugger.fake-report","Debugger","Create fake report",()=>{currentReport=QueueReportAsync("[FAKE] runtime acceptance report",true,false);})
                });
                DreamyDebug.Instance.SetBugReporterHandler(new OutboxReportHandler(this));
                ScreenshotBridge.Capture=()=>CaptureImageForForm();
                SetHud(runtimeConfig.InitialHud);
            }
            Application.logMessageReceivedThreaded+=OnLog;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged+=OnSceneChanged;
            currentReport=InitializeJournalAsync(lifetime.Token);
        }
        private async Task InitializeJournalAsync(CancellationToken token){
            try{
                var previous=await SessionJournal.BeginAsync(Application.persistentDataPath,token);
                if(previous)await SaveTextAsync("Previous session ended without a clean marker (not a confirmed crash).",false,token);
#if UNITY_ANDROID && !UNITY_EDITOR
                var watermarkPath=System.IO.Path.Combine(Application.persistentDataPath,"DreamyDebugger","android-exit.watermark");
                var after=await Task.Run(()=>{long value;return System.IO.File.Exists(watermarkPath)&&long.TryParse(System.IO.File.ReadAllText(watermarkPath),out value)?value:0L;},token);
                long newest;var evidence=AndroidExitEvidence.Collect(after,out newest);
                if(!string.IsNullOrEmpty(evidence) && await SaveTextAsync(evidence,false,token))await Task.Run(()=>System.IO.File.WriteAllText(watermarkPath,newest.ToString()),token);
#endif
            }catch(OperationCanceledException){}catch(Exception error){if(this!=null)Status="Journal: "+error.GetType().Name;}
        }
        private void OnLog(string message,string stack,LogType type){
            if(message!=null && message.StartsWith("[Dreamy",StringComparison.Ordinal))return;
            if(type!=LogType.Exception && type!=LogType.Error && type!=LogType.Assert)return;
            // Copy only a bounded string; no Unity calls, IO, screenshots, regular expressions or networking here.
            var text=(message??"");if(text.Length>4000)text=text.Substring(0,4000);
            var trace=stack??"";if(trace.Length>8000)trace=trace.Substring(0,8000);
            Interlocked.Exchange(ref pendingError,text+"\n"+trace);
        }
        private void Update(){
            if(stopping || runtimeConfig==null)return;
            consoleDrain?.Drain();
            var time=Time.realtimeSinceStartupAsDouble;
            if(hasPresentation && !Access.IsUnlocked && DreamyDebug.Instance.IsDebugPanelVisible)DreamyDebug.Instance.HideDebugPanel();
            if(runtimeConfig.AutoReportErrors && time>=nextReport && reportsThisSession<10 && (currentReport==null || currentReport.IsCompleted)){
                var error=Interlocked.Exchange(ref pendingError,null);
                if(error!=null && recentErrors.Add(error)){nextReport=time+runtimeConfig.ReportCooldownSeconds;reportsThisSession++;currentReport=QueueReportAsync(error,false,runtimeConfig.AllowScreenshots);}
            }
            if(pump!=null && time>=nextUpload && Application.isFocused && (!GameplayBusy || runtimeConfig.UploadDuringGameplay) && (currentUpload==null || currentUpload.IsCompleted)){
                nextUpload=time+5;currentUpload=UploadTickAsync(lifetime.Token);
            }
        }
        public Task FlushPendingAsync()=>lifetime==null?Task.CompletedTask:UploadTickAsync(lifetime.Token);
        private async Task UploadTickAsync(CancellationToken token){try{await pump.TickAsync(DateTime.UtcNow,token);}catch(OperationCanceledException){}catch(Exception e){if(this!=null)Status="Upload deferred: "+e.GetType().Name;}}
        public void SetHud(HudMode mode){
            if(!hasPresentation)return;
            if(Hud==null && mode!=HudMode.Off){var prefab=PresentationAssets.Load<GameObject>("DreamyDebugger/PerformanceHud");if(prefab!=null){var go=Instantiate(prefab,transform);Hud=go.GetComponent<PerformanceHud>();}}
            ProfilerServiceImpl.CollectForHud=mode!=HudMode.Off;
            if(Hud!=null)Hud.SetMode(mode);
        }
        public void Open(DefaultTabs tab=DefaultTabs.Console){if(hasPresentation)DreamyDebug.Instance.ShowDebugPanel(tab);}
        public void Relock(){
            if(hasPresentation && ServiceManager.HasService<IPinEntryService>())ServiceManager.GetService<IPinEntryService>().CancelPinEntry();
            Access?.Relock();if(hasPresentation)DreamyDebug.Instance.HideDebugPanel();
        }
        private void OnApplicationFocus(bool focused){if(!focused)Relock();}
        private void OnApplicationPause(bool paused){if(paused)Relock();}
        private void OnSceneChanged(UnityEngine.SceneManagement.Scene a,UnityEngine.SceneManagement.Scene b){Relock();}
        public void Confirm(string text,Action accepted){
            if(!CanCheat)return;var prefab=PresentationAssets.Load<GameObject>("DreamyDebugger/ConfirmationPrompt");if(prefab==null){Status="Confirmation prefab unavailable";return;}
            Instantiate(prefab,transform).GetComponent<ConfirmationPrompt>().Configure(text,()=>{if(CanCheat)accepted();});
        }
        public async Task<bool> QueueReportAsync(string description,bool synthetic=false,bool screenshot=false){
            if(lifetime==null || stopping)return false;
            var token=lifetime.Token;
            try{
                var context=ContextSnapshot();CaptureResult image=CaptureResult.WithoutImage(screenshot?"capture disabled by policy":"not requested");
                if(screenshot && runtimeConfig.AllowScreenshots)image=await CaptureHiddenAsync(token);
                return await PersistAsync(description,synthetic,context,image,token);
            }catch(OperationCanceledException){return false;}catch(Exception e){if(this!=null)Status="Report deferred: "+e.GetType().Name;return false;}
        }
        private async Task<bool> PersistAsync(string description,bool synthetic,string context,CaptureResult image,CancellationToken token){
            var report=new DiagnosticReport(ReportRedactor.Clean(description),Application.buildGUID??"",synthetic);
            var bundle=new ReportBundle(report,context,image.Jpeg,image.Reason,image.Jpeg==null?(DateTime?)null:image.CapturedUtc);
            var saved=await Outbox.EnqueueAsync(bundle,token);if(this!=null)Status=saved?"Saved locally • delivery follows upload policy":"Outbox limit reached";return saved;
        }
        private Task<bool> SaveTextAsync(string description,bool synthetic,CancellationToken token)=>PersistAsync(description,synthetic,ContextSnapshot(),CaptureResult.WithoutImage("text-only diagnostics"),token);
        private string ContextSnapshot(){
            var b=new StringBuilder();b.AppendLine("app="+Application.identifier).AppendLine("version="+Application.version).AppendLine("unity="+Application.unityVersion).AppendLine("platform="+Application.platform).AppendLine("device="+SystemInfo.deviceModel).AppendLine("os="+SystemInfo.operatingSystem).AppendLine("scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            if(hasPresentation){var console=ServiceManager.GetService<IConsoleService>();foreach(var entry in console.AllEntries.Skip(Math.Max(0,console.AllEntries.Count-100)))b.AppendLine(entry.LogType+": "+entry.Message);}
            return ReportRedactor.Clean(b.ToString(),240000);
        }
        private async Task<CaptureResult> CaptureHiddenAsync(CancellationToken token){
            var wasOpen=hasPresentation && DreamyDebug.Instance.IsDebugPanelVisible;var hudMode=Hud!=null?Hud.Mode:HudMode.Off;
            if(wasOpen)DreamyDebug.Instance.HideDebugPanel();if(Hud!=null)Hud.gameObject.SetActive(false);
            try{return await capture.CaptureAsync(runtimeConfig.ScreenshotLongEdge,token);}
            finally{if(this!=null && !stopping){if(Hud!=null)Hud.SetMode(hudMode);if(wasOpen && Access.IsUnlocked)DreamyDebug.Instance.ShowDebugPanel(false);}}
        }
        private async Task<byte[]> CaptureImageForForm(){var result=await CaptureHiddenAsync(lifetime.Token);return result.Jpeg;}
        private void OnApplicationQuit(){_ = SessionJournal.EndAsync(Application.persistentDataPath);}
        private void OnDestroy(){
            if(Instance!=this)return;stopping=true;
            Application.logMessageReceivedThreaded-=OnLog;UnityEngine.SceneManagement.SceneManager.activeSceneChanged-=OnSceneChanged;
            if(hasPresentation){DreamyDebug.Instance.HideDebugPanel();DreamyDebug.Instance.DestroyDebugPanel();}
            options?.Dispose();builtins?.Dispose();Access?.Relock();lifetime?.Cancel();relay?.Dispose();lifetime?.Dispose();
            if(hasPresentation){var console=ServiceManager.GetService<IConsoleService>() as IDisposable;console?.Dispose();var parent=Hierarchy.Get("DreamyDebugger");if(parent!=null)Destroy(parent.gameObject);}
            if(hasPresentation)ServiceManager.Shutdown();
            AccessGate.Clear();ScreenshotBridge.Capture=null;PresentationAssets.Catalog=null;ProfilerServiceImpl.CollectForHud=false;Instance=null;
            if(runtimeConfig!=null)Destroy(runtimeConfig);
        }
        private sealed class OutboxReportHandler : IBugReporterHandler {
            private readonly DebuggerRuntime owner;public OutboxReportHandler(DebuggerRuntime owner){this.owner=owner;}
            public bool IsUsable=>owner!=null;
            public async void Submit(BugReport report,Action<BugReportSubmitResult> complete,IProgress<float> progress){
                try{progress?.Report(.1f);var result=await owner.PersistAsync(report.UserDescription,false,owner.ContextSnapshot(),new CaptureResult{Jpeg=report.ScreenshotData,Reason=report.ScreenshotData==null?"not captured":"captured",CapturedUtc=DateTime.UtcNow},owner.lifetime.Token);progress?.Report(1);if(owner!=null)complete(result?BugReportSubmitResult.Success:BugReportSubmitResult.Error("Outbox full"));}
                catch(OperationCanceledException){}catch(Exception error){if(owner!=null)complete(BugReportSubmitResult.Error(error.GetType().Name));}
            }
        }
    }
}
