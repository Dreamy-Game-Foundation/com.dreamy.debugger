using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public enum CrashEvidenceKind { ManagedException,NativeCrash,Anr,LowMemory,AbnormalSession,OsExit }
    public sealed class CrashEvidence {
        public string SourceId {get;}
        public CrashEvidenceKind Kind {get;}
        public string Description {get;}
        public DateTime OccurredUtc {get;}
        public CrashEvidence(string sourceId,CrashEvidenceKind kind,string description,DateTime occurredUtc){
            if(string.IsNullOrWhiteSpace(sourceId)||sourceId.Length>256||description==null||description.Length>12000)throw new ArgumentException("Invalid crash evidence bounds");
            SourceId=sourceId;Kind=kind;Description=description;OccurredUtc=occurredUtc.ToUniversalTime();
        }
    }
    /// <summary>Native SDK adapters collect on next launch; acknowledge only after durable local acceptance.</summary>
    public interface ICrashEvidenceProvider {
        Task<CrashEvidence[]> CollectPreviousAsync(CancellationToken token);
        Task AcknowledgeAsync(string sourceId,CancellationToken token);
    }
    public static class CrashEvidenceImporter {
        public static async Task<int> ImportAsync(ICrashEvidenceProvider provider,DiskOutbox outbox,string buildId,CancellationToken token){
            if(provider==null||outbox==null)throw new ArgumentNullException();var count=0;
            var evidence=await provider.CollectPreviousAsync(token).ConfigureAwait(false);
            if(evidence==null)return 0;
            var seen=0;
            foreach(var item in evidence){token.ThrowIfCancellationRequested();if(seen++>=10)break;
                byte[] digest;using(var hash=SHA256.Create())digest=hash.ComputeHash(Encoding.UTF8.GetBytes(item.SourceId+"|"+item.Kind+"|"+item.OccurredUtc.Ticks));
                var identity=new byte[16];Buffer.BlockCopy(digest,0,identity,0,16);
                var report=new DiagnosticReport(new Guid(identity).ToString("N"),item.Kind+": "+item.Description,buildId,false,DateTime.UtcNow);
                if(await outbox.EnqueueAsync(new ReportBundle(report,"provider="+item.SourceId+"\neventUtc="+item.OccurredUtc.ToString("O"),screenshotReason:"native/OS evidence; no crash-time screenshot guarantee"),token).ConfigureAwait(false)){
                    await provider.AcknowledgeAsync(item.SourceId,token).ConfigureAwait(false);count++;
                }
            }return count;
        }
    }
}
