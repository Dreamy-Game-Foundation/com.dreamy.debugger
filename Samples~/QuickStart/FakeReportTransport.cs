using System;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;

namespace Dreamy.Debugger.Sample
{
    public enum FakeReportOutcome { Success, Offline, RateLimited, ServerError, Reject, Timeout }
    /// <summary>No sockets, credentials, file IO or real destinations. Configure before starting a send.</summary>
    public sealed class FakeReportTransport : IReportTransport
    {
        public FakeReportOutcome Outcome { get; set; }
        public int DelayMilliseconds { get; set; } = 10;
        public int SendCount { get; private set; }
        public async Task<DeliveryStatus> SendAsync(DiagnosticReport report, CancellationToken cancellationToken)
        {
            var outcome = Outcome;
            var delay = DelayMilliseconds;
            if (!report.IsSynthetic) throw new InvalidOperationException("Fake transport only accepts synthetic reports.");
            if (delay < 0 || delay > 30000) throw new ArgumentOutOfRangeException(nameof(DelayMilliseconds));
            SendCount++;
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            switch (outcome)
            {
                case FakeReportOutcome.Success: return DeliveryStatus.Accepted;
                case FakeReportOutcome.Reject: return DeliveryStatus.Rejected;
                case FakeReportOutcome.Timeout: throw new TimeoutException("Synthetic timeout.");
                default: return DeliveryStatus.Retry;
            }
        }
    }
}
