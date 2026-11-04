using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public sealed class PendingReport {
        public ReportBundle Bundle {get;internal set;}
        public int Attempts {get;internal set;}
        public DateTime NextAttemptUtc {get;internal set;}
    }
    /// <summary>All filesystem access runs through one worker gate. No Unity APIs; temp-then-rename persistence.</summary>
    public sealed class DiskOutbox {
        private readonly string directory;
        private readonly int maxReports;
        private readonly long maxBytes;
        private readonly TimeSpan retention;
        private readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        public DiskOutbox(string directory,int maxReports=10,long maxBytes=20*1024*1024,double retentionDays=7) {
            if(string.IsNullOrWhiteSpace(directory)||maxReports<1||maxBytes<1024*1024||retentionDays<=0)throw new ArgumentException("Invalid outbox policy");
            this.directory=Path.GetFullPath(directory);this.maxReports=maxReports;this.maxBytes=maxBytes;retention=TimeSpan.FromDays(retentionDays);
        }
        private async Task<T> Work<T>(Func<T> action,CancellationToken token) {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try{return await Task.Run(()=>{token.ThrowIfCancellationRequested();Directory.CreateDirectory(directory);return action();},token).ConfigureAwait(false);}
            finally{gate.Release();}
        }
        public Task<bool> EnqueueAsync(ReportBundle bundle,CancellationToken token=default) {
            if(bundle==null)throw new ArgumentNullException(nameof(bundle));
            return Work(()=>{var path=PathFor(bundle.Report.Id);if(File.Exists(path))return false;Prune();Write(path,new PendingReport{Bundle=bundle,NextAttemptUtc=DateTime.UtcNow});Prune();return File.Exists(path);},token);
        }
        public Task<PendingReport[]> ReadAsync(CancellationToken token=default)=>Work(()=>{
            Prune();var result=new List<PendingReport>();
            foreach(var file in Files()){try{result.Add(Read(file.FullName));}catch(Exception ex) when(ex is IOException || ex is ArgumentException || ex is FormatException){File.Delete(file.FullName);}}
            return result.OrderBy(x=>x.Bundle.Report.CreatedUtc).ToArray();},token);
        public Task<bool> CompleteAsync(string id,bool rejected=false,CancellationToken token=default)=>Work(()=>{
            var path=PathFor(id);if(!File.Exists(path))return false;
            // Permanent rejection is removed instead of being resent indefinitely; counters belong to the host/relay.
            if(rejected){var dead=path+".dead";if(File.Exists(dead))File.Delete(dead);File.Move(path,dead);Prune();}else File.Delete(path);return true;},token);
        public Task<bool> RetryAsync(PendingReport report,DateTime nextUtc,CancellationToken token=default)=>Work(()=>{
            var path=PathFor(report.Bundle.Report.Id);if(!File.Exists(path))return false;
            report.Attempts++;report.NextAttemptUtc=nextUtc;Write(path,report);return true;},token);
        private string PathFor(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid report ID");return Path.Combine(directory,id+".report");}
        private FileInfo[] Files()=>new DirectoryInfo(directory).GetFiles("*.report").OrderBy(f=>f.LastWriteTimeUtc).ToArray();
        private void Prune(){
            foreach(var temp in Directory.GetFiles(directory,"*.tmp"))File.Delete(temp);
            var files=new DirectoryInfo(directory).GetFiles().Where(f=>f.Name.EndsWith(".report")||f.Name.EndsWith(".dead")).OrderBy(f=>f.LastWriteTimeUtc).ToArray();long bytes=files.Sum(f=>f.Length);int count=files.Length;
            foreach(var file in files)if(DateTime.UtcNow-file.LastWriteTimeUtc>retention||count>maxReports||bytes>maxBytes){bytes-=file.Length;count--;file.Delete();}
        }
        private static void Write(string path,PendingReport p){
            var temp=path+".tmp";
            try{
                using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){
                    using(var w=new BinaryWriter(stream,Encoding.UTF8,true)){
                        w.Write(0x44524731);w.Write(p.Bundle.Report.Id);w.Write(p.Bundle.Report.Message);w.Write(p.Bundle.Report.BuildId);
                        w.Write(p.Bundle.Report.IsSynthetic);w.Write(p.Bundle.Report.CreatedUtc.Ticks);w.Write(p.Bundle.Context);
                        w.Write(p.Bundle.ScreenshotReason);w.Write(p.Bundle.ScreenshotUtc?.Ticks??0);w.Write(p.Attempts);w.Write(p.NextAttemptUtc.Ticks);
                        w.Write(p.Bundle.ImageBytes);if(p.Bundle.ImageBytes>0)w.Write(p.Bundle.Image);
                    } stream.Flush(true);
                }
                if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
            }finally{if(File.Exists(temp))File.Delete(temp);}
        }
        private static string ReadString(BinaryReader reader){
            var length=0;var shift=0;byte value;
            do{if(shift>=35)throw new IOException("Invalid string length");value=reader.ReadByte();length|=(value&127)<<shift;shift+=7;}while((value&128)!=0);
            if(length<0 || length>262144 || length>reader.BaseStream.Length-reader.BaseStream.Position)throw new IOException("Invalid string bounds");
            var bytes=reader.ReadBytes(length);if(bytes.Length!=length)throw new EndOfStreamException();return Encoding.UTF8.GetString(bytes);
        }
        private static PendingReport Read(string path){
            var info=new FileInfo(path);if(info.Length>1024*1024)throw new IOException("Oversize report");
            using(var reader=new BinaryReader(File.OpenRead(path),Encoding.UTF8)){
                if(reader.ReadInt32()!=0x44524731)throw new IOException("Unknown schema");
                var id=ReadString(reader);var message=ReadString(reader);var build=ReadString(reader);var fake=reader.ReadBoolean();var created=new DateTime(reader.ReadInt64(),DateTimeKind.Utc);
                var report=new DiagnosticReport(id,message,build,fake,created);var context=ReadString(reader);var reason=ReadString(reader);var imageAt=reader.ReadInt64();
                var attempts=reader.ReadInt32();var next=new DateTime(reader.ReadInt64(),DateTimeKind.Utc);var length=reader.ReadInt32();
                if(length<0 || length>512*1024 || attempts<0)throw new IOException("Invalid report bounds");
                var image=reader.ReadBytes(length);if(image.Length!=length)throw new EndOfStreamException();
                return new PendingReport{Bundle=new ReportBundle(report,context,length==0?null:image,reason,imageAt==0?(DateTime?)null:new DateTime(imageAt,DateTimeKind.Utc)),Attempts=attempts,NextAttemptUtc=next};
            }
        }
    }
}
