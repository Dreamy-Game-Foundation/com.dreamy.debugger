using System.Threading;
using System.Threading.Tasks;

namespace Dreamy.Debugger.Reporting
{
    public enum DeliveryStatus { Accepted, Retry, Rejected, Busy, Canceled }
    /// <summary>Implementations must honor cancellation; never call Unity APIs from worker continuations.</summary>
    public interface IReportTransport
    {
        Task<DeliveryStatus> SendAsync(DiagnosticReport report, CancellationToken cancellationToken);
    }
}
