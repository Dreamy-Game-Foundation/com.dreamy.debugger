using UnityEngine;
namespace Dreamy.Debugger {
    public enum DebuggerMode { Off, Development, Qa, ReleaseDiagnostics }
    public enum HudMode { Off, FpsOnly, Compact, Detailed }
    [CreateAssetMenu(menuName="Dreamy/Debugger/Configuration",fileName="DebuggerConfiguration")]
    public sealed class DebuggerConfiguration : ScriptableObject {
        [Header("Access")]
        public DebuggerMode Mode=DebuggerMode.Development;
        [Range(1,20)]public int RequiredTaps=5;
        [Range(.1f,2)]public float TapGapSeconds=.4f;
        [Range(.5f,10)]public float SequenceTimeoutSeconds=2.5f;
        public string Credential="0000";
        public bool Passphrase;
        [Range(30,3600)]public float SessionSeconds=300;
        [Header("Overlay")]
        public HudMode InitialHud=HudMode.Off;
        public bool ShowCornerTrigger=true;
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
