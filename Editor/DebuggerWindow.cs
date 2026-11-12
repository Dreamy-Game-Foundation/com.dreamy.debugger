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
        private int selected;private bool advanced;private Vector2 scroll;private DebuggerConfiguration config;private SerializedObject serialized;
        private string status="";private FakeOutcome testOutcome;private CancellationTokenSource lifetime;
        [MenuItem("Window/Dreamy Debugger/Settings",priority=100)]
        public static void Open(){var w=GetWindow<DebuggerWindow>("Dreamy Debugger");w.minSize=new Vector2(440,500);}
        [MenuItem("Window/Dreamy Debugger/Telegram & Reports",priority=102)]
        public static void OpenReports(){Open();GetWindow<DebuggerWindow>().selected=4;}
        [MenuItem("Window/Dreamy Debugger/Cheat Panel",priority=99)]
        public static void OpenCheatPanel(){Open();GetWindow<DebuggerWindow>().selected=2;if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null)DebuggerRuntime.Instance.Open(DefaultTabs.Options);}
        [MenuItem("Tools/Dreamy Debugger/Advanced/Access settings",priority=101)]private static void Settings(){Open();GetWindow<DebuggerWindow>().selected=1;}
        private void OnEnable(){lifetime=new CancellationTokenSource();config=DebuggerProjectSettings.instance.Configuration;if(config==null){var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(scene.IsValid())config=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DebuggerRuntime>(true)).Select(r=>r.ConfigurationAsset).FirstOrDefault(c=>c!=null); }EditorApplication.playModeStateChanged+=OnPlayMode;}
        private void OnDisable(){EditorApplication.playModeStateChanged-=OnPlayMode;lifetime?.Cancel();lifetime?.Dispose();}
        private void OnPlayMode(PlayModeStateChange state){Repaint();}
        private static GUIStyle Label(int size,Color color,bool bold=false)=>new GUIStyle(EditorStyles.label){font=AssetDatabase.LoadAssetAtPath<Font>("Packages/com.dreamy.debugger/Runtime/Branding/Material/"+(bold?"Barlow-SemiBold.ttf":"Barlow-Regular.ttf")),fontSize=size,fontStyle=FontStyle.Normal,normal={textColor=color},wordWrap=true};
        private static void Rule(Rect rect,Color color)=>EditorGUI.DrawRect(rect,color);
        private void OnGUI(){
            CaptureRect=new Rect(GUIUtility.GUIToScreenPoint(Vector2.zero),position.size);
            Rule(new Rect(0,0,position.width,position.height),Background);
            Rule(new Rect(0,0,position.width,58),Theme.Surface);
            if(banner==null)banner=AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.dreamy.debugger/Runtime/Branding/Material/Wordmark.png");
            if(banner!=null)GUI.DrawTexture(new Rect(16,17,108,26),banner,ScaleMode.ScaleToFit,true);
            GUI.Label(new Rect(142,16,position.width-250,30),"Debugger",Label(20,Theme.Ivory,true));
            GUI.Label(new Rect(position.width-94,21,80,24),DreamyDebug.Version,Label(11,Muted));
            Rule(new Rect(0,58,position.width,1),Theme.Border);
            GUI.Label(new Rect(16,72,138,24),"Project configuration",Label(12,Muted));
            var next=(DebuggerConfiguration)EditorGUI.ObjectField(new Rect(154,72,position.width-208,24),config,typeof(DebuggerConfiguration),false);
            if(next!=config){config=next;serialized=null;DebuggerProjectSettings.instance.Configuration=config;DebuggerProjectSettings.instance.SaveSettings();}
            using(new EditorGUI.DisabledScope(config==null))if(GUI.Button(new Rect(position.width-46,72,30,24),new GUIContent("◎","Locate configuration asset"))){EditorGUIUtility.PingObject(config);}
            var names=new[]{"General","Access","Cheats","Performance","Reports","Layout"};
            var order=new[]{0,1,5,2,3,4};
            var tabWidth=(position.width-24)/6;
            for(var index=0;index<order.Length;index++){
                var i=order[index];var rect=new Rect(12+index*tabWidth,108,tabWidth,36);
                var style=Label(position.width<540?11:13,i==selected?Theme.Ivory:Muted,i==selected);style.alignment=TextAnchor.MiddleCenter;
                if(GUI.Button(rect,names[i],style)){selected=i;scroll=Vector2.zero;GUI.FocusControl(null);}
                if(i==selected)Rule(new Rect(rect.x+6,rect.yMax-2,rect.width-12,2),Accent);
            }
            Rule(new Rect(12,145,position.width-24,1),Theme.Border);
            GUILayout.BeginArea(new Rect(16,160,position.width-32,position.height-200));
            GUILayout.Label(selected==5?"Layout":pages[selected],Label(20,Theme.Ivory,true));GUILayout.Space(4);
            GUILayout.Label(Subtitle(),Label(12,Muted));GUILayout.Space(12);
            scroll=EditorGUILayout.BeginScrollView(scroll);
            if(selected!=0){
                if(config==null){EditorGUILayout.HelpBox("Select a project-owned configuration or create one.",MessageType.Info);if(GUILayout.Button("Create project configuration"))CreateConfig();}
                else{serialized=new SerializedObject(config);serialized.Update();}
            }
            switch(selected){case 0:Overview();break;case 1:AccessPage();break;case 2:OptionsPage();break;case 3:PerformancePage();break;case 4:ReportPage();break;case 5:LayoutPage();break;}
            if(serialized!=null && config!=null && selected>0)serialized.ApplyModifiedProperties();
            if(!string.IsNullOrEmpty(status)){GUILayout.Space(12);EditorGUILayout.HelpBox(status,MessageType.Info);}
            EditorGUILayout.EndScrollView();GUILayout.EndArea();
            Rule(new Rect(0,position.height-28,position.width,28),Theme.Surface);
            GUI.Label(new Rect(16,position.height-22,position.width-130,20),config!=null?"Changes are saved to the selected asset.":"Select a configuration to edit.",Label(10,Muted));
            GUI.Label(new Rect(position.width-96,position.height-22,80,20),EditorApplication.isPlaying?"PLAY MODE":"EDITOR",Label(10,Muted));
        }
        private string Subtitle(){switch(selected){case 0:return "One workspace for your internal tools.";case 1:return "Deliberate access. Configurable gestures and credentials.";case 2:return "Typed controls, clear groups and explicit ownership.";case 3:return "A small HUD with a bounded cost.";case 4:return "Capture locally. Deliver when the game has room.";case 5:return "Anchors, hidden entry and compact pinned overlays.";default:return "A phase is accepted only when its matching checks pass.";}}
        private void Section(string title,string description){GUILayout.Space(6);GUILayout.Label(title,Label(15,Theme.Ivory,true));GUILayout.Label(description,Label(12,Muted));GUILayout.Space(10);}
        private void Field(string name,string label=null){if(serialized!=null){var p=serialized.FindProperty(name);if(p!=null){if(label==null)EditorGUILayout.PropertyField(p,true);else EditorGUILayout.PropertyField(p,new GUIContent(label),true);GUILayout.Space(4);}}}
        private void Overview(){
            Section("DREAMY DEBUGGER","Console, options, profiler, system information and local-first bug reports.");
            GUILayout.Label("5 taps + 0000 by default",Label(18,Accent,true));GUILayout.Space(8);
            GUILayout.Label("Runtime controls use the project registry. Game-specific commands and service bindings stay in your imported sample.",Label(12,Muted));GUILayout.Space(18);
            var profile=DebuggerProjectSettings.instance;
            var loading=(DebuggerLoadingMode)EditorGUILayout.EnumPopup("Loading mode",profile.Loading);
            if(loading!=profile.Loading){profile.Loading=loading;profile.SaveSettings();}
            EditorGUILayout.HelpBox("Automatic: Play any scene, tap the top-left corner five times and enter 0000. No sample or bootstrap required. An authored scene bootstrap takes precedence.",MessageType.Info);
            if(advanced && GUILayout.Button("Import editable cheat sample"))DebuggerAssetBuilder.InstallSample();
            advanced=EditorGUILayout.Foldout(advanced,"Optional sample",true);
            RuntimePanelActions(DefaultTabs.Console);
        }
        private void AccessPage(){Section("ACCESS POLICY","Tap count and credential are project configuration; PIN preserves leading zeroes.");Field("Mode");Field("RequiredTaps");Field("TapGapSeconds");Field("SequenceTimeoutSeconds");Field("Credential");Field("Passphrase");Field("SessionSeconds");Field("ShowCornerTrigger","Enable hidden tap trigger");DrawTapPosition();Field("PersistRuntimePreferences");Field("PreferenceRevision");EditorGUILayout.HelpBox("Release diagnostics has no cheat UI. Local credentials prevent accidental access and do not authorize server operations.",MessageType.None);}
        private void OptionsPage(){Section("GROUPED CHEAT OPTIONS","Boolean • number • enum • text • read-only • action");GUILayout.Label("Register a typed category/container with CheatRegistry.Register(...). Dispose its registration when the owner closes. The runtime panel reads the same registry and rechecks access on every mutation.",EditorStyles.wordWrappedLabel);
            RuntimePanelActions(DefaultTabs.Options);
            if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null){foreach(var option in DebuggerRuntime.Instance.Cheats.Snapshot())EditorGUILayout.LabelField(option.Category+" / "+option.DisplayName,option.IsAction?"Action":option.ValueType.Name);}
        }
        private void RuntimePanelActions(DefaultTabs tab){
            GUILayout.Space(14);
            if(EditorApplication.isPlaying){
                var runtime=DebuggerRuntime.Instance;
                if(runtime==null){EditorGUILayout.HelpBox("No active debugger runtime. Check Loading mode, scene bootstrap state and configuration Mode.",MessageType.Warning);return;}
                EditorGUILayout.HelpBox(runtime.Access.IsUnlocked?"Debugger unlocked. Cheat controls are in the runtime Cheats tab.":"Open the runtime panel, then enter your configured PIN (default 0000).",MessageType.Info);
                if(GUILayout.Button(tab==DefaultTabs.Options?"Open cheat panel in Game view":"Open runtime console in Game view",GUILayout.Height(36))){runtime.Open(tab);EditorApplication.ExecuteMenuItem("Window/General/Game");}
                return;
            }
            if(DebuggerProjectSettings.instance.Loading==DebuggerLoadingMode.Automatic){EditorGUILayout.HelpBox("Enter Play Mode to use the debugger. A configuration asset is optional; defaults are 5 taps + 0000, HUD Off, reports local only.",MessageType.Info);return;}
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var host=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<DebuggerRuntime>(true)).FirstOrDefault();
            EditorGUILayout.HelpBox(host==null?"This scene has no debugger bootstrap. Add it to the startup scene, then enter Play Mode to open the cheat panel.":"Debugger bootstrap is present. Enter Play Mode, then click Open cheat panel here or tap the top-left corner 5 times and enter 0000 (default configuration).",host==null?MessageType.Warning:MessageType.Info);
            if(host==null && GUILayout.Button("Add debugger bootstrap to current scene",GUILayout.Height(36))){try{DebuggerAssetBuilder.AddBootstrapToActiveScene();status="Debugger added. Save the scene and enter Play Mode.";}catch(Exception e){status=e.Message;}}
        }
        private void PerformancePage(){
            if(config==null || serialized==null)return;
            Field("InitialHud");
            Field("HudTapToCycle","Tap FPS to cycle");Field("HudCloseButton","Show close button");Field("HudDragEnabled","Free drag");
            DrawHudPreview((HudMode)serialized.FindProperty("InitialHud").enumValueIndex);
            EditorGUILayout.HelpBox("HUD starts at the bottom left. Drag its grip to place it freely. Runtime keeps your display mode and position on this device.",MessageType.Info);
            if(EditorApplication.isPlaying && DebuggerRuntime.Instance!=null){var runtime=DebuggerRuntime.Instance;var current=runtime.Hud!=null?runtime.Hud.Mode:HudMode.Off;var next=(HudMode)EditorGUILayout.EnumPopup("HUD mode",current);if(next!=current)runtime.SetHud(next);}
        }
        private void LayoutPage()
        {
            Section("OVERLAY LAYOUT","HUD uses the full viewport; panel and access dialog respect the safe area.");
            DrawTapPosition();GUILayout.Space(12);
            EditorGUILayout.LabelField("HUD safe area","Off (full viewport)");EditorGUILayout.LabelField("Panel safe area","On");
            EditorGUILayout.HelpBox("Pinned categories start as compact chips. Expand only when needed. Console can be docked with its pin button.",MessageType.None);
        }
        private void DrawTapPosition(){
            if(serialized==null)return;
            GUILayout.Label("Tap trigger position",EditorStyles.boldLabel);
            var property=serialized.FindProperty("TapPosition");
            var positions=new[]{PinAlignment.TopLeft,PinAlignment.TopCenter,PinAlignment.TopRight,PinAlignment.CenterLeft,PinAlignment.CenterLeft,PinAlignment.CenterRight,PinAlignment.BottomLeft,PinAlignment.BottomCenter,PinAlignment.BottomRight};
            var labels=new[]{"Top left","Top center","Top right","Center left","","Center right","Bottom left","Bottom center","Bottom right"};
            for(var row=0;row<3;row++){EditorGUILayout.BeginHorizontal();for(var col=0;col<3;col++){var i=row*3+col;using(new EditorGUI.DisabledScope(i==4)){var selected=property.intValue==(int)positions[i] && i!=4;if(GUILayout.Toggle(selected,labels[i],"Button",GUILayout.Height(32)) && i!=4)property.intValue=(int)positions[i];}}EditorGUILayout.EndHorizontal();}
        }
        private void DrawHudPreview(HudMode mode){
            var rect=EditorGUILayout.GetControlRect(false,mode==HudMode.Detailed?150:112);
            Rule(rect,Theme.Surface);GUI.Label(new Rect(rect.x+12,rect.y+8,rect.width-24,20),"SAMPLE PREVIEW • NOT LIVE",Label(10,Muted));
            if(mode==HudMode.Off){GUI.Label(new Rect(rect.x+12,rect.y+42,rect.width-24,30),"HUD off",Label(22,Theme.Ivory));return;}
            Rule(new Rect(rect.x+12,rect.y+35,Mathf.Min(280,rect.width-24),2),Theme.Brand);
            GUI.Label(new Rect(rect.x+20,rect.y+44,rect.width-40,32),"60 fps   ·   16.7 ms",Label(22,Theme.Ivory,true));
            if(mode==HudMode.Compact || mode==HudMode.Detailed)GUI.Label(new Rect(rect.x+20,rect.y+80,rect.width-40,24),"RAM 414 MB",Label(12,Muted));
            if(mode==HudMode.Detailed)GUI.Label(new Rect(rect.x+20,rect.y+112,rect.width-40,24),"avg 59.8    1% 53    .1% 47",Label(12,Muted));
        }
        private void ReportPage(){Section("LOCAL-FIRST REPORTING","Async GPU readback, bounded image encoding and a durable disk queue.");foreach(var p in new[]{"AutoReportErrors","AllowScreenshots","ReportCooldownSeconds","ScreenshotLongEdge","UploadsEnabled","RelayUrl","LimitedClientKey","UploadDuringGameplay"})Field(p);
            Section("SCREENSHOT MODE","In-game Screenshot tab: local full-resolution PNG, separate from reports.");
            Field("ScreenshotRestoreKey");Field("ScreenshotRestoreTouchHoldSeconds");
            EditorGUILayout.HelpBox("Mark gameplay UI roots with ScreenshotHideTarget, or choose AllCanvases in Screenshot Mode. Restore/cancel with the configured key or a three-finger hold. UI Toolkit and OnGUI need project-specific integration.",MessageType.Info);
            TelegramRelaySettings.DrawSettings();
            EditorGUILayout.HelpBox("Telegram uses your configured relay. A relay response confirms durable acceptance; destination delivery is tracked separately on the relay. Configure the bot token and chat ID on the relay, then set this game's Relay URL and enable Uploads. A bot token is not a Relay URL.",MessageType.Info);
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
        private void CreateConfig(){var path=EditorUtility.SaveFilePanelInProject("Create debugger configuration","DebuggerConfiguration","asset","Project-owned debugger configuration");if(string.IsNullOrEmpty(path))return;config=CreateInstance<DebuggerConfiguration>();AssetDatabase.CreateAsset(config,path);DebuggerProjectSettings.instance.Configuration=config;DebuggerProjectSettings.instance.SaveSettings();Selection.activeObject=config;}
    }
}
