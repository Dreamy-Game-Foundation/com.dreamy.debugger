using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class RelayDiagnosticsTests
    {
        [Test]
        public async Task OutboxRetainsFailureClassificationForRuntimeDiagnostics()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dreamy-diagnostic-" + Guid.NewGuid().ToString("N"));
            try
            {
                var outbox = new DiskOutbox(directory);
                await outbox.EnqueueAsync(new ReportBundle(new DiagnosticReport("test", "test")));
                using (var relay = new RelayTransport("https://qa.example.test/reports", "limited-key", new Handler { Code = 401 }))
                {
                    var pump = new OutboxPump(outbox, relay);
                    await pump.TickAsync(DateTime.UtcNow.AddSeconds(1), CancellationToken.None);
                    Assert.That(pump.LastResult.HttpStatusCode, Is.EqualTo(401));
                    Assert.That(pump.LastResult.Failure, Is.EqualTo(RelayFailure.Authentication));
                    Assert.That((await outbox.ReadLocalAsync())[0].State, Is.EqualTo(LocalReportState.Failed));
                }
            }
            finally { if (System.IO.Directory.Exists(directory)) System.IO.Directory.Delete(directory, true); }
        }

        private sealed class Handler : HttpMessageHandler
        {
            public int Code;
            public Exception Error;
            public string Key, Body, MediaType;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                if (Error != null) throw Error;
                Key = string.Join("", request.Headers.GetValues("X-Dreamy-Key"));
                MediaType = request.Content.Headers.ContentType.MediaType;
                Body = await request.Content.ReadAsStringAsync();
                var response = new HttpResponseMessage((HttpStatusCode)Code) { Content = new StringContent("secret-response-must-not-be-logged") };
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));
                return response;
            }
        }

        [TestCase(202, DeliveryStatus.Accepted, RelayFailure.None)]
        [TestCase(401, DeliveryStatus.Rejected, RelayFailure.Authentication)]
        [TestCase(403, DeliveryStatus.Rejected, RelayFailure.Authentication)]
        [TestCase(400, DeliveryStatus.Rejected, RelayFailure.Payload)]
        [TestCase(413, DeliveryStatus.Rejected, RelayFailure.Payload)]
        [TestCase(404, DeliveryStatus.Rejected, RelayFailure.Endpoint)]
        [TestCase(302, DeliveryStatus.Rejected, RelayFailure.Endpoint)]
        [TestCase(429, DeliveryStatus.Retry, RelayFailure.RateLimit)]
        [TestCase(408, DeliveryStatus.Retry, RelayFailure.Timeout)]
        [TestCase(504, DeliveryStatus.Retry, RelayFailure.Timeout)]
        [TestCase(503, DeliveryStatus.Retry, RelayFailure.Server)]
        public async Task TextOnlyDiagnosticPreservesStatusAndNeverExposesKeyOrBody(int code, DeliveryStatus status, RelayFailure failure)
        {
            var handler = new Handler { Code = code };
            using (var relay = new RelayTransport("https://qa.example.test/reports", "limited-key", handler))
            {
                var result = await relay.SendBundleAsync(new ReportBundle(new DiagnosticReport("[QA] text only", "test", true)), CancellationToken.None);
                Assert.That(result.Status, Is.EqualTo(status));
                Assert.That(result.HttpStatusCode, Is.EqualTo(code));
                Assert.That(result.Failure, Is.EqualTo(failure));
                Assert.That(handler.Key, Is.EqualTo("limited-key"));
                Assert.That(handler.MediaType, Is.EqualTo("application/json"));
                Assert.That(handler.Body, Does.Contain("\"jpegBase64\":\"\""));
                Assert.That(result.DiagnosticSummary, Does.Not.Contain("limited-key").And.Not.Contain("secret-response").And.Not.Contain("https://"));
                if (code == 429) Assert.That(result.RetryAfterSeconds, Is.EqualTo(60));
            }
        }

        [TestCase(true, RelayFailure.Timeout)]
        [TestCase(false, RelayFailure.Network)]
        public async Task NetworkAndTimeoutFailuresRemainDistinctWithoutExceptionText(bool timeout, RelayFailure failure)
        {
            var handler = new Handler { Error = timeout ? (Exception)new TaskCanceledException("secret-exception") : new HttpRequestException("secret-exception") };
            using (var relay = new RelayTransport("https://qa.example.test/reports", handler: handler))
            {
                var result = await relay.SendBundleAsync(new ReportBundle(new DiagnosticReport("test", "test")), CancellationToken.None);
                Assert.That(result.Status, Is.EqualTo(DeliveryStatus.Retry));
                Assert.That(result.Failure, Is.EqualTo(failure));
                Assert.That(result.HttpStatusCode, Is.Zero);
                Assert.That(result.DiagnosticSummary, Does.Not.Contain("secret-exception"));
            }
        }

        [Test]
        public void CallerCancellationRemainsCancellation()
        {
            using (var cancel = new CancellationTokenSource())
            using (var relay = new RelayTransport("https://qa.example.test/reports", handler: new Handler { Error = new TaskCanceledException() }))
            {
                cancel.Cancel();
                Assert.CatchAsync<OperationCanceledException>(() => relay.SendBundleAsync(new ReportBundle(new DiagnosticReport("test", "test")), cancel.Token));
            }
        }
    }
}
