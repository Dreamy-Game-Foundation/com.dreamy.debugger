using UnityEngine;
namespace Dreamy.Debugger {
    public enum ReportingMode { Relay = 0, LocalOnly = 1, DirectQa = 2 }
    public enum DebuggerMode { Off, Development, Qa, ReleaseDiagnostics }
    public enum HudPosition { TopRight, TopLeft, BottomRight, BottomLeft, Free }
    public enum HudMode { Off, FpsOnly, Compact, Detailed }
    [CreateAssetMenu(menuName="Dreamy/Debugger/Configuration",fileName="DebuggerConfiguration")]
    public sealed class DebuggerConfiguration : ScriptableObject {
        [Header("Access")]
        public DebuggerMode Mode=DebuggerMode.Development;
        [Range(1,20)]public int RequiredTaps=3;
        [Range(.1f,2)]public float TapGapSeconds=.4f;
        [Range(.5f,10)]public float SequenceTimeoutSeconds=2.5f;
        public string Credential="0000";
        public bool Passphrase;
        [Range(30,3600)]public float SessionSeconds=300;
        [Header("Scene inspection")]
        public bool EnableSceneInspection=true;
        public bool AllowSceneEditing=false;
        [System.Serializable]
        public struct ComponentIcon
        {
            [Tooltip("Full C# type name, including namespace.")]
            public string TypeName;
            public Sprite Icon;
        }
        public enum HierarchyDrawer { Automatic, Inline, Picker }
        [System.Serializable]
        public struct MemberPresentation
        {
            [Tooltip("Full C# component type name, including namespace.")]
            public string TypeName;
            [Tooltip("Field or property name as declared in C#.")]
            public string MemberName;
            public string Label;
            public string Group;
            public HierarchyDrawer Drawer;
        }
        public MemberPresentation[] HierarchyMemberPresentation = System.Array.Empty<MemberPresentation>();
        public string[] HierarchyProjectTags = System.Array.Empty<string>();
        public ComponentIcon[] HierarchyComponentIcons = System.Array.Empty<ComponentIcon>();
        [Min(.1f)] public float SceneRefreshSeconds=.25f;
        [Range(.1f,5)] public float SceneScanBudgetMilliseconds=1;
        [Range(1,8)] public int InspectionMaxDepth=4;
        [Range(8,64)] public int InspectionPageSize=32;
        [Header("Local preferences")]
        public bool PersistRuntimePreferences=true;
        [Min(1)] public int PreferenceRevision=1;
        [Header("Overlay")]
        public HudMode InitialHud=HudMode.Off;
        [Tooltip("Enable an invisible tap region at TapPosition; no icon or sprite is displayed.")]
        public bool ShowCornerTrigger=true;
        public Dreamy.Debugger.Presentation.PinAlignment TapPosition=Dreamy.Debugger.Presentation.PinAlignment.TopLeft;
        public HudPosition InitialHudPosition=HudPosition.BottomLeft;
        public Vector2 HudOffset=new Vector2(12,12);
        [Range(.5f,2)]public float HudScale=1;
        public bool HudMemory=true, HudAudio=false;
        [Tooltip("Drag the small HUD grip; the rest of the HUD passes touches to the game.")]
        public bool HudDragEnabled=true;
        public bool HudTapToCycle=true, HudCloseButton=true;
        public Color FpsGood=new Color(.46f,.83f,.23f),FpsCaution=new Color(.95f,.82f,.2f),FpsCritical=new Color(.86f,.16f,.12f);
        public int GoodFps=60,CautionFps=30;
        public bool LowFpsWarning;
        public float LowFpsThreshold=30,LowFpsSustain=3,LowFpsCooldown=60;

        [Header("Screenshot mode — local PNG export")]
        public KeyCode ScreenshotRestoreKey=KeyCode.F8;
        public KeyCode ScreenshotCaptureKey=KeyCode.None;
        [Tooltip("Relative to the application's persistent data folder. Empty uses DreamyDebugger/Screenshots.")]
        public string ScreenshotOutputSubdirectory="";
        [Min(.25f)] public float ScreenshotRestoreTouchHoldSeconds=1;

        [Header("Reports — local by default")]
        public ReportingMode ReportingMode;
        public bool AutoReportErrors;
        public bool AllowAutoReportingControls=true;
        public bool AllowScreenshots;
        public bool UploadDuringGameplay;
        public string RelayUrl="";
        [Tooltip("Limited per-build ingestion key only. Never bot/mailbox credentials.")]
        public string LimitedClientKey="";
        public bool UploadsEnabled;
        [Range(10,600)]public float ReportCooldownSeconds=60;
        [Tooltip("Maximum screenshot edge in pixels. Capture may use a smaller size to fit the device's memory budget.")]
        [Range(240,1440)]public int ScreenshotLongEdge=1440;
    }
}
