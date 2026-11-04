using System;
using System.Threading;
using System.Threading.Tasks;

namespace Dreamy.Debugger.Reporting
{
    /// <summary>Single-flight injectable dispatch; no background loop, retry loop or network side effects.</summary>
    public sealed class ReportDispatcher
    {
        private readonly IReportTransport transport;
        private int sending;
        public ReportDispatcher(IReportTransport transport) { this.transport = transport ?? throw new ArgumentNullException(nameof(transport)); }
        public async Task<DeliveryStatus> SendAsync(DiagnosticReport report, CancellationToken cancellationToken)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (cancellationToken.IsCancellationRequested) return DeliveryStatus.Canceled;
            if (Interlocked.CompareExchange(ref sending, 1, 0) != 0) return DeliveryStatus.Busy;
            try { return await transport.SendAsync(report, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return DeliveryStatus.Canceled; }
            catch (Exception) { return DeliveryStatus.Retry; }
            finally { Interlocked.Exchange(ref sending, 0); }
        }
    }
}
