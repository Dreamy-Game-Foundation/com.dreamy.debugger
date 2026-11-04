using System;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class ReportDispatcherTests
    {
        private sealed class Transport : IReportTransport
        {
            public Func<CancellationToken, Task<DeliveryStatus>> Send;
            public int Calls;
            public Task<DeliveryStatus> SendAsync(DiagnosticReport report,CancellationToken token) { Calls++; return Send(token); }
        }
        private DiagnosticReport Report() => new DiagnosticReport("[FAKE] exception", "test-build", true);
        [Test] public void ReportHasIdentityMarkerAndPayloadLimits()
        {
            var first=Report(); var second=Report();
            Assert.That(first.Id,Is.Not.EqualTo(second.Id)); Assert.That(first.IsSynthetic,Is.True);
            Assert.That(first.CreatedUtc.Kind,Is.EqualTo(DateTimeKind.Utc));
            Assert.Throws<ArgumentException>(()=>new DiagnosticReport(new string('x',16385),"build"));
            Assert.Throws<ArgumentException>(()=>new DiagnosticReport("error",new string('x',257)));
        }
        [Test] public async Task SuccessIsForwardedWithoutRetry()
        {
            var transport=new Transport{Send=token=>Task.FromResult(DeliveryStatus.Accepted)};
            Assert.That(await new ReportDispatcher(transport).SendAsync(Report(),CancellationToken.None),Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(transport.Calls,Is.EqualTo(1));
        }
        [Test] public async Task ConcurrentSendIsBusyAndCompletedSendReleasesGate()
        {
            var pending=new TaskCompletionSource<DeliveryStatus>();
            var transport=new Transport{Send=token=>pending.Task}; var dispatcher=new ReportDispatcher(transport);
            var first=dispatcher.SendAsync(Report(),CancellationToken.None);
            Assert.That(await dispatcher.SendAsync(Report(),CancellationToken.None),Is.EqualTo(DeliveryStatus.Busy));
            pending.SetResult(DeliveryStatus.Accepted); Assert.That(await first,Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(await dispatcher.SendAsync(Report(),CancellationToken.None),Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(transport.Calls,Is.EqualTo(2));
        }
        [Test] public async Task CancellationBeforeSendDoesNotCallTransport()
        {
            var transport=new Transport(); var source=new CancellationTokenSource(); source.Cancel();
            Assert.That(await new ReportDispatcher(transport).SendAsync(Report(),source.Token),Is.EqualTo(DeliveryStatus.Canceled));
            Assert.That(transport.Calls,Is.Zero); source.Dispose();
        }
        [Test] public async Task FailureAndTimeoutAreRetryableAndReleaseGate()
        {
            var transport=new Transport{Send=token=>throw new TimeoutException("fake timeout")};
            var dispatcher=new ReportDispatcher(transport);
            Assert.That(await dispatcher.SendAsync(Report(),CancellationToken.None),Is.EqualTo(DeliveryStatus.Retry));
            transport.Send=token=>Task.FromResult(DeliveryStatus.Rejected);
            Assert.That(await dispatcher.SendAsync(Report(),CancellationToken.None),Is.EqualTo(DeliveryStatus.Rejected));
        }
        [Test] public async Task CancelDuringSendIsObservedAndGateIsReleased()
        {
            var transport=new Transport{Send=async token=>{await Task.Delay(30000,token);return DeliveryStatus.Accepted;}};
            var dispatcher=new ReportDispatcher(transport); using(var source=new CancellationTokenSource())
            {
                var send=dispatcher.SendAsync(Report(),source.Token); source.Cancel();
                Assert.That(await send,Is.EqualTo(DeliveryStatus.Canceled));
            }
            transport.Send=token=>Task.FromResult(DeliveryStatus.Accepted);
            Assert.That(await dispatcher.SendAsync(Report(),CancellationToken.None),Is.EqualTo(DeliveryStatus.Accepted));
        }
    }
}
