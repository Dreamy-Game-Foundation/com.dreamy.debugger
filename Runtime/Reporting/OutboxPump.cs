using System;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public sealed class OutboxPump {
        private readonly DiskOutbox outbox;private readonly IBundleTransport transport;private int running;
        public int Accepted {get;private set;} public int Rejected {get;private set;} public int Retried {get;private set;}
        public OutboxPump(DiskOutbox outbox,IBundleTransport transport){this.outbox=outbox??throw new ArgumentNullException(nameof(outbox));this.transport=transport??throw new ArgumentNullException(nameof(transport));}
        public async Task TickAsync(DateTime nowUtc,CancellationToken token,string reportId=null){
            if(Interlocked.CompareExchange(ref running,1,0)!=0)return;
            try{
                var pending=await outbox.ReadAsync(token).ConfigureAwait(false);
                foreach(var entry in pending){
                    if(reportId!=null && entry.Bundle.Report.Id!=reportId)continue;
                    token.ThrowIfCancellationRequested();if(entry.NextAttemptUtc>nowUtc)continue;
                    RelayResult result;
                    try{result=await transport.SendBundleAsync(entry.Bundle,token).ConfigureAwait(false);}
                    catch(OperationCanceledException) when(token.IsCancellationRequested){throw;}
                    catch(Exception){result=new RelayResult(DeliveryStatus.Retry);}
                    if(result.Status==DeliveryStatus.Accepted){await outbox.CompleteAsync(entry.Bundle.Report.Id,token:token).ConfigureAwait(false);Accepted++;}
                    else if(result.Status==DeliveryStatus.Rejected || entry.Attempts>=7){await outbox.CompleteAsync(entry.Bundle.Report.Id,true,token).ConfigureAwait(false);Rejected++;}
                    else{
                        var jitter=(entry.Bundle.Report.Id[0]%7)*.25;
                        var delay=Math.Max(result.RetryAfterSeconds,Math.Min(3600,Math.Pow(2,entry.Attempts+1)*5+jitter));
                        await outbox.RetryAsync(entry,nowUtc.AddSeconds(delay),token).ConfigureAwait(false);Retried++;
                    }
                    break; // One item per host tick, never a busy upload loop.
                }
            }finally{Interlocked.Exchange(ref running,0);}
        }
    }
}
