using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Reporting;
using UnityEditor;
using UnityEngine;
using Theme = Dreamy.Debugger.Foundation.UI.DebuggerPalette;
namespace Dreamy.Debugger.Editor {
    public sealed class DebuggerWindow : EditorWindow {
        private static readonly Color Background=Theme.Background,Card=Theme.Surface,Accent=Theme.Accent,Muted=Theme.Muted;
        private Texture2D banner;
        private readonly string[] tabs={"Overview","Access","Options","Performance","Reports"};
        private readonly string[] pages={"General","Access & security","Cheat options","Performance HUD","Diagnostics"};
        public Rect CaptureRect {get;private set;}
        private int selected;private Vector2 scroll;private DebuggerConfiguration config;private SerializedObject serialized;
        private string status="";private FakeOutcome testOutcome;private CancellationTokenSource lifetime;
        [MenuItem("Window/Dreamy Debugger/Settings",priority=100)]
        public static void Open(){var w=GetWindow<DebuggerWindow>("Dreamy Debugger");w.minSize=new Vector2(640,560);}
        [MenuItem("Window/Dreamy Debugger/Telegram & Reports",priority=102)]
        public static void OpenReports(){Open();GetWindow<DebuggerWindow>().selected=4;}
        [MenuItem("Window/Dreamy Debugger/Cheat Panel",priority=99)]
        public static void OpenCheatPanel(){Open();GetWindow<DebuggerWindow>().selected=2;if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null)DebuggerRuntime.Instance.Open(DefaultTabs.Options);}
        [MenuItem("Tools/Dreamy Debugger/Advanced/Access settings",priority=101)]private static void Settings(){Open();GetWindow<DebuggerWindow>().selected=1;}
        private void OnEnable(){lifetime=new CancellationTokenSource();var path=AssetDatabase.FindAssets("t:DebuggerConfiguration").Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.StartsWith("Assets/")).OrderBy(p=>p.StartsWith("Assets/_Project/")?0:1).FirstOrDefault();if(path!=null)config=AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(path);EditorApplication.playModeStateChanged+=OnPlayMode;}
        private void OnDisable(){EditorApplication.playModeStateChanged-=OnPlayMode;lifetime?.Cancel();lifetime?.Dispose();}
        private void OnPlayMode(PlayModeStateChange state){Repaint();}
        private static GUIStyle Label(int size,Color color,bool bold=false)=>new GUIStyle(EditorStyles.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,normal={textColor=color},wordWrap=true};
        private static void Rule(Rect rect,Color color)=>EditorGUI.DrawRect(rect,color);
        private void OnGUI(){
            CaptureRect=new Rect(GUIUtility.GUIToScreenPoint(Vector2.zero),position.size);
            Rule(new Rect(0,0,position.width,position.height),Background);
            Rule(new Rect(0,0,position.width,96),Theme.Brand);
            if(banner==null)banner=AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dreamy.debugger/Runtime/Branding/DreamyBanner.png");
            if(banner!=null)GUI.DrawTexture(new Rect(16,4,Mathf.Min(330,position.width*.52f),88),banner,ScaleMode.ScaleToFit);
            else GUI.Label(new Rect(24,24,300,40),"DREAMY",Label(30,Theme.Ivory,true));
            GUI.Label(new Rect(position.width-218,32,194,28),"DEBUGGER",Label(20,Theme.Ivory,true));
            var tabWidth=position.width/tabs.Length;
            for(var i=0;i<tabs.Length;i++){
                var rect=new Rect(i*tabWidth,96,tabWidth,38);
                Rule(rect,i==selected?Theme.Selected:Theme.Surface);
                if(i==selected)Rule(new Rect(rect.x,132,rect.width,2),Accent);
                var style=Label(11,i==selected?Theme.Ivory:Muted,i==selected);style.alignment=TextAnchor.MiddleCenter;
                if(GUI.Button(rect,tabs[i],style)){selected=i;scroll=Vector2.zero;GUI.FocusControl(null);}
            }
            GUILayout.BeginArea(new Rect(24,156,position.width-48,position.height-192));
            GUILayout.Label(pages[selected],Label(25,Color.white,true));GUILayout.Space(8);
            GUILayout.Label(Subtitle(),Label(12,Muted));GUILayout.Space(22);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(selected!=0){
                var next=(DebuggerConfiguration)EditorGUILayout.ObjectField("Project configuration",config,typeof(DebuggerConfiguration),false);
                if(next!=config){config=next;serialized=null;}
                if(config==null){EditorGUILayout.HelpBox("Create a project-owned configuration or install the editable sample.",MessageType.Info);if(GUILayout.Button("Create project configuration"))CreateConfig();}
                else{serialized=new SerializedObject(config);serialized.Update();}
                GUILayout.Space(16);
            }
            switch(selected){case 0:Overview();break;case 1:AccessPage();break;case 2:OptionsPage();break;case 3:PerformancePage();break;case 4:ReportPage();break;}
            if(serialized!=null && config!=null && selected>0 && selected<5)serialized.ApplyModifiedProperties();
            if(!string.IsNullOrEmpty(status)){GUILayout.Space(16);EditorGUILayout.HelpBox(status,MessageType.Info);}
            EditorGUILayout.EndScrollView();GUILayout.EndArea();
            Rule(new Rect(0,position.height-30,position.width,30),Theme.Surface);
            GUI.Label(new Rect(24,position.height-25,360,22),"DREAMY GAME STUDIO  /  DEBUGGER",Label(10,Muted));
            GUI.Label(new Rect(position.width-156,position.height-25,132,22),EditorApplication.isPlaying?"PLAY MODE":"EDITOR",Label(10,Muted));
        }
        private string Subtitle(){switch(selected){case 0:return "One workspace for your internal tools.";case 1:return "Deliberate access. Configurable gestures and credentials.";case 2:return "Typed controls, clear groups and explicit ownership.";case 3:return "A small HUD with a bounded cost.";case 4:return "Capture locally. Deliver when the game has room.";case 5:return "Editable project assets, maintained package runtime.";default:return "A phase is accepted only when its matching checks pass.";}}
        private void Section(string title,string description){GUILayout.Space(8);var rect=EditorGUILayout.GetControlRect(false,64);Rule(rect,Card);GUI.Label(new Rect(rect.x+16,rect.y+10,rect.width-32,24),title,Label(14,Color.white,true));GUI.Label(new Rect(rect.x+16,rect.y+35,rect.width-32,25),description,Label(11,Muted));GUILayout.Space(12);}
        private void Field(string name){if(serialized!=null){var p=serialized.FindProperty(name);if(p!=null){EditorGUILayout.PropertyField(p,true);GUILayout.Space(4);}}}
        private void Overview(){
            Section("DREAMY DEBUGGER","Console, options, profiler, system information and local-first bug reports.");
            GUILayout.Label("5 taps + 0000 by default",Label(18,Accent,true));GUILayout.Space(8);
            GUILayout.Label("The runtime overlay retains the tested SRDebugger layout and controls with Dreamy branding. Game-specific commands stay in your imported sample.",Label(12,Muted));GUILayout.Space(18);
            if(config==null && GUILayout.Button("Create starter setup",GUILayout.Height(36))){try{DebuggerAssetBuilder.InstallSample();status="Sample installed. Open its demo scene or add its bootstrap prefab to your own startup scene.";}catch(Exception e){status=e.Message;}}
            RuntimePanelActions(DefaultTabs.Console);
        }
        private void AccessPage(){Section("ACCESS POLICY","Tap count and credential are project configuration; PIN preserves leading zeroes.");Field("Mode");Field("RequiredTaps");Field("TapGapSeconds");Field("SequenceTimeoutSeconds");Field("Credential");Field("Passphrase");Field("SessionSeconds");Field("ShowCornerTrigger");EditorGUILayout.HelpBox("Release diagnostics has no cheat UI. Local credentials prevent accidental access and do not authorize server operations.",MessageType.None);}
        private void OptionsPage(){Section("GROUPED CHEAT OPTIONS","Boolean • number • enum • text • read-only • action");GUILayout.Label("Register a typed category/container with CheatRegistry.Register(...). Dispose its registration when the owner closes. The runtime panel reads the same registry and rechecks access on every mutation.",EditorStyles.wordWrappedLabel);
            RuntimePanelActions(DefaultTabs.Options);
            if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null){foreach(var option in DebuggerRuntime.Instance.Cheats.Snapshot())EditorGUILayout.LabelField(option.Category+" / "+option.DisplayName,option.IsAction?"Action":option.ValueType.Name);}
        }
        private void RuntimePanelActions(DefaultTabs tab){
            GUILayout.Space(14);
            if(EditorApplication.isPlaying){
                var runtime=DebuggerRuntime.Instance;
                if(runtime==null){EditorGUILayout.HelpBox("No active debugger runtime. Stop Play Mode and add the debugger bootstrap to your startup scene. Also check that its configuration Mode is enabled.",MessageType.Warning);return;}
                EditorGUILayout.HelpBox(runtime.Access.IsUnlocked?"Debugger unlocked. Cheat controls are in the runtime Options tab.":"Open the runtime panel, then enter your configured PIN (default 0000).",MessageType.Info);
                if(GUILayout.Button(tab==DefaultTabs.Options?"Open cheat panel in Game view":"Open runtime console in Game view",GUILayout.Height(36))){runtime.Open(tab);EditorApplication.ExecuteMenuItem("Window/General/Game");}
                return;
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var host=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DebuggerRuntime>(true)).FirstOrDefault();
            EditorGUILayout.HelpBox(host==null?"This scene has no debugger bootstrap. Add it to the startup scene, then enter Play Mode to open the cheat panel.":"Debugger bootstrap is present. Enter Play Mode, then click Open cheat panel here or tap the top-left corner 5 times and enter 0000 (default configuration).",host==null?MessageType.Warning:MessageType.Info);
            if(host==null && GUILayout.Button("Add debugger bootstrap to current scene",GUILayout.Height(36))){try{DebuggerAssetBuilder.AddBootstrapToActiveScene();status="Debugger added. Save the scene and enter Play Mode.";}catch(Exception e){status=e.Message;}}
        }
        private void PerformancePage(){Section("LIGHTWEIGHT PERFORMANCE HUD","Off / FPS / Compact / Detailed — independent of the debug panel.");foreach(var field in new[]{"InitialHud","InitialHudPosition","HudOffset","HudScale","HudDragEnabled","HudMemory","HudAudio","GoodFps","CautionFps","FpsGood","FpsCaution","FpsCritical","LowFpsWarning","LowFpsThreshold","LowFpsSustain","LowFpsCooldown"})Field(field);if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null){var current=DebuggerRuntime.Instance.Hud!=null?DebuggerRuntime.Instance.Hud.Mode:HudMode.Off;var next=(HudMode)EditorGUILayout.EnumPopup("Live HUD mode",current);if(next!=current)DebuggerRuntime.Instance.SetHud(next);}GUILayout.Label("FPS is frames / unscaled elapsed time. HUD history shows FPS; the profiler tab shows frame time, not GPU timing. Text refreshes at 4 Hz and memory at 1 Hz; Detailed adds memory history and an audio meter. Device profiling remains the acceptance source.",EditorStyles.wordWrappedLabel);}
        private void ReportPage(){Section("LOCAL-FIRST REPORTING","Async GPU readback, bounded image encoding and a durable disk queue.");foreach(var p in new[]{"AutoReportErrors","AllowScreenshots","ReportCooldownSeconds","ScreenshotLongEdge","UploadsEnabled","RelayUrl","LimitedClientKey","UploadDuringGameplay"})Field(p);
            TelegramRelaySettings.DrawSettings();
            EditorGUILayout.HelpBox("Telegram uses your configured relay. A relay response confirms durable acceptance; destination delivery is tracked separately on the relay. Gmail: pending. Configure the bot token and chat ID on the relay, then set this game's Relay URL and enable Uploads. A bot token is not a Relay URL.",MessageType.Info);
            if(config!=null && (!config.UploadsEnabled || string.IsNullOrWhiteSpace(config.RelayUrl)))EditorGUILayout.HelpBox("Sending is not configured for this asset: enable Uploads and set a running relay endpoint. Play Mode uses a snapshot; restart Play after configuration changes.",MessageType.Warning);
            if(GUILayout.Button("Open Telegram relay setup folder")){var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.dreamy.debugger/package.json");if(package!=null)EditorUtility.RevealInFinder(System.IO.Path.Combine(package.resolvedPath,"Tools~/relay"));}
            testOutcome=(FakeOutcome)EditorGUILayout.EnumPopup("Fake scenario",testOutcome);
            if(GUILayout.Button("Simulate delivery — no network"))Simulate();if(GUILayout.Button("Create + send [FAKE] screenshot report"))SendFake();
            if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null && GUILayout.Button("Upload queued reports now"))FlushReports();
            EditorGUILayout.HelpBox("No bot/mailbox secrets belong in the game. Use the reference relay. Screenshot failures fall back to text; process death cannot guarantee a new image.",MessageType.None);
        }
        private enum FakeOutcome {Success,Offline,RateLimited,ServerError,Rejected,Timeout,Canceled}
        private sealed class SimulationTransport : IReportTransport {
            public FakeOutcome Outcome;
            public async Task<DeliveryStatus> SendAsync(DiagnosticReport report,CancellationToken token){
                await Task.Delay(100,token);
                if(Outcome==FakeOutcome.Timeout)throw new TimeoutException("Synthetic timeout");
                return Outcome==FakeOutcome.Success?DeliveryStatus.Accepted:Outcome==FakeOutcome.Rejected?DeliveryStatus.Rejected:DeliveryStatus.Retry;
            }
        }
        private async void Simulate(){
            using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token)){
                var owner=lifetime;var fake=new SimulationTransport{Outcome=testOutcome};
                var task=new ReportDispatcher(fake).SendAsync(new DiagnosticReport("[FAKE] Editor scenario","editor",true),cancel.Token);
                if(testOutcome==FakeOutcome.Canceled)cancel.Cancel();
                var result=await task;if(this!=null && ReferenceEquals(owner,lifetime)){status="[FAKE] "+result+" — no network request";Repaint();}
            }
        }
        private async void FlushReports(){var token=lifetime.Token;await DebuggerRuntime.Instance.FlushPendingAsync();if(this!=null&&!token.IsCancellationRequested){status="Upload tick completed; relay acceptance differs from destination delivery.";Repaint();}}
        private async void SendFake(){if(!EditorApplication.isPlaying || DebuggerRuntime.Instance==null){status="Enter Play Mode with an active debugger first.";return;}var token=lifetime.Token;var runtime=DebuggerRuntime.Instance;var ok=await runtime.CreateAndSendFakeReportAsync();if(this!=null && !token.IsCancellationRequested){status=ok?runtime.Status:"Report not saved.";Repaint();}}
        private void CreateConfig(){var path=EditorUtility.SaveFilePanelInProject("Create debugger configuration","DebuggerConfiguration","asset","Project-owned debugger configuration");if(string.IsNullOrEmpty(path))return;config=CreateInstance<DebuggerConfiguration>();AssetDatabase.CreateAsset(config,path);Selection.activeObject=config;}
    }
}
