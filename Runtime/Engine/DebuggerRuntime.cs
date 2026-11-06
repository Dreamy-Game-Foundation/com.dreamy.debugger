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
    public sealed partial class DebuggerRuntime : MonoBehaviour {
        public static DebuggerRuntime Instance {get;private set;}
        internal event Action ShuttingDown;
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
        private ConfirmationPrompt confirmation;
        private Task currentReport,currentUpload;
        private Task<bool> manualReport;
        public string LastManualReportId {get;private set;}
        private IDisposable builtins;
        private Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService consoleDrain;
        private readonly HashSet<string> recentErrors=new HashSet<string>(StringComparer.Ordinal);
        private readonly Queue<string> pendingErrors=new Queue<string>();private readonly object errorsLock=new object();
        private double fpsRuleStarted;private Dreamy.Debugger.Metrics.FpsThresholdRule fpsRule;public int DroppedErrors {get;private set;}
private double nextReport,nextUpload;private int reportsThisSession;
        private GameObject presentationRoot;
        private int serviceGeneration;
        private int captureOverlayUsers;
        private bool capturePanelWasOpen;
        private HudMode captureHudMode;
        private HudPosition sessionHudPosition;
        private bool hasPresentation,stopping;
        private Dictionary<string,string> restartPreferences;
        public bool CanCheat => hasPresentation && runtimeConfig!=null && runtimeConfig.Mode!=DebuggerMode.ReleaseDiagnostics && Access.IsUnlocked;
        internal bool IsAutomatic {get;set;}
        private void Awake()=>EnsureInitialized();
        private void OnEnable(){if(Application.isPlaying)EnsureInitialized();}
        internal void EnsureInitialized(){
            if(Instance==this && !stopping)return;
            Dictionary<string,string> seed=null;
            if(Instance!=null && Instance!=this){
                if(!IsAutomatic && Instance.IsAutomatic){var old=Instance;seed=old.Preferences?.Export();old.Shutdown();Destroy(old.gameObject);}
                else {enabled=false;return;}
            }
            stopping=false;hasPresentation=false;

            var authored=configuration;
            if(authored==null){authored=ScriptableObject.CreateInstance<DebuggerConfiguration>();runtimeConfig=authored;}else runtimeConfig=Instantiate(authored);
            if(runtimeConfig.Mode==DebuggerMode.Off || runtimeConfig.Mode!=DebuggerMode.ReleaseDiagnostics && !Debug.isDebugBuild){Destroy(runtimeConfig);runtimeConfig=null;enabled=false;return;}
            AccessPolicy policy;
            try{policy=new AccessPolicy(runtimeConfig.RequiredTaps,runtimeConfig.Credential,tapGapSeconds:runtimeConfig.TapGapSeconds,sequenceSeconds:runtimeConfig.SequenceTimeoutSeconds,sessionSeconds:runtimeConfig.SessionSeconds,isPassphrase:runtimeConfig.Passphrase);}
            catch(ArgumentException){Status="Invalid access configuration; debugger disabled.";Destroy(runtimeConfig);runtimeConfig=null;enabled=false;return;}
            sessionHudPosition=runtimeConfig.InitialHudPosition;
            Instance=this;DontDestroyOnLoad(gameObject);
            InitializePreferences(seed ?? restartPreferences);
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
                settings.TriggerPosition=runtimeConfig.TapPosition;
                settings.RequiredTapCount=runtimeConfig.RequiredTaps;settings.TapGapSeconds=runtimeConfig.TapGapSeconds;settings.SequenceTimeoutSeconds=runtimeConfig.SequenceTimeoutSeconds;settings.TriggerBehaviour=Settings.TriggerBehaviours.ConfigurableTap;
                settings.RequireCode=true;settings.EntryCode=(runtimeConfig.Passphrase?"0000":runtimeConfig.Credential).Select(c=>c-'0').ToArray();
                settings.EnableBugReporter=false;settings.EnableBugReportScreenshot=runtimeConfig.AllowScreenshots;
                if(!runtimeConfig.ShowCornerTrigger)settings.EnableTrigger=Settings.TriggerEnableModes.Off;
                AccessGate.IsUnlocked=()=>Access.IsUnlocked;AccessGate.BeginPrompt=Access.RequestPrompt;AccessGate.Validate=Access.SubmitPin;AccessGate.Passphrase=runtimeConfig.Passphrase;
                ServiceManager.RuntimeInitialize();DreamyDebug.Init();hasPresentation=true;presentationRoot=Hierarchy.Get("DreamyDebugger").gameObject;
                options=new OptionsPresentationBridge(this,Cheats);
                consoleDrain=ServiceManager.GetService<IConsoleService>() as Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService;
                builtins=Cheats.Register(new[]{
                    CheatOption.Button("debugger.preferences-reset","Debugger","Reset local preferences",ResetLocalPreferences),
                    CheatOption.Value("debugger.build","Debugger","Build",()=>Application.buildGUID??""),
                });
                DreamyDebug.Instance.SetBugReporterHandler(new OutboxReportHandler(this));
                ScreenshotBridge.Capture=()=>CaptureImageForForm();
                ApplyPreferences(null);
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
            if(message!=null && message.StartsWith("[DreamyDebugger.Report]",StringComparison.Ordinal))return;
            if(type!=LogType.Exception && type!=LogType.Error && type!=LogType.Assert)return;
            // Copy only a bounded string; no Unity calls, IO, screenshots, regular expressions or networking here.
            var text=(message??"");if(text.Length>4000)text=text.Substring(0,4000);
            var trace=stack??"";if(trace.Length>8000)trace=trace.Substring(0,8000);
            lock(errorsLock){if(pendingErrors.Count>=32){pendingErrors.Dequeue();DroppedErrors++;}pendingErrors.Enqueue(text+"\n"+trace);}
        }
        private void Update(){
            if(stopping || runtimeConfig==null)return;
            if(hasPresentation && serviceGeneration!=ServiceManager.Generation){
                serviceGeneration=ServiceManager.Generation;
                consoleDrain=ServiceManager.GetService<IConsoleService>() as Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService;
            }
            consoleDrain?.Drain();
            var time=Time.realtimeSinceStartupAsDouble;
            Preferences?.Tick(time);
            options?.Tick(time);
            DreamyProfilerService.CollectForRules=hasPresentation && runtimeConfig.LowFpsWarning;
            if(hasPresentation && runtimeConfig.LowFpsWarning){
                DreamyProfilerService.CollectForRules=true;
                if(fpsRule==null){try{fpsRuleStarted=time;fpsRule=new Dreamy.Debugger.Metrics.FpsThresholdRule(runtimeConfig.LowFpsThreshold,runtimeConfig.LowFpsSustain,runtimeConfig.LowFpsCooldown);}catch(ArgumentException){runtimeConfig.LowFpsWarning=false;}}
                var metric=ServiceManager.GetService<IProfilerService>();var seconds=metric is DreamyProfilerService sampler?(float)sampler.RecentFrames.AverageSeconds:metric!=null?metric.LastFrameTime:0;
                if(fpsRule!=null && fpsRule.Step(time-fpsRuleStarted,seconds>0?1/seconds:0,!Application.isFocused))Debug.LogWarning("[DreamyDebugger.Performance] Sustained low FPS: "+(seconds>0?1/seconds:0).ToString("0"));
            }
            var presentation=hasPresentation?DreamyDebug.Instance:null;
            if(presentation!=null && !Access.IsUnlocked && presentation.IsDebugPanelVisible)presentation.HideDebugPanel();
            if(runtimeConfig.AutoReportErrors && time>=nextReport && reportsThisSession<10 && (currentReport==null || currentReport.IsCompleted)){
                string error=null;lock(errorsLock){if(pendingErrors.Count>0)error=pendingErrors.Dequeue();}
                if(error!=null && recentErrors.Add(System.Text.RegularExpressions.Regex.Replace(error,@"\b[0-9]+\b","#"))){nextReport=time+runtimeConfig.ReportCooldownSeconds;reportsThisSession++;currentReport=QueueReportAsync(error,false,runtimeConfig.AllowScreenshots);}
            }
            if(pump!=null && time>=nextUpload && Application.isFocused && (!GameplayBusy || runtimeConfig.UploadDuringGameplay) && (currentUpload==null || currentUpload.IsCompleted)){
                nextUpload=time+5;currentUpload=UploadTickAsync(lifetime.Token);
            }
        }
        public Task FlushPendingAsync()=>lifetime==null?Task.CompletedTask:UploadTickAsync(lifetime.Token);
        private async Task UploadTickAsync(CancellationToken token){try{await pump.TickAsync(DateTime.UtcNow,token);}catch(OperationCanceledException){}catch(Exception e){if(this!=null)Status="Upload deferred: "+e.GetType().Name;}}
        public void Open(DefaultTabs tab=DefaultTabs.Console){if(hasPresentation)DreamyDebug.Instance?.ShowDebugPanel(tab);}
        public void Relock(){
            if(hasPresentation && ServiceManager.HasService<IPinEntryService>())ServiceManager.GetService<IPinEntryService>().CancelPinEntry();
            Access?.Relock();if(hasPresentation)DreamyDebug.Instance?.HideDebugPanel();
        }
        private void OnApplicationFocus(bool focused){if(!focused){Relock();_ = FlushPreferencesAsync();}}
        private void OnApplicationPause(bool paused){if(paused){Relock();_ = FlushPreferencesAsync();}}
        private void OnSceneChanged(UnityEngine.SceneManagement.Scene a,UnityEngine.SceneManagement.Scene b){Relock();}
        public void Confirm(string text,Action accepted,Func<bool> validate=null){
            if(!CanCheat || confirmation!=null)return;var prefab=PresentationAssets.Load<GameObject>("DreamyDebugger/ConfirmationPrompt");if(prefab==null){Status="Confirmation prefab unavailable";return;}
            confirmation=Instantiate(prefab,transform).GetComponent<ConfirmationPrompt>();confirmation.Configure(text,()=>{if(CanCheat)accepted();},()=>CanCheat && (validate==null || validate()));
        }
        public Task<bool> CreateAndSendFakeReportAsync(){
            if(manualReport==null || manualReport.IsCompleted)manualReport=QueueReportAsync("[FAKE] Manual gameplay screenshot report; no real crash occurred.",true,true,true);
            return manualReport;
        }
        public async Task<bool> QueueReportAsync(string description,bool synthetic=false,bool screenshot=false,bool sendImmediately=false){
            if(lifetime==null || stopping)return false;
            var token=lifetime.Token;
            try{
                var context=ContextSnapshot();CaptureResult image=CaptureResult.WithoutImage(screenshot?"capture disabled by policy":"not requested");
                if(screenshot && runtimeConfig.AllowScreenshots)image=await CaptureHiddenAsync(token);
                if(image.Jpeg!=null && capture.IsSensitiveScreen)image=CaptureResult.WithoutImage("sensitive screen");
                return await PersistAsync(description,synthetic,context,image,token,sendImmediately);
            }catch(OperationCanceledException){return false;}catch(Exception e){if(this!=null)Status="Report deferred: "+e.GetType().Name;return false;}
        }
        private async Task<bool> PersistAsync(string description,bool synthetic,string context,CaptureResult image,CancellationToken token,bool sendImmediately=false){
            var report=new DiagnosticReport(ReportRedactor.Clean(description),Application.buildGUID??"",synthetic);
            var bundle=new ReportBundle(report,context,image.Jpeg,image.Reason,image.Jpeg==null?(DateTime?)null:image.CapturedUtc);
            var saved=await Outbox.EnqueueAsync(bundle,token);
            if(sendImmediately)LastManualReportId=report.Id;
            if(this!=null)Status=saved?"Saved locally • image: "+image.Reason:"Outbox limit reached";
            if(saved && sendImmediately && pump!=null){
                var accepted=pump.Accepted;await pump.TickAsync(DateTime.UtcNow,token,report.Id);
                if(this!=null)Status=(pump.Accepted>accepted?"Relay accepted":"Saved locally; upload pending")+" • image: "+image.Reason;
            }
            return saved;
        }
        private Task<bool> SaveTextAsync(string description,bool synthetic,CancellationToken token)=>PersistAsync(description,synthetic,ContextSnapshot(),CaptureResult.WithoutImage("text-only diagnostics"),token);
        private string ContextSnapshot(){
            var b=new StringBuilder();b.AppendLine("app="+Application.identifier).AppendLine("version="+Application.version).AppendLine("unity="+Application.unityVersion).AppendLine("platform="+Application.platform).AppendLine("device="+SystemInfo.deviceModel).AppendLine("os="+SystemInfo.operatingSystem).AppendLine("scene="+UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            if(hasPresentation){var console=ServiceManager.GetService<IConsoleService>();foreach(var entry in console.AllEntries.Skip(Math.Max(0,console.AllEntries.Count-100)))b.AppendLine(entry.LogType+": "+entry.Message);}
            return ReportRedactor.Clean(b.ToString(),240000);
        }
        private async Task<CaptureResult> CaptureHiddenAsync(CancellationToken token){
            if(captureOverlayUsers++==0){
                var presentation=hasPresentation?DreamyDebug.Instance:null;
                capturePanelWasOpen=presentation!=null && presentation.IsDebugPanelVisible;
                captureHudMode=Hud!=null?Hud.Mode:HudMode.Off;
                if(capturePanelWasOpen)presentation.HideDebugPanel();if(Hud!=null)Hud.gameObject.SetActive(false);
            }
            try{return await capture.CaptureAsync(runtimeConfig.ScreenshotLongEdge,token);}
            finally{
                captureOverlayUsers--;
                if(captureOverlayUsers==0 && this!=null && !stopping){
                    ApplyHud(desiredHudMode);
                    if(capturePanelWasOpen && Access.IsUnlocked)DreamyDebug.Instance?.ShowDebugPanel(false);
                }
            }
        }
        private async Task<byte[]> CaptureImageForForm(){
            if(runtimeConfig==null || !runtimeConfig.AllowScreenshots)return null;
            var result=await CaptureHiddenAsync(lifetime.Token);return result.Jpeg;
        }
        private void OnApplicationQuit(){_ = FlushPreferencesAsync();_ = SessionJournal.EndAsync(Application.persistentDataPath);}
        private void OnDisable(){Shutdown();}
        private void OnDestroy(){Shutdown();}
        internal void Shutdown(){
            if(Instance!=this)return;stopping=true;
            restartPreferences=Preferences?.Export();
            Preferences?.Dispose();Preferences=null;PreferenceBridge.Clear();
            if(Hud!=null){Hud.ModeChanged=null;Hud.PositionChanged=null;Hud.DragCompleted=null;Destroy(Hud.gameObject);}
            if(confirmation!=null)Destroy(confirmation.gameObject);
            if(capture!=null){capture.enabled=false;Destroy(capture);capture=null;}

            if(ShuttingDown!=null)foreach(Action release in ShuttingDown.GetInvocationList())try{release();}catch(Exception error){Status="Cheat cleanup: "+error.GetType().Name;}
            ShuttingDown=null;
            Application.logMessageReceivedThreaded-=OnLog;UnityEngine.SceneManagement.SceneManager.activeSceneChanged-=OnSceneChanged;
            if(hasPresentation){var presentation=DreamyDebug.Instance;presentation?.HideDebugPanel();presentation?.DestroyDebugPanel();}
            options?.Dispose();builtins?.Dispose();Access?.Relock();lifetime?.Cancel();relay?.Dispose();lifetime?.Dispose();
            if(hasPresentation){var console=ServiceManager.GetService<IConsoleService>() as IDisposable;console?.Dispose();if(presentationRoot!=null){presentationRoot.SetActive(false);presentationRoot.name="DreamyDebugger (closing)";Hierarchy.RuntimeInitialize();Destroy(presentationRoot);}}
            if(hasPresentation)ServiceManager.Shutdown();
            AccessGate.Clear();ScreenshotBridge.Capture=null;PresentationAssets.Catalog=null;DreamyProfilerService.CollectForHud=false;DreamyProfilerService.CollectForRules=false;Instance=null;
            if(runtimeConfig!=null)Destroy(runtimeConfig);
            if(preferenceDefaults!=null)Destroy(preferenceDefaults);runtimeConfig=null;preferenceDefaults=null;hasPresentation=false;Hud=null;
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
