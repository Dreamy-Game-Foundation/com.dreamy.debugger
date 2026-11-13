using UnityEngine;
namespace Dreamy.Debugger {
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
        [Min(.25f)] public float ScreenshotRestoreTouchHoldSeconds=1;

        [Header("Reports — local by default")]
        public bool AutoReportErrors;
        public bool AllowScreenshots;
        public bool UploadDuringGameplay;
        public string RelayUrl="";
        [Tooltip("Limited per-build ingestion key only. Never bot/mailbox credentials.")]
        public string LimitedClientKey="";
        public bool UploadsEnabled;
        [Range(10,600)]public float ReportCooldownSeconds=60;
        [Range(240,960)]public int ScreenshotLongEdge=960;
    }
}
