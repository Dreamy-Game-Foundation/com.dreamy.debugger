using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Options;
using Dreamy.Debugger.Reporting;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Presentation.Services;
using Dreamy.Debugger.Presentation.Profiler;
using Dreamy.Debugger.Presentation.UI.Tabs;
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
        public ScreenshotMode Screenshots {get;private set;}
        public string Status {get;private set;}="Ready";
        public bool GameplayBusy {get;set;}=true;
        private DebuggerConfiguration runtimeConfig;
        private CancellationTokenSource lifetime;
        private RelayTransport relay;private OutboxPump pump;private OptionsPresentationBridge options;
        private CaptureCoordinator capture;
        internal bool IsReportCaptureBusy => capture != null && capture.IsBusy;
        private ConfirmationPrompt confirmation;
        private Task currentReport,currentUpload;
        private Task<bool> manualReport;
        public string LastManualReportId {get;private set;}
        private IDisposable builtins;
        private Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService consoleDrain;
        private sealed class Incident
        {
            internal string Message, Stack, Scene;
            internal LogType Type;
            internal DateTime First, Last;
            internal int Count=1;
            internal double ReadyAt;
            internal int SceneHandle;
        }
        private static readonly Regex VariableLogValues = new Regex(@"\b(?:0x[0-9a-fA-F]+|\d+)\b", RegexOptions.CultureInvariant);
        private readonly Queue<Incident> pendingErrors=new Queue<Incident>(), incidents=new Queue<Incident>();
        private readonly Dictionary<string,Incident> groupedErrors=new Dictionary<string,Incident>(StringComparer.Ordinal);
        private readonly object errorsLock=new object();
        private CancellationTokenSource automaticReporting;
        private bool acceptsErrors;
        public bool AutoReportingEnabled => automaticReporting!=null;
        public bool CanChangeAutoReporting => CanCheat && runtimeConfig.AllowAutoReportingControls;
        public bool ReportUploadsEnabled => pump!=null && uploadsAllowed;
        public bool ReportScreenshotsEnabled => runtimeConfig!=null && runtimeConfig.AllowScreenshots;
        public Func<bool> IsSensitiveReportScreen { get; set; }
        private bool uploadsAllowed;
        private CancellationTokenSource uploadLifetime;
        private double fpsRuleStarted;private Dreamy.Debugger.Metrics.FpsThresholdRule fpsRule;public int DroppedErrors {get;private set;}
private double nextReport,nextUpload;private int reportsThisSession;
        private GameObject presentationRoot;
        private int serviceGeneration;
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
            reportsThisSession=DroppedErrors=0;nextReport=nextUpload=0;fpsRule=null;LastManualReportId=null;

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
            capture.SensitiveScreen=()=>IsSensitiveReportScreen?.Invoke()==true;
            if(runtimeConfig.UploadsEnabled && !string.IsNullOrWhiteSpace(runtimeConfig.RelayUrl)){
                try{relay=new RelayTransport(runtimeConfig.RelayUrl,runtimeConfig.LimitedClientKey);pump=new OutboxPump(Outbox,relay,true);}
                catch(ArgumentException){Status="Relay configuration invalid; local reports remain available.";}
                catch(UriFormatException){Status="Relay configuration invalid; local reports remain available.";}
            }
            if(runtimeConfig.Mode!=DebuggerMode.ReleaseDiagnostics && catalog!=null){
                PresentationAssets.Catalog=catalog;ObjectFactory.Created=PresentationAssets.ApplySkin;var settings=Settings.Instance;
                settings.TriggerPosition=runtimeConfig.TapPosition;
                settings.RequiredTapCount=runtimeConfig.RequiredTaps;settings.TapGapSeconds=runtimeConfig.TapGapSeconds;settings.SequenceTimeoutSeconds=runtimeConfig.SequenceTimeoutSeconds;settings.TriggerBehaviour=Settings.TriggerBehaviours.ConfigurableTap;
                settings.RequireCode=true;settings.EntryCode=(runtimeConfig.Passphrase?"0000":runtimeConfig.Credential).Select(c=>c-'0').ToArray();
                settings.EnableBugReporter=true;settings.EnableBugReportScreenshot=runtimeConfig.AllowScreenshots;
                if(!runtimeConfig.ShowCornerTrigger)settings.EnableTrigger=Settings.TriggerEnableModes.Off;
                AccessGate.IsUnlocked=()=>Access.IsUnlocked;AccessGate.BeginPrompt=Access.RequestPrompt;AccessGate.Validate=Access.SubmitPin;AccessGate.Passphrase=runtimeConfig.Passphrase;
                ServiceManager.RuntimeInitialize();DreamyDebug.Init();hasPresentation=true;presentationRoot=Hierarchy.Get("DreamyDebugger").gameObject;
                Screenshots=GetComponent<ScreenshotMode>();
                if(Screenshots==null)Screenshots=gameObject.AddComponent<ScreenshotMode>();
                Screenshots.Configure(this,()=>new[]{presentationRoot,Hud!=null?Hud.gameObject:null,confirmation!=null?confirmation.gameObject:null,Screenshots!=null?Screenshots.OverlayRoot:null,Screenshots!=null?Screenshots.HoldFeedbackRoot:null});
                Screenshots.RestoreKey=runtimeConfig.ScreenshotRestoreKey;
                Screenshots.CaptureKey=runtimeConfig.ScreenshotCaptureKey;
                if (!Screenshots.TrySetOutputSubdirectory(runtimeConfig.ScreenshotOutputSubdirectory))
                    Status = "Invalid screenshot folder; using the default.";
                Screenshots.RestoreTouchHoldSeconds=runtimeConfig.ScreenshotRestoreTouchHoldSeconds;
                options=new OptionsPresentationBridge(this,Cheats);
                consoleDrain=ServiceManager.GetService<IConsoleService>() as Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService;
                builtins=Cheats.Register(new[]{
                    CheatOption.Button("debugger.preferences-reset","Debugger","Reset local preferences",ResetLocalPreferences),
                    CheatOption.Value("debugger.build","Debugger","Build",()=>Application.buildGUID??""),
                });
                DreamyDebug.Instance.SetBugReporterHandler(new OutboxReportHandler(this));
                ScreenshotBridge.Capture=()=>CaptureImageForForm();
                BugReportTabController.GetControls=()=>CanCheat?new BugReportTabController.Controls
                {
                    Read=ListReportsAsync,
                    Export=ExportReportAsync,
                    Delete=DeleteReportAsync,
                    Send=SendPendingReportAsync,
                    OpenExport=OpenReportExportAsync,
                    AutoEnabled=()=>AutoReportingEnabled,
                    SetAuto=CanChangeAutoReporting ? TrySetAutoReporting : null,
                    UploadEnabled=()=>ReportUploadsEnabled
                }:null;
                ApplyPreferences(null);
            }
            SetAutoReporting(runtimeConfig.AutoReportErrors);
            SetReportUploadPermission(pump!=null);
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
            lock(errorsLock)
            {
                if(!acceptsErrors)return;
                if(pendingErrors.Count>=32){DroppedErrors++;return;}
                var utc=DateTime.UtcNow;
                pendingErrors.Enqueue(new Incident{Message=Bound(message,4000),Stack=Bound(stack,8000),Type=type,First=utc,Last=utc});
            }
        }

        private static string Bound(string value,int size)=>value==null?"":value.Substring(0,Math.Min(value.Length,size));

        public bool TrySetAutoReporting(bool enabled)
        {
            if(!CanChangeAutoReporting)return false;
            SetAutoReporting(enabled);
            return true;
        }

        private void SetAutoReporting(bool enabled)
        {
            if(enabled==AutoReportingEnabled)return;
            lock(errorsLock){acceptsErrors=enabled;pendingErrors.Clear();}
            if(enabled)
            {
                automaticReporting=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                Application.logMessageReceivedThreaded+=OnLog;
                return;
            }
            Application.logMessageReceivedThreaded-=OnLog;
            automaticReporting.Cancel();
            automaticReporting.Dispose();
            automaticReporting=null;
            incidents.Clear();
            groupedErrors.Clear();
        }

        private void DrainIncidents(double time)
        {
            for(var index=0;index<8;index++)
            {
                Incident incident;
                lock(errorsLock)
                {
                    if(pendingErrors.Count==0)break;
                    incident=pendingErrors.Dequeue();
                }
                var key=Fingerprint(incident);
                if(groupedErrors.TryGetValue(key,out var grouped))
                {
                    grouped.Count++;grouped.Last=incident.Last;
                }
                else if(incidents.Count<32)
                {
                    incident.ReadyAt=time+.25;
                    var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                    incident.SceneHandle=scene.handle;
                    incident.Scene=scene.path;
                    incidents.Enqueue(incident);groupedErrors.Add(key,incident);
                }
                else lock(errorsLock){DroppedErrors++;}
            }
            if(incidents.Count==0 || time<nextReport || reportsThisSession>=10 ||
               currentReport!=null && !currentReport.IsCompleted || incidents.Peek().ReadyAt>time)return;
            var selected=incidents.Dequeue();
            groupedErrors.Remove(Fingerprint(selected));
            nextReport=time+runtimeConfig.ReportCooldownSeconds;
            reportsThisSession++;
            var sameScene=selected.SceneHandle==UnityEngine.SceneManagement.SceneManager.GetActiveScene().handle;
            currentReport=SaveReportAsync(selected.Message,false,runtimeConfig.AllowScreenshots && sameScene,false,
                automaticReporting.Token,CreateDetails("auto",selected),sameScene?"not requested":"scene changed",selected.Scene);
        }

        private static string Fingerprint(Incident incident)
        {
            var end=incident.Stack.IndexOf('\n');
            var frame=end<0 ? incident.Stack : incident.Stack.Substring(0,end);
            return incident.Type+"\n"+VariableLogValues.Replace(incident.Message,"#")+"\n"+VariableLogValues.Replace(frame,"#");
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
            DreamyProfilerService.CollectForReports=AutoReportingEnabled;
            if(AutoReportingEnabled)DrainIncidents(time);
            if(ReportUploadsEnabled && time>=nextUpload && Application.isFocused && (!GameplayBusy || runtimeConfig.UploadDuringGameplay) && (currentUpload==null || currentUpload.IsCompleted)){
                nextUpload=time+5;currentUpload=UploadTickAsync(uploadLifetime.Token);
            }
        }
        public Task FlushPendingAsync()=>ReportUploadsEnabled?UploadTickAsync(uploadLifetime.Token):Task.CompletedTask;
        public Task<PendingReport[]> ListReportsAsync() => CanCheat && !stopping
            ? Outbox.ReadLocalAsync(lifetime.Token) : Task.FromResult(Array.Empty<PendingReport>());
        public Task<bool> DeleteReportAsync(string reportId) => CanCheat && !stopping
            ? Outbox.DeleteAsync(reportId,lifetime.Token) : Task.FromResult(false);
        public Task SendPendingReportAsync(string reportId) => CanCheat && ReportUploadsEnabled
            ? pump.TickAsync(DateTime.UtcNow,uploadLifetime.Token,reportId) : Task.CompletedTask;
        public Task<string> ExportReportAsync(string reportId) => CanCheat && !stopping
            ? Outbox.ExportAsync(reportId,Path.Combine(Application.persistentDataPath,"DreamyDebugger","Report Exports"),lifetime.Token)
            : Task.FromResult<string>(null);
        private async Task<string> OpenReportExportAsync(string directory)
        {
            if(!CanCheat || stopping)return "Report exported locally.";
            if(ScreenshotExport.CanOpenFolder)
            {
                ScreenshotExport.OpenFolder(directory);
                return "Export folder opened.";
            }
#if UNITY_ANDROID && !UNITY_EDITOR
            if(ScreenshotExport.CanSaveToGallery)
            {
                var token=lifetime.Token;
                var archive=Path.Combine(Application.temporaryCachePath,"Dreamy-"+Path.GetFileName(directory)+".zip");
                await Task.Run(()=>{
                    if(File.Exists(archive))File.Delete(archive);
                    System.IO.Compression.ZipFile.CreateFromDirectory(directory,archive);
                },token);
                try
                {
                    token.ThrowIfCancellationRequested();
                    if(CanCheat)ScreenshotExport.ShareReportArchive(archive);
                }
                finally { File.Delete(archive); }
                return "Report ZIP saved to Downloads / DreamyDebugger.";
            }
#else
            await Task.CompletedTask;
#endif
            return "Report exported to " + directory;
        }
        public bool SetReportUploadPermission(bool allowed)
        {
            if(allowed && (pump==null || stopping))return false;
            if(uploadsAllowed==allowed)return true;
            uploadsAllowed=allowed;
            if(allowed)
            {
                uploadLifetime=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                nextUpload=0;
            }
            else
            {
                uploadLifetime?.Cancel();
                uploadLifetime?.Dispose();
                uploadLifetime=null;
            }
            return true;
        }
        private async Task UploadTickAsync(CancellationToken token){try{await pump.TickAsync(DateTime.UtcNow,token);}catch(OperationCanceledException){}catch(Exception e){if(this!=null)Status="Upload deferred: "+e.GetType().Name;}}
        public void Open(DefaultTabs tab=DefaultTabs.Console){Screenshots?.RestoreUi();if(hasPresentation)DreamyDebug.Instance?.ShowDebugPanel(tab);}
        public void Relock(){
            Screenshots?.RestoreUi();
            if(hasPresentation && ServiceManager.HasService<IPinEntryService>())ServiceManager.GetService<IPinEntryService>().CancelPinEntry();
            Access?.Relock();if(hasPresentation)DreamyDebug.Instance?.HideDebugPanel();
        }
        private void OnApplicationFocus(bool focused){if(!focused){if(Screenshots==null || !Screenshots.PreserveOnFocusLoss)Relock();_ = FlushPreferencesAsync();}}
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
        public Task<bool> QueueReportAsync(string description,bool synthetic=false,bool screenshot=false,bool sendImmediately=false){
            if(lifetime==null || stopping)return Task.FromResult(false);
            return SaveReportAsync(description,synthetic,screenshot,sendImmediately,lifetime.Token,
                CreateDetails(synthetic?"synthetic":"manual"),"not requested");
        }
        private async Task<bool> SaveReportAsync(string description,bool synthetic,bool screenshot,bool sendImmediately,
            CancellationToken token,ReportDetails details,string noImageReason,string incidentScene=null){
            try{
                var context=ContextSnapshot(incidentScene);CaptureResult image=CaptureResult.WithoutImage(screenshot?"capture disabled by policy":noImageReason);
                if(screenshot && runtimeConfig.AllowScreenshots)image=await CaptureHiddenAsync(token);
                if(image.Jpeg!=null && capture.IsSensitiveScreen)image=CaptureResult.WithoutImage("sensitive screen");
                return await PersistAsync(description,synthetic,context,image,token,sendImmediately,details);
            }catch(OperationCanceledException){return false;}catch(Exception e){if(this!=null)Status="Report deferred: "+e.GetType().Name;return false;}
        }
        private async Task<bool> PersistAsync(string description,bool synthetic,string context,CaptureResult image,CancellationToken token,bool sendImmediately=false,ReportDetails details=null){
            token.ThrowIfCancellationRequested();
            var message=ReportRedactor.Clean(description);
            if(string.IsNullOrWhiteSpace(message))message=details?.Source=="auto"?"Unity "+details.Severity:"Manual report";
            var report=new DiagnosticReport(message,Application.buildGUID??"",synthetic);
            var bundle=new ReportBundle(report,context,image.Jpeg,image.Reason,image.Jpeg==null?(DateTime?)null:image.CapturedUtc,details);
            var saved=await Outbox.EnqueueAsync(bundle,token);
            if(saved && (sendImmediately || details?.Source=="manual"))LastManualReportId=report.Id;
            if(this!=null)Status=saved?"Saved locally • image: "+image.Reason:"Outbox limit reached";
            if(saved && sendImmediately && ReportUploadsEnabled){
                var upload=pump;var accepted=upload.Accepted;
                try{await upload.TickAsync(DateTime.UtcNow,uploadLifetime.Token,report.Id);}
                catch(OperationCanceledException){return true;}
                if(this!=null)Status=(upload.Accepted>accepted?"Relay accepted":"Saved locally; upload pending")+" • image: "+image.Reason;
            }
            return saved;
        }
        private ReportDetails CreateDetails(string source,Incident incident=null)
        {
            var utc=DateTime.UtcNow;
            var sampler=hasPresentation?ServiceManager.GetService<IProfilerService>() as DreamyProfilerService:null;
            var seconds=0d;
            if(Application.isFocused)
            {
                if(sampler!=null && Time.realtimeSinceStartupAsDouble-sampler.LastSampleTime<=1)
                    seconds=sampler.RecentFrames.AverageSeconds;
                if(seconds<=0)seconds=Time.unscaledDeltaTime;
            }
            return new ReportDetails(source,incident?.Type.ToString()??"",ReportRedactor.Clean(incident?.Stack,8000),
                incident?.Count??1,incident?.First??utc,incident?.Last??utc,seconds>0?1/seconds:0,
                Math.Max(0,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()),
                Math.Max(0,UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()),utc);
        }
        private Task<bool> SaveTextAsync(string description,bool synthetic,CancellationToken token)=>PersistAsync(description,synthetic,ContextSnapshot(),CaptureResult.WithoutImage("text-only diagnostics"),token);
        private string ContextSnapshot(string scene=null){
            var b=new StringBuilder();b.AppendLine("app="+Application.identifier).AppendLine("version="+Application.version).AppendLine("unity="+Application.unityVersion).AppendLine("platform="+Application.platform).AppendLine("device="+SystemInfo.deviceModel).AppendLine("os="+SystemInfo.operatingSystem).AppendLine("scene="+(scene??UnityEngine.SceneManagement.SceneManager.GetActiveScene().path));
            if(hasPresentation){var console=ServiceManager.GetService<IConsoleService>();foreach(var entry in console.AllEntries.Skip(Math.Max(0,console.AllEntries.Count-100)))b.AppendLine(entry.LogType+": "+Bound(entry.Message,600));}
            var clean=ReportRedactor.Clean(b.ToString(),62000);
            return Bound(clean,62000);
        }
        private async Task<CaptureResult> CaptureHiddenAsync(CancellationToken token){
            if (Screenshots != null && Screenshots.IsBusy) return CaptureResult.WithoutImage("capture busy");
            using(Screenshots?.HideDebuggerForReport())
                return await capture.CaptureAsync(runtimeConfig.ScreenshotLongEdge,token);
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
            SetAutoReporting(false);
            SetReportUploadPermission(false);
            sceneInspection?.Dispose();sceneInspection=null;
            if(Screenshots!=null){Screenshots.Shutdown();Screenshots=null;}
            restartPreferences=Preferences?.Export();
            Preferences?.Dispose();Preferences=null;PreferenceBridge.Clear();
            if(Hud!=null){Hud.ModeChanged=null;Hud.PositionChanged=null;Hud.DragCompleted=null;Destroy(Hud.gameObject);}
            if(confirmation!=null)Destroy(confirmation.gameObject);
            if(capture!=null){capture.enabled=false;Destroy(capture);capture=null;}

            if(ShuttingDown!=null)foreach(Action release in ShuttingDown.GetInvocationList())try{release();}catch(Exception error){Status="Cheat cleanup: "+error.GetType().Name;}
            ShuttingDown=null;
            UnityEngine.SceneManagement.SceneManager.activeSceneChanged-=OnSceneChanged;
            if(hasPresentation){var presentation=DreamyDebug.Instance;presentation?.HideDebugPanel();presentation?.DestroyDebugPanel();}
            options?.Dispose();builtins?.Dispose();Access?.Relock();lifetime?.Cancel();relay?.Dispose();lifetime?.Dispose();
            pump=null;relay=null;lifetime=null;currentUpload=null;manualReport=null;
            if(hasPresentation){var console=ServiceManager.GetService<IConsoleService>() as IDisposable;console?.Dispose();if(presentationRoot!=null){presentationRoot.SetActive(false);presentationRoot.name="DreamyDebugger (closing)";Hierarchy.RuntimeInitialize();Destroy(presentationRoot);}}
            if(hasPresentation)ServiceManager.Shutdown();
            AccessGate.Clear();ObjectFactory.Created=null;ScreenshotBridge.Capture=null;BugReportTabController.GetControls=null;PresentationAssets.Catalog=null;DreamyProfilerService.CollectForHud=false;DreamyProfilerService.CollectForRules=false;DreamyProfilerService.CollectForReports=false;Instance=null;
            if(runtimeConfig!=null)Destroy(runtimeConfig);
            if(preferenceDefaults!=null)Destroy(preferenceDefaults);runtimeConfig=null;preferenceDefaults=null;hasPresentation=false;Hud=null;
        }
        private sealed class OutboxReportHandler : IBugReporterHandler {
            private readonly DebuggerRuntime owner;public OutboxReportHandler(DebuggerRuntime owner){this.owner=owner;}
            public bool IsUsable=>owner!=null;
            public async void Submit(BugReport report,Action<BugReportSubmitResult> complete,IProgress<float> progress){
                try{
                    if(!owner.CanCheat){complete(BugReportSubmitResult.Error("Unlock the debugger first."));return;}
                    progress?.Report(.1f);
                    var data=owner.ReportScreenshotsEnabled && owner.capture!=null && !owner.capture.IsSensitiveScreen?report.ScreenshotData:null;
                    var image=new CaptureResult{Jpeg=data,Reason=data==null?"not captured":"captured",CapturedUtc=DateTime.UtcNow};
                    var result=await owner.PersistAsync(Bound(report.UserDescription,2000),false,owner.ContextSnapshot(),image,
                        owner.lifetime.Token,details:owner.CreateDetails("manual"));
                    progress?.Report(1);
                    if(owner!=null)complete(result?BugReportSubmitResult.Success:BugReportSubmitResult.Error("Could not save to local storage."));
                }
                catch(OperationCanceledException){}catch(Exception error){if(owner!=null)complete(BugReportSubmitResult.Error(error.GetType().Name));}
            }
        }
    }
}
