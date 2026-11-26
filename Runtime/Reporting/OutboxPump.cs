using System;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public sealed class OutboxPump {
        private readonly DiskOutbox outbox;private readonly IBundleTransport transport;
        private readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        private string sendingReportId;
        private int revision;
        public string SendingReportId=>Volatile.Read(ref sendingReportId);
        private RelayResult lastResult;
        public RelayResult LastResult=>Volatile.Read(ref lastResult);
        public int Revision=>Volatile.Read(ref revision);
        private readonly bool keepAccepted;
        private readonly string destination;
        public int Accepted {get;private set;} public int Rejected {get;private set;} public int Retried {get;private set;}
        public OutboxPump(DiskOutbox outbox,IBundleTransport transport):this(outbox,transport,false){}
        public OutboxPump(DiskOutbox outbox,IBundleTransport transport,bool keepAccepted,string destination=null){this.destination=destination;this.outbox=outbox??throw new ArgumentNullException(nameof(outbox));this.transport=transport??throw new ArgumentNullException(nameof(transport));this.keepAccepted=keepAccepted;}
        public async Task TickAsync(DateTime nowUtc,CancellationToken token,string reportId=null){
            await RunAsync(nowUtc,token,reportId,false).ConfigureAwait(false);
        }
        public Task RetryNowAsync(string reportId,CancellationToken token){
            if(string.IsNullOrEmpty(reportId))throw new ArgumentException("Report ID is required.",nameof(reportId));
            return RunAsync(DateTime.UtcNow,token,reportId,true);
        }
        private async Task RunAsync(DateTime nowUtc,CancellationToken token,string reportId,bool manual){
            if(manual)await gate.WaitAsync(token).ConfigureAwait(false);
            else if(!gate.Wait(0,token))return;
            var changed=false;
            try{
                if(manual)changed=await outbox.RequeueFailedAsync(reportId,nowUtc,token,destination).ConfigureAwait(false);
                var pending=await outbox.ReadAsync(token).ConfigureAwait(false);
                foreach(var entry in pending){
                    if(entry.Destination!=destination || reportId!=null && entry.Bundle.Report.Id!=reportId)continue;
                    token.ThrowIfCancellationRequested();if(entry.NextAttemptUtc>nowUtc)continue;
                    changed=true;
                    Volatile.Write(ref sendingReportId,entry.Bundle.Report.Id);
                    RelayResult result;
                    try{result=await transport.SendBundleAsync(entry.Bundle,token).ConfigureAwait(false);}
                    catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
                    catch(Exception){result=new RelayResult(DeliveryStatus.Retry,failure:RelayFailure.Unexpected);}
                    Volatile.Write(ref lastResult,result);
                    if(result.Status==DeliveryStatus.Accepted){
                        if(keepAccepted)await outbox.RetainAcceptedAsync(entry.Bundle.Report.Id,token).ConfigureAwait(false);
                        else await outbox.CompleteAsync(entry.Bundle.Report.Id,token:token).ConfigureAwait(false);
                        Accepted++;
                    }
                    else if(result.Status==DeliveryStatus.Rejected || entry.Attempts>=7){await outbox.CompleteAsync(entry.Bundle.Report.Id,true,token,result.Failure==RelayFailure.Ambiguous).ConfigureAwait(false);Rejected++;}
                    else{
                        var jitter=(entry.Bundle.Report.Id[0]%7)*.25;
                        var delay=Math.Max(result.RetryAfterSeconds,Math.Min(3600,Math.Pow(2,entry.Attempts+1)*5+jitter));
                        await outbox.RetryAsync(entry,nowUtc.AddSeconds(delay),token).ConfigureAwait(false);Retried++;
                    }
                    break; // One item per host tick, never a busy upload loop.
                }
            }finally{
                Volatile.Write(ref sendingReportId,null);
                if(changed)Interlocked.Increment(ref revision);
                gate.Release();
            }
        }
    }
}
