using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
using Dreamy.Debugger.Access;
using Dreamy.Debugger.Metrics;
using NUnit.Framework;
namespace Dreamy.Debugger.Tests {
    public sealed class ReportPipelineTests {
        private string folder;
        [SetUp]public void SetUp(){folder=Path.Combine(Path.GetTempPath(),"dreamy-report-test-"+Guid.NewGuid().ToString("N"));}
        [TearDown]public void TearDown(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
        private ReportBundle Bundle(string text="[FAKE] report")=>new ReportBundle(new DiagnosticReport(text,"test",true),"context");
        [Test]public async Task OutboxSurvivesRecreationAndDeduplicatesIdentity(){var bundle=Bundle();var box=new DiskOutbox(folder);Assert.That(await box.EnqueueAsync(bundle),Is.True);Assert.That(await box.EnqueueAsync(bundle),Is.False);var recovered=await new DiskOutbox(folder).ReadAsync();Assert.That(recovered.Length,Is.EqualTo(1));Assert.That(recovered[0].Bundle.Report.Id,Is.EqualTo(bundle.Report.Id));Assert.That(recovered[0].Bundle.Report.Message,Is.EqualTo(bundle.Report.Message));}
        [Test]public async Task CorruptAndInterruptedWritesDoNotPreventGoodReports(){var box=new DiskOutbox(folder);await box.EnqueueAsync(Bundle());File.WriteAllBytes(Path.Combine(folder,"broken.report"),new byte[]{1,2,3});File.WriteAllText(Path.Combine(folder,"abandoned.tmp"),"partial");Assert.That((await box.ReadAsync()).Length,Is.EqualTo(1));Assert.That(File.Exists(Path.Combine(folder,"abandoned.tmp")),Is.False);}
        [Test]public async Task QuotaEvictsOldestAndDoesNotGrowWithoutBound(){var box=new DiskOutbox(folder,maxReports:2);for(var i=0;i<5;i++)await box.EnqueueAsync(Bundle("[FAKE] "+i));Assert.That((await box.ReadAsync()).Length,Is.EqualTo(2));Assert.That(Directory.GetFiles(folder).Length,Is.EqualTo(2));}
        [Test]public async Task CompletionRemovesOnlyTheAcknowledgedReport(){var box=new DiskOutbox(folder);var a=Bundle();var b=Bundle();await box.EnqueueAsync(a);await box.EnqueueAsync(b);await box.CompleteAsync(a.Report.Id);Assert.That((await box.ReadAsync())[0].Bundle.Report.Id,Is.EqualTo(b.Report.Id));}
        [Test]public async Task CancellationBeforeEnqueueCreatesNoReport(){using(var cancel=new CancellationTokenSource()){cancel.Cancel();Assert.ThrowsAsync<TaskCanceledException>(()=>new DiskOutbox(folder).EnqueueAsync(Bundle(),cancel.Token));}await Task.CompletedTask;}
        [Test]public void AttachmentAndPathBoundsAreEnforced(){Assert.Throws<ArgumentException>(()=>new ReportBundle(new DiagnosticReport("test","build"),jpeg:new byte[524289]));Assert.ThrowsAsync<ArgumentException>(()=>new DiskOutbox(folder).CompleteAsync("../../outside"));}
        private sealed class BundleFake : IBundleTransport {public RelayResult Result=new RelayResult(DeliveryStatus.Accepted);public int Calls;public Task<RelayResult> SendBundleAsync(ReportBundle b,CancellationToken t){Calls++;return Task.FromResult(Result);}}
        [Test]public async Task ManualSendTargetsOnlyItsNewReport(){var box=new DiskOutbox(folder);var older=Bundle("older");var selected=Bundle("selected");await box.EnqueueAsync(older);await box.EnqueueAsync(selected);var fake=new BundleFake();await new OutboxPump(box,fake).TickAsync(DateTime.UtcNow.AddSeconds(1),CancellationToken.None,selected.Report.Id);var left=await box.ReadAsync();Assert.That(fake.Calls,Is.EqualTo(1));Assert.That(left.Length,Is.EqualTo(1));Assert.That(left[0].Bundle.Report.Id,Is.EqualTo(older.Report.Id));}
        [Test]public async Task PumpRemovesAcceptedAndKeepsRetryableWithBackoff(){var box=new DiskOutbox(folder);await box.EnqueueAsync(Bundle());var fake=new BundleFake{Result=new RelayResult(DeliveryStatus.Retry,120)};var pump=new OutboxPump(box,fake);var now=DateTime.UtcNow.AddSeconds(1);await pump.TickAsync(now,CancellationToken.None);var pending=(await box.ReadAsync())[0];Assert.That(pending.Attempts,Is.EqualTo(1));Assert.That(pending.NextAttemptUtc,Is.GreaterThanOrEqualTo(now.AddSeconds(120)));await pump.TickAsync(now.AddSeconds(1),CancellationToken.None);Assert.That(fake.Calls,Is.EqualTo(1));fake.Result=new RelayResult(DeliveryStatus.Accepted);await pump.TickAsync(now.AddSeconds(121),CancellationToken.None);Assert.That((await box.ReadAsync()).Length,Is.Zero);}
        [Test]public async Task PermanentRejectionBecomesBoundedDeadLetter(){var box=new DiskOutbox(folder);await box.EnqueueAsync(Bundle());await new OutboxPump(box,new BundleFake{Result=new RelayResult(DeliveryStatus.Rejected)}).TickAsync(DateTime.UtcNow.AddSeconds(1),CancellationToken.None);Assert.That((await box.ReadAsync()).Length,Is.Zero);Assert.That(Directory.GetFiles(folder,"*.dead").Length,Is.EqualTo(1));}
        private sealed class HttpFake : HttpMessageHandler {public HttpStatusCode Code;public string Id;public string Body;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Id=string.Join("",request.Headers.GetValues("Idempotency-Key"));Body=await request.Content.ReadAsStringAsync();var response=new HttpResponseMessage(Code);response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(60));return response;}}
        [TestCase(202,DeliveryStatus.Accepted)][TestCase(429,DeliveryStatus.Retry)][TestCase(503,DeliveryStatus.Retry)][TestCase(400,DeliveryStatus.Rejected)]
        public async Task HttpStatusIsMappedWithStableIdempotency(int code,DeliveryStatus status){var fake=new HttpFake{Code=(HttpStatusCode)code};var bundle=Bundle("test \"quote\"\nnewline");using(var relay=new RelayTransport("http://127.0.0.1:8765/reports",handler:fake)){var result=await relay.SendBundleAsync(bundle,CancellationToken.None);Assert.That(result.Status,Is.EqualTo(status));Assert.That(fake.Id,Is.EqualTo(bundle.Report.Id));Assert.That(fake.Body,Does.Contain("\\\"quote\\\""));Assert.That(result.RetryAfterSeconds,Is.EqualTo(status==DeliveryStatus.Accepted?0:status==DeliveryStatus.Rejected?0:60));}}
        [Test]public void ExternalCleartextRelayIsRejected(){Assert.Throws<ArgumentException>(()=>new RelayTransport("http://example.com/reports"));}

        private sealed class NativeFake : ICrashEvidenceProvider {
            public bool Acknowledged;
            public Task<CrashEvidence[]> CollectPreviousAsync(CancellationToken token)=>Task.FromResult(new[]{new CrashEvidence("native-test-1",CrashEvidenceKind.NativeCrash,"[FAKE] previous native evidence",new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc))});
            public Task AcknowledgeAsync(string id,CancellationToken token){Acknowledged=true;return Task.CompletedTask;}
        }
        [Test]public async Task NativeEvidenceAcknowledgesOnlyAfterDurablePersistence(){var provider=new NativeFake();var box=new DiskOutbox(folder);Assert.That(await CrashEvidenceImporter.ImportAsync(provider,box,"test",CancellationToken.None),Is.EqualTo(1));Assert.That(provider.Acknowledged,Is.True);Assert.That((await box.ReadAsync())[0].Bundle.ScreenshotReason,Does.Contain("no crash-time screenshot"));}
        [Test]public void FrameWindowUsesFramesOverDurationAndBoundsHistory(){var window=new FrameWindow(2);window.Add(.01);window.Add(.03);Assert.That(window.FramesPerSecond,Is.EqualTo(50).Within(.001));window.Add(.02);Assert.That(window.Count,Is.EqualTo(2));Assert.That(window.GetOldest(0),Is.EqualTo(.03));window.Add(double.NaN);Assert.That(window.Count,Is.EqualTo(2));window.Clear();Assert.That(window.FramesPerSecond,Is.Zero);}
        [Test]public void PassphraseRequiresGestureAndSupportsLongCredential(){double time=1;var session=new AccessSession(new AccessPolicy(pin:"Dreamy QA passphrase",isPassphrase:true),()=>time);Assert.That(session.SubmitPin("Dreamy QA passphrase"),Is.EqualTo(UnlockResult.GestureRequired));Assert.That(session.RequestPrompt(),Is.True);Assert.That(session.SubmitPin("Dreamy QA passphrase"),Is.EqualTo(UnlockResult.Unlocked));}
    }
}
