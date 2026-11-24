using System;
using System.Text;
namespace Dreamy.Debugger.Reporting {
    /// <summary>Snapshot with strict attachment caps. Byte arrays are copied at ownership boundaries.</summary>
    public sealed class ReportBundle {
        private readonly byte[] image;
        public DiagnosticReport Report {get;}
        public string Context {get;}
        public string ScreenshotReason {get;}
        public DateTime? ScreenshotUtc {get;}
        public ReportDetails Details { get; }
        public int ImageBytes => image?.Length ?? 0;
        public byte[] CopyImage()=>image==null?null:(byte[])image.Clone();
        internal byte[] Image=>image;
        public ReportBundle(DiagnosticReport report,string context="",byte[] jpeg=null,string screenshotReason="not requested",DateTime? screenshotUtc=null)
            : this(report, context, jpeg, screenshotReason, screenshotUtc, null) { }
        public ReportBundle(DiagnosticReport report,string context,byte[] jpeg,string screenshotReason,DateTime? screenshotUtc,ReportDetails details) {
            Report=report??throw new ArgumentNullException(nameof(report));
            if(context==null || Encoding.UTF8.GetByteCount(context)>256*1024)throw new ArgumentException("Context cap is 256 KiB");
            if(jpeg!=null && jpeg.Length>512*1024)throw new ArgumentException("Screenshot cap is 512 KiB");
            Details=details;
            Context=context;image=jpeg==null?null:(byte[])jpeg.Clone();ScreenshotReason=(screenshotReason??"").Substring(0,Math.Min(screenshotReason?.Length??0,256));ScreenshotUtc=screenshotUtc;
        }
    }

    public sealed class ReportDetails
    {
        public string Source { get; }
        public string Severity { get; }
        public string StackTrace { get; }
        public int OccurrenceCount { get; }
        public DateTime FirstSeenUtc { get; }
        public DateTime LastSeenUtc { get; }
        public double Fps { get; }
        public long AllocatedBytes { get; }
        public long ReservedBytes { get; }
        public DateTime SampledAtUtc { get; }

        public ReportDetails(string source, string severity, string stackTrace, int count, DateTime firstSeenUtc,
            DateTime lastSeenUtc, double fps, long allocatedBytes, long reservedBytes, DateTime sampledAtUtc)
        {
            Source = Limit(source, 32);
            Severity = Limit(severity, 32);
            StackTrace = Limit(stackTrace, 8000);
            OccurrenceCount = Math.Max(1, count);
            FirstSeenUtc = firstSeenUtc;
            LastSeenUtc = lastSeenUtc;
            Fps = fps > 0 && !double.IsInfinity(fps) ? fps : 0;
            AllocatedBytes = allocatedBytes;
            ReservedBytes = reservedBytes;
            SampledAtUtc = sampledAtUtc;
        }

        private static string Limit(string value, int size) =>
            value == null ? "" : value.Substring(0, Math.Min(value.Length, size));
    }
}
