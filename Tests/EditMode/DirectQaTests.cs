#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Reporting;
using Dreamy.Debugger.Reporting.DirectQa;
using NUnit.Framework;

namespace Dreamy.Debugger.Tests
{
    public sealed class DirectQaTests
    {
        private string folder;
        private const string Webhook="https://discord.com/api/webhooks/123456/test_credential";
        private const string Token="123456:abcdefghijklmnopqrstuvwxyz12345";
        [SetUp] public void Setup()=>folder=Path.Combine(Path.GetTempPath(),"direct-qa-"+Guid.NewGuid().ToString("N"));
        [TearDown] public void Cleanup(){if(Directory.Exists(folder))Directory.Delete(folder,true);}
        private sealed class HttpFake:HttpMessageHandler
        {
            public readonly Queue<HttpResponseMessage> Responses=new Queue<HttpResponseMessage>();
            public readonly List<string> Bodies=new List<string>(),Paths=new List<string>();
            public readonly List<Dictionary<string, byte[]>> Parts = new List<Dictionary<string, byte[]>>();
            public string UserAgent;
            public Exception Error;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            {
                Paths.Add(request.RequestUri.AbsolutePath);Bodies.Add(await request.Content.ReadAsStringAsync());
                if (request.Content is MultipartFormDataContent multipart)
                {
                    var parts = new Dictionary<string, byte[]>();
                    foreach (var part in multipart)
                    {
                        var disposition = part.Headers.ContentDisposition;
                        var name = (disposition.FileName ?? disposition.Name).Trim('"');
                        parts.Add(name, await part.ReadAsByteArrayAsync());
                    }
                    Parts.Add(parts);
                }
                UserAgent=request.Headers.UserAgent.ToString();
                if(Error!=null)throw Error;
                return Responses.Dequeue();
            }
        }
        private static HttpResponseMessage Response(int code=200,string json="{\"id\":\"12345\"}")=>new HttpResponseMessage((HttpStatusCode)code){Content=new StringContent(json)};
        private static HttpResponseMessage Telegram(int code=200)=>Response(code,"{\"ok\":true,\"result\":{\"message_id\":123}}");
        private static ReportBundle Bundle(bool image=true)=>new ReportBundle(new DiagnosticReport("[QA] report","test",true),"app=test",image?new byte[]{255,216,255,217}:null);

        private static ReportBundle DetailedBundle()
        {
            var now = DateTime.UtcNow;
            const string stack = "UnityEngine.Debug:LogError (object)\nGame.Shop.Open() (at Assets/Shop.cs:12)\n" +
                "Game.Hud.Open()\nGame.Input.Press()\nGame.FullTraceOnly()";
            var details = new ReportDetails("auto", "Exception", stack, 3, now, now, 59.5, 32L << 20, 64L << 20, now);
            const string context = "app=test.app\nversion=1.2\nunity=6000.4\nplatform=Android\ndevice=Pixel 8\nos=Android 15\n" +
                "scene=Assets/Scenes/Sandbox.unity\n[Log] Console attachment only";
            return new ReportBundle(new DiagnosticReport("NullReferenceException: Shop <b> & <@123>", "qa-build"),
                context, new byte[] {255, 216, 255, 217}, "captured", now, details);
        }

        [Test]
        public async Task DiscordEmbedsReportSummaryAndAttachesOriginalFiles()
        {
            var bundle = DetailedBundle();
            var http = new HttpFake();
            http.Responses.Enqueue(Response());
            using var direct = new DirectDiscordTransport(Webhook, folder, http);

            Assert.That((await direct.SendBundleAsync(bundle, CancellationToken.None)).Status, Is.EqualTo(DeliveryStatus.Accepted));
            var parts = http.Parts[0];
            var payload = Encoding.UTF8.GetString(parts["payload_json"]);
            Assert.That(payload, Does.Contain("\"embeds\"").And.Contain("\"allowed_mentions\"")
                .And.Contain("NullReferenceException").And.Contain("3 occurrences")
                .And.Contain("Pixel 8").And.Contain("Sandbox").And.Contain("59.5 FPS")
                .And.Contain("Game.Shop.Open()").And.Contain("DD-").And.Contain(bundle.Report.Id)
                .And.Contain("attachment://screenshot.jpg")
                .And.Not.Contain("Game.FullTraceOnly()").And.Not.Contain("Assets/Shop.cs"));
            Assert.That(Encoding.UTF8.GetString(parts["report.json"]),
                Does.Contain("Game.FullTraceOnly()").And.Contain("Console attachment only"));
            Assert.That(parts["screenshot.jpg"], Is.EqualTo(bundle.CopyImage()));
        }

        [Test]
        public async Task TelegramEscapesCaptionAndCorrelatesOriginalScreenshotWithReport()
        {
            var bundle = DetailedBundle();
            var http = new HttpFake();
            http.Responses.Enqueue(Telegram());
            http.Responses.Enqueue(Telegram());
            using var direct = new DirectTelegramTransport(Token, "-123", folder, http);

            Assert.That((await direct.SendBundleAsync(bundle, CancellationToken.None)).Status, Is.EqualTo(DeliveryStatus.Accepted));
            var parts = http.Parts[0];
            Assert.That(Encoding.UTF8.GetString(parts["parse_mode"]), Is.EqualTo("HTML"));
            var caption = Encoding.UTF8.GetString(parts["caption"]);
            Assert.That(caption, Does.Contain("<b>Shop &lt;b&gt; &amp; &lt;@123&gt;</b>")
                .And.Contain("<pre>Game.Shop.Open()").And.Contain("59.5 FPS").And.Contain(bundle.Report.Id)
                .And.Not.Contain("Game.FullTraceOnly()").And.Not.Contain("Console attachment only"));
            Assert.That(WebUtility.HtmlDecode(Regex.Replace(caption, "<[^>]+>", "")).Length, Is.LessThanOrEqualTo(1024));
            Assert.That(Encoding.UTF8.GetString(parts["report.json"]), Does.Contain("Game.FullTraceOnly()"));
            Assert.That(Encoding.UTF8.GetString(http.Parts[1]["caption"]), Does.Contain(bundle.Report.Id));
            Assert.That(http.Parts[1]["screenshot.jpg"], Is.EqualTo(bundle.CopyImage()));
        }

        [Test] public async Task DiscordUploadsJsonAndScreenshotOnceAcrossRestart()
        {
            var http=new HttpFake();http.Responses.Enqueue(Response());var report=Bundle();
            using(var direct=new DirectDiscordTransport(Webhook,folder,http))Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Status,Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(http.Bodies[0],Does.Contain("report.json").And.Contain("screenshot.jpg").And.Contain(report.Report.Id).And.Not.Contain(Webhook));
            var reopened=new HttpFake();using(var direct=new DirectDiscordTransport(Webhook,folder,reopened))Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Status,Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(reopened.Bodies,Is.Empty);
        }
        [Test] public async Task TelegramPartial429RetriesOnlyImageAfterRestart()
        {
            var http=new HttpFake();http.Responses.Enqueue(Telegram());http.Responses.Enqueue(Response(429,"{\"ok\":false,\"parameters\":{\"retry_after\":37}}"));var report=Bundle();
            using(var direct=new DirectTelegramTransport(Token,"-123",folder,http)){
                var result=await direct.SendBundleAsync(report,CancellationToken.None);Assert.That(result.Failure,Is.EqualTo(RelayFailure.RateLimit));Assert.That(result.RetryAfterSeconds,Is.EqualTo(37));}
            var retry=new HttpFake();retry.Responses.Enqueue(Telegram());using(var direct=new DirectTelegramTransport(Token,"-123",folder,retry))Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Status,Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(retry.Bodies.Count,Is.EqualTo(1));Assert.That(retry.Bodies[0],Does.Contain("screenshot.jpg").And.Not.Contain("report.json"));
        }
        [TestCase(400,RelayFailure.Payload)][TestCase(401,RelayFailure.Authentication)][TestCase(403,RelayFailure.Authentication)]
        [TestCase(413,RelayFailure.Payload)][TestCase(429,RelayFailure.RateLimit)][TestCase(500,RelayFailure.Server)][TestCase(503,RelayFailure.Server)]
        public async Task ProviderHttpErrorsAreClassifiedWithoutCredentialLeak(int code,RelayFailure failure)
        {
            var http=new HttpFake();http.Responses.Enqueue(Response(code,"{\"error\":\"secret-response\"}"));
            using(var direct=new DirectDiscordTransport(Webhook,folder,http)){
                var result=await direct.SendBundleAsync(Bundle(false),CancellationToken.None);Assert.That(result.Failure,Is.EqualTo(failure));Assert.That(result.HttpStatusCode,Is.EqualTo(code));
                Assert.That(result.DiagnosticSummary,Does.Not.Contain("secret-response").And.Not.Contain(Webhook));}
        }
        [TestCase(413, RelayFailure.Payload)]
        [TestCase(429, RelayFailure.RateLimit)]
        public async Task OversizedErrorBodyDoesNotHideKnownHttpResult(int code, RelayFailure failure)
        {
            using var response=Response(code,new string('x',70*1024));
            if(code==429)response.Headers.RetryAfter=new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(42));
            var http=new HttpFake();
            http.Responses.Enqueue(response);
            using var direct=new DirectDiscordTransport(Webhook,folder,http);
            var result=await direct.SendBundleAsync(Bundle(false),CancellationToken.None);
            Assert.That(result.HttpStatusCode,Is.EqualTo(code));
            Assert.That(result.Failure,Is.EqualTo(failure));
            Assert.That(result.RetryAfterSeconds,Is.EqualTo(code==429?42:0));
        }
        [TestCase(false)][TestCase(true)]public async Task LostAcknowledgementRequiresExplicitConfirmation(bool timeout)
        {
            var report=Bundle();var http=new HttpFake{Error=timeout?(Exception)new TaskCanceledException("secret") : new HttpRequestException("secret")};
            using(var direct=new DirectDiscordTransport(Webhook,folder,http))Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Failure,Is.EqualTo(RelayFailure.Ambiguous));
            var retry=new HttpFake();retry.Responses.Enqueue(Response());using(var direct=new DirectDiscordTransport(Webhook,folder,retry)){
                Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Failure,Is.EqualTo(RelayFailure.Ambiguous));Assert.That(retry.Bodies,Is.Empty);
                await direct.AllowUncertainRetryAsync(report.Report.Id,CancellationToken.None);Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Status,Is.EqualTo(DeliveryStatus.Accepted));}
        }
        [Test]public async Task MalformedAcknowledgementIsNotSuccessOrAutomaticDuplicate()
        {
            var http=new HttpFake();http.Responses.Enqueue(Response(200,"not json"));using(var direct=new DirectDiscordTransport(Webhook,folder,http)){
                var report=Bundle(false);Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Failure,Is.EqualTo(RelayFailure.Ambiguous));
                Assert.That((await direct.SendBundleAsync(report,CancellationToken.None)).Failure,Is.EqualTo(RelayFailure.Ambiguous));Assert.That(http.Bodies.Count,Is.EqualTo(1));}
        }
        [Test]public async Task TextProbesUseProviderTextApiAndNoFiles()
        {
            var discord=new HttpFake();discord.Responses.Enqueue(Response());using(var direct=new DirectDiscordTransport(Webhook,folder,discord))Assert.That((await direct.TestConnectionAsync(CancellationToken.None)).Status,Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(discord.Bodies[0],Does.Contain("content").And.Not.Contain("report.json"));
            Assert.That(discord.UserAgent,Does.StartWith("DiscordBot (https://"));
            var telegram=new HttpFake();telegram.Responses.Enqueue(Telegram());using(var direct=new DirectTelegramTransport(Token,"-123",folder,telegram))Assert.That((await direct.TestConnectionAsync(CancellationToken.None)).Status,Is.EqualTo(DeliveryStatus.Accepted));
            Assert.That(telegram.Paths[0],Does.EndWith("/sendMessage"));Assert.That(telegram.Bodies[0],Does.Contain("chat_id").And.Contain("text").And.Not.Contain(Token));
        }
        [Test]public async Task DestinationsHaveIndependentReceiptsAndContainNoCredentials()
        {
            var report=Bundle(false);var a=new HttpFake();a.Responses.Enqueue(Response());using(var direct=new DirectDiscordTransport(Webhook,folder,a))await direct.SendBundleAsync(report,CancellationToken.None);
            var b=new HttpFake();b.Responses.Enqueue(Telegram());using(var direct=new DirectTelegramTransport(Token,"-123",folder,b))await direct.SendBundleAsync(report,CancellationToken.None);
            Assert.That(b.Bodies.Count,Is.EqualTo(1));foreach(var file in Directory.GetFiles(folder)){Assert.That(file,Does.Not.Contain("123456").And.Not.Contain(Token));Assert.That(File.ReadAllText(file),Does.Not.Contain(Webhook).And.Not.Contain(Token));}
        }
        [Test]public void InvalidCredentialsRejectBeforeNetwork()
        {
            Assert.Throws<ArgumentException>(()=>new DirectDiscordTransport("http://discord.com/api/webhooks/1/x",folder));
            Assert.Throws<ArgumentException>(()=>new DirectDiscordTransport(Webhook+"?wait=false",folder));
            Assert.Throws<ArgumentException>(()=>new DirectTelegramTransport("invalid","-1",folder));
        }
        [Test]public void CanceledBeforeSendHasNoNetworkOrReceipt()
        {
            var http=new HttpFake();using(var cancel=new CancellationTokenSource())using(var direct=new DirectDiscordTransport(Webhook,folder,http)){
                cancel.Cancel();Assert.CatchAsync<OperationCanceledException>(()=>direct.SendBundleAsync(Bundle(),cancel.Token));Assert.That(http.Bodies,Is.Empty);}
        }
    }
}
#endif
