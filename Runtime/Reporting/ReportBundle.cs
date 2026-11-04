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
        public int ImageBytes => image?.Length ?? 0;
        public byte[] CopyImage()=>image==null?null:(byte[])image.Clone();
        internal byte[] Image=>image;
        public ReportBundle(DiagnosticReport report,string context="",byte[] jpeg=null,string screenshotReason="not requested",DateTime? screenshotUtc=null) {
            Report=report??throw new ArgumentNullException(nameof(report));
            if(context==null || Encoding.UTF8.GetByteCount(context)>256*1024)throw new ArgumentException("Context cap is 256 KiB");
            if(jpeg!=null && jpeg.Length>512*1024)throw new ArgumentException("Screenshot cap is 512 KiB");
            Context=context;image=jpeg==null?null:(byte[])jpeg.Clone();ScreenshotReason=(screenshotReason??"").Substring(0,Math.Min(screenshotReason?.Length??0,256));ScreenshotUtc=screenshotUtc;
        }
    }
}
