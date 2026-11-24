using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public enum LocalReportState { Pending, RelayAccepted, Failed }

    public sealed class PendingReport {
        public ReportBundle Bundle {get;internal set;}
        public int Attempts {get;internal set;}
        public DateTime NextAttemptUtc {get;internal set;}
        public LocalReportState State { get; internal set; }
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
            return Work(()=>{var path=PathFor(bundle.Report.Id);if(File.Exists(path)||File.Exists(path+".saved")||File.Exists(path+".dead"))return false;Prune();Write(path,new PendingReport{Bundle=bundle,NextAttemptUtc=DateTime.UtcNow});Prune();return File.Exists(path);},token);
        }
        public Task<PendingReport[]> ReadAsync(CancellationToken token=default)=>Work(()=>Load(false),token);
        public Task<PendingReport[]> ReadLocalAsync(CancellationToken token=default)=>Work(()=>Load(true),token);
        private PendingReport[] Load(bool includeHistory) {
            Prune();var result=new List<PendingReport>();
            var files=includeHistory ? new DirectoryInfo(directory).GetFiles().Where(IsReportFile) : Files();
            foreach(var file in files){
                try{
                    var entry=Read(file.FullName);
                    entry.State=file.Extension==".saved"?LocalReportState.RelayAccepted:
                        file.Extension==".dead"?LocalReportState.Failed:LocalReportState.Pending;
                    result.Add(entry);
                }catch(Exception ex) when(ex is IOException || ex is ArgumentException || ex is FormatException){File.Delete(file.FullName);}
            }
            return result.OrderBy(x=>x.Bundle.Report.CreatedUtc).ToArray();
        }
        public Task<bool> DeleteAsync(string id, CancellationToken token=default)=>Work(()=>{
            var path=PathFor(id);var removed=false;
            foreach(var file in new[]{path,path+".saved",path+".dead"}){
                if(!File.Exists(file))continue;
                File.Delete(file);removed=true;
            }
            return removed;
        },token);
        public Task<bool> RetainAcceptedAsync(string id, CancellationToken token=default)=>Work(()=>{
            var path=PathFor(id);
            if(!File.Exists(path))return false;
            File.Move(path,path+".saved");
            return true;
        },token);
        public Task<string> ExportAsync(string id, string destinationDirectory, CancellationToken token=default)=>Work(()=>{
            var path=PathFor(id);
            var stored=new[]{path,path+".saved",path+".dead"}.FirstOrDefault(File.Exists);
            if(stored==null)return null;
            var bundle=Read(stored).Bundle;
            var directory=Path.GetFullPath(destinationDirectory);
            Directory.CreateDirectory(directory);
            var output=Path.Combine(directory,id);
            if(Directory.Exists(output))return output;
            var staged=Path.Combine(directory,".export-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staged);
            try{
                File.WriteAllText(Path.Combine(staged,"report.json"),RelayTransport.SerializeExport(bundle),new UTF8Encoding(false));
                if(bundle.ImageBytes>0)File.WriteAllBytes(Path.Combine(staged,"screenshot.jpg"),bundle.Image);
                token.ThrowIfCancellationRequested();
                Directory.Move(staged,output);
                return output;
            }finally{
                if(Directory.Exists(staged))Directory.Delete(staged,true);
            }
        },token);
        public Task<bool> CompleteAsync(string id,bool rejected=false,CancellationToken token=default)=>Work(()=>{
            var path=PathFor(id);if(!File.Exists(path))return false;
            // Permanent rejection is removed instead of being resent indefinitely; counters belong to the host/relay.
            if(rejected){var dead=path+".dead";if(File.Exists(dead))File.Delete(dead);File.Move(path,dead);Prune();}else File.Delete(path);return true;},token);
        public Task<bool> RetryAsync(PendingReport report,DateTime nextUtc,CancellationToken token=default)=>Work(()=>{
            var path=PathFor(report.Bundle.Report.Id);if(!File.Exists(path))return false;
            report.Attempts++;report.NextAttemptUtc=nextUtc;Write(path,report);return true;},token);
        private string PathFor(string id){if(!Guid.TryParseExact(id,"N",out _))throw new ArgumentException("Invalid report ID");return Path.Combine(directory,id+".report");}
        private FileInfo[] Files()=>new DirectoryInfo(directory).GetFiles("*.report").OrderBy(f=>f.LastWriteTimeUtc).ToArray();
        private static bool IsReportFile(FileInfo file)=>file.Name.EndsWith(".report",StringComparison.Ordinal) ||
            file.Name.EndsWith(".report.dead",StringComparison.Ordinal) || file.Name.EndsWith(".report.saved",StringComparison.Ordinal);
        private void Prune(){
            foreach(var temp in Directory.GetFiles(directory,"*.tmp"))File.Delete(temp);
            var files=new DirectoryInfo(directory).GetFiles().Where(IsReportFile).OrderBy(f=>f.Extension==".report").ThenBy(f=>f.LastWriteTimeUtc).ToArray();long bytes=files.Sum(f=>f.Length);int count=files.Length;
            foreach(var file in files)if(DateTime.UtcNow-file.LastWriteTimeUtc>retention||count>maxReports||bytes>maxBytes){bytes-=file.Length;count--;file.Delete();}
        }
        private static void Write(string path,PendingReport p){
            var temp=path+".tmp";
            try{
                using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)){
                    using(var w=new BinaryWriter(stream,Encoding.UTF8,true)){
                        w.Write(p.Bundle.Details==null?0x44524731:0x44524732);w.Write(p.Bundle.Report.Id);w.Write(p.Bundle.Report.Message);w.Write(p.Bundle.Report.BuildId);
                        w.Write(p.Bundle.Report.IsSynthetic);w.Write(p.Bundle.Report.CreatedUtc.Ticks);w.Write(p.Bundle.Context);
                        w.Write(p.Bundle.ScreenshotReason);w.Write(p.Bundle.ScreenshotUtc?.Ticks??0);w.Write(p.Attempts);w.Write(p.NextAttemptUtc.Ticks);
                        w.Write(p.Bundle.ImageBytes);if(p.Bundle.ImageBytes>0)w.Write(p.Bundle.Image);
                        var details=p.Bundle.Details;
                        if(details!=null){
                            w.Write(details.Source);w.Write(details.Severity);w.Write(details.StackTrace);w.Write(details.OccurrenceCount);
                            w.Write(details.FirstSeenUtc.Ticks);w.Write(details.LastSeenUtc.Ticks);w.Write(details.Fps);
                            w.Write(details.AllocatedBytes);w.Write(details.ReservedBytes);w.Write(details.SampledAtUtc.Ticks);
                        }
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
                var version=reader.ReadInt32();
                if(version!=0x44524731 && version!=0x44524732)throw new IOException("Unknown schema");
                var id=ReadString(reader);var message=ReadString(reader);var build=ReadString(reader);var fake=reader.ReadBoolean();var created=new DateTime(reader.ReadInt64(),DateTimeKind.Utc);
                var report=new DiagnosticReport(id,message,build,fake,created);var context=ReadString(reader);var reason=ReadString(reader);var imageAt=reader.ReadInt64();
                var attempts=reader.ReadInt32();var next=new DateTime(reader.ReadInt64(),DateTimeKind.Utc);var length=reader.ReadInt32();
                if(length<0 || length>512*1024 || attempts<0)throw new IOException("Invalid report bounds");
                var image=reader.ReadBytes(length);if(image.Length!=length)throw new EndOfStreamException();
                var details=version==0x44524732
                    ? new ReportDetails(ReadString(reader),ReadString(reader),ReadString(reader),reader.ReadInt32(),
                        ReadUtc(reader),ReadUtc(reader),reader.ReadDouble(),reader.ReadInt64(),reader.ReadInt64(),ReadUtc(reader))
                    : null;
                var bundle=new ReportBundle(report,context,length==0?null:image,reason,imageAt==0?(DateTime?)null:new DateTime(imageAt,DateTimeKind.Utc),details);
                return new PendingReport{Bundle=bundle,Attempts=attempts,NextAttemptUtc=next};
            }
        }
        private static DateTime ReadUtc(BinaryReader reader)=>new DateTime(reader.ReadInt64(),DateTimeKind.Utc);
    }
}
