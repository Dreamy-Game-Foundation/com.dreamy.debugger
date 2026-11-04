using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Presentation;
using Dreamy.Debugger.Reporting;
using UnityEditor;
using UnityEngine;
namespace Dreamy.Debugger.Editor {
    public sealed class DebuggerWindow : EditorWindow {
        private static readonly Color Background=new Color(.055f,.07f,.10f),Sidebar=new Color(.075f,.09f,.13f),Card=new Color(.10f,.12f,.17f),Accent=new Color(.30f,.83f,.76f),Muted=new Color(.53f,.59f,.69f);
        private readonly string[] pages={"Overview","Access & security","Cheat options","Performance HUD","Bug reports","Theme & assets","Acceptance"};
        public Rect CaptureRect {get;private set;}
        private int selected;private Vector2 scroll;private DebuggerConfiguration config;private SerializedObject serialized;
        private string status="";private FakeOutcome testOutcome;private CancellationTokenSource lifetime;
        [MenuItem("Window/Dreamy Debugger/Control Center",priority=100)]
        public static void Open(){var w=GetWindow<DebuggerWindow>("Dreamy Debugger");w.minSize=new Vector2(780,560);}
        [MenuItem("Window/Dreamy Debugger/Cheat Panel",priority=99)]
        public static void OpenCheatPanel(){Open();GetWindow<DebuggerWindow>().selected=2;if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null)DebuggerRuntime.Instance.Open(DefaultTabs.Options);}
        [MenuItem("Window/Dreamy Debugger/Settings",priority=101)]private static void Settings(){Open();GetWindow<DebuggerWindow>().selected=1;}
        private void OnEnable(){lifetime=new CancellationTokenSource();var path=AssetDatabase.FindAssets("t:DebuggerConfiguration").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p=>p.StartsWith("Assets/"));if(path!=null)config=AssetDatabase.LoadAssetAtPath<DebuggerConfiguration>(path);EditorApplication.playModeStateChanged+=OnPlayMode;}
        private void OnDisable(){EditorApplication.playModeStateChanged-=OnPlayMode;lifetime?.Cancel();lifetime?.Dispose();}
        private void OnPlayMode(PlayModeStateChange state){Repaint();}
        private static GUIStyle Label(int size,Color color,bool bold=false)=>new GUIStyle(EditorStyles.label){fontSize=size,fontStyle=bold?FontStyle.Bold:FontStyle.Normal,normal={textColor=color},wordWrap=true};
        private static void Rule(Rect rect,Color color)=>EditorGUI.DrawRect(rect,color);
        private void OnGUI(){
            CaptureRect=new Rect(GUIUtility.GUIToScreenPoint(Vector2.zero),position.size);
            Rule(new Rect(0,0,position.width,position.height),Background);Rule(new Rect(0,0,214,position.height),Sidebar);
            GUI.Label(new Rect(24,25,176,30),"DREAMY",Label(24,Color.white,true));GUI.Label(new Rect(25,57,175,22),"DEBUGGER  /  INTERNAL",Label(10,Accent,true));
            Rule(new Rect(24,94,166,1),new Color(.18f,.22f,.28f));
            for(var i=0;i<pages.Length;i++){
                var y=116+i*44;var rect=new Rect(12,y,190,38);
                if(i==selected){Rule(rect,new Color(.12f,.22f,.26f));Rule(new Rect(12,y,3,38),Accent);}
                if(GUI.Button(rect,"",GUIStyle.none))selected=i;
                GUI.Label(new Rect(27,y+9,166,24),pages[i],Label(12,i==selected?Color.white:Muted,i==selected));
            }
            GUI.Label(new Rect(25,position.height-78,172,52),"Independent Unity toolkit\nAccess  •  Inspect  •  Diagnose",Label(10,Muted));
            GUILayout.BeginArea(new Rect(238,24,position.width-264,position.height-44));
            GUILayout.Label(pages[selected],Label(25,Color.white,true));GUILayout.Space(8);
            GUILayout.Label(Subtitle(),Label(12,Muted));GUILayout.Space(22);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(selected!=0 && selected!=5 && selected!=6){
                var next=(DebuggerConfiguration)EditorGUILayout.ObjectField("Project configuration",config,typeof(DebuggerConfiguration),false);
                if(next!=config){config=next;serialized=null;}
                if(config==null){EditorGUILayout.HelpBox("Create a project-owned configuration or install the editable sample.",MessageType.Info);if(GUILayout.Button("Create project configuration"))CreateConfig();}
                else{serialized=new SerializedObject(config);serialized.Update();}
                GUILayout.Space(16);
            }
            switch(selected){case 0:Overview();break;case 1:AccessPage();break;case 2:OptionsPage();break;case 3:PerformancePage();break;case 4:ReportPage();break;case 5:ThemePage();break;case 6:AcceptancePage();break;}
            if(serialized!=null && config!=null && selected>0 && selected<5)serialized.ApplyModifiedProperties();
            if(!string.IsNullOrEmpty(status)){GUILayout.Space(16);EditorGUILayout.HelpBox(status,MessageType.Info);}
            EditorGUILayout.EndScrollView();GUILayout.EndArea();
        }
        private string Subtitle(){switch(selected){case 0:return "One workspace for your internal tools.";case 1:return "Deliberate access. Configurable gestures and credentials.";case 2:return "Typed controls, clear groups and explicit ownership.";case 3:return "A small HUD with a bounded cost.";case 4:return "Capture locally. Deliver when the game has room.";case 5:return "Editable project assets, maintained package runtime.";default:return "A phase is accepted only when its matching checks pass.";}}
        private void Section(string title,string description){GUILayout.Space(8);var rect=EditorGUILayout.GetControlRect(false,64);Rule(rect,Card);GUI.Label(new Rect(rect.x+16,rect.y+10,rect.width-32,24),title,Label(14,Color.white,true));GUI.Label(new Rect(rect.x+16,rect.y+35,rect.width-32,25),description,Label(11,Muted));GUILayout.Space(12);}
        private void Field(string name){if(serialized!=null){var p=serialized.FindProperty(name);if(p!=null)EditorGUILayout.PropertyField(p,true);}}
        private void Overview(){
            Section("DREAMY DEBUGGER","Console, options, profiler, system information and local-first bug reports.");
            GUILayout.Label("5 taps + 0000 by default",Label(18,Accent,true));GUILayout.Space(8);
            GUILayout.Label("The runtime overlay retains the tested SRDebugger layout and controls with Dreamy branding. Game-specific commands stay in your imported sample.",Label(12,Muted));GUILayout.Space(18);
            if(GUILayout.Button("Install / update editable production sample",GUILayout.Height(36))){try{DebuggerAssetBuilder.InstallSample();status="Sample installed. Open its demo scene or add its bootstrap prefab to your own startup scene.";}catch(Exception e){status=e.Message;}}
            if(GUILayout.Button("Select package asset catalog",GUILayout.Height(30)))Selection.activeObject=AssetDatabase.LoadAssetAtPath<DebuggerAssetCatalog>(DebuggerAssetBuilder.CatalogPath);
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
            if(GUILayout.Button("Open standalone debugger demo")){if(UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()){DebuggerAssetBuilder.InstallSample();UnityEditor.SceneManagement.EditorSceneManager.OpenScene(DebuggerAssetBuilder.DemoFolder+"/DebuggerDemo.unity");}}
        }
        private void PerformancePage(){Section("LIGHTWEIGHT PERFORMANCE HUD","Off / FPS / Compact / Detailed — independent of the debug panel.");Field("InitialHud");if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null){var current=DebuggerRuntime.Instance.Hud!=null?DebuggerRuntime.Instance.Hud.Mode:HudMode.Off;var next=(HudMode)EditorGUILayout.EnumPopup("Live HUD mode",current);if(next!=current)DebuggerRuntime.Instance.SetHud(next);}GUILayout.Label("FPS is frames / unscaled elapsed time. Graphs show frame time, not GPU timing. Text refreshes at 4 Hz and memory at 1 Hz. Device profiling remains the acceptance source.",EditorStyles.wordWrappedLabel);}
        private void ReportPage(){Section("LOCAL-FIRST REPORTING","Async GPU readback, bounded image encoding and a durable disk queue.");foreach(var p in new[]{"AutoReportErrors","AllowScreenshots","ReportCooldownSeconds","ScreenshotLongEdge","UploadsEnabled","RelayUrl","LimitedClientKey","UploadDuringGameplay"})Field(p);
            testOutcome=(FakeOutcome)EditorGUILayout.EnumPopup("Fake scenario",testOutcome);
            if(GUILayout.Button("Simulate delivery — no network"))Simulate();if(GUILayout.Button("Create [FAKE] local report"))SendFake();
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
        private async void SendFake(){if(!EditorApplication.isPlaying || DebuggerRuntime.Instance==null){status="Enter Play Mode in the demo first.";return;}var token=lifetime.Token;var ok=await DebuggerRuntime.Instance.QueueReportAsync("[FAKE] "+testOutcome+" — editor acceptance",true,false);if(this!=null && !token.IsCancellationRequested){status=ok?"Synthetic report stored locally.":"Report not saved.";Repaint();}}
        private void ThemePage(){Section("DREAMY THEME","Dark surfaces, mint accents, clear typography and the original responsive layout.");GUILayout.Label("Package prefabs are the maintained base. Create project variants to change colors, fonts and layout. The debugger owns a diagnostic Canvas and does not depend on Dreamy UI or Feature bases.",EditorStyles.wordWrappedLabel);if(GUILayout.Button("Select overlay prefab"))Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(DebuggerAssetBuilder.PanelPath);if(GUILayout.Button("Select HUD prefab"))Selection.activeObject=AssetDatabase.LoadAssetAtPath<GameObject>(DebuggerAssetBuilder.HudPath);}
        private void AcceptancePage(){Section("ACCEPTANCE GATES","Source tests + UI behaviour + player/device receipts.");GUILayout.Label("0  Baseline and migration inventory\n1  Standalone package, GUIDs and prefab references\n2  Access, typed cheats, lifecycle and input\n3  HUD sampling, toggles and measured cost\n4  Outbox recovery, capture failures and quotas\n5  Fake server, HTTP retry/idempotency and relay\n6  Native/OS evidence support matrix\n7  Consumer import, release build and upgrade",EditorStyles.wordWrappedLabel);GUILayout.Space(16);if(GUILayout.Button("Validate package prefabs")){status=DebuggerAssetBuilder.ValidatePrefabs();}if(GUILayout.Button("Open Test Runner"))EditorApplication.ExecuteMenuItem("Window/General/Test Runner");}
        private void CreateConfig(){var path=EditorUtility.SaveFilePanelInProject("Create debugger configuration","DebuggerConfiguration","asset","Project-owned debugger configuration");if(string.IsNullOrEmpty(path))return;config=CreateInstance<DebuggerConfiguration>();AssetDatabase.CreateAsset(config,path);Selection.activeObject=config;}
    }
}
