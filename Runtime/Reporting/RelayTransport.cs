using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public enum RelayFailure { None, Authentication, Payload, RateLimit, Timeout, Network, Server, Endpoint, Unexpected, InvalidAcknowledgement, Ambiguous }
    public enum DeliveryTarget { Relay, Discord, Telegram }
    public sealed class RelayResult {
        public DeliveryStatus Status {get;}
        public double RetryAfterSeconds {get;}
        public int HttpStatusCode {get;}
        public RelayFailure Failure {get;}
        public DeliveryTarget Target {get;}
        // Deliberately contains no endpoint, header, response body or exception text.
        public string DiagnosticSummary => Target + " " + Status + (Failure == RelayFailure.None ? "" : ": " + Failure) +
            (HttpStatusCode == 0 ? "" : " (HTTP " + HttpStatusCode + ")");
        public RelayResult(DeliveryStatus status,double retryAfterSeconds=0,int httpStatusCode=0,RelayFailure failure=RelayFailure.None,DeliveryTarget target=DeliveryTarget.Relay){Status=status;RetryAfterSeconds=Math.Max(0,retryAfterSeconds);HttpStatusCode=httpStatusCode;Failure=failure;Target=target;}
    }
    public interface IBundleTransport {Task<RelayResult> SendBundleAsync(ReportBundle bundle,CancellationToken token);}
    public sealed class RelayTransport : IBundleTransport,IDisposable {
        private readonly HttpClient client;
        private readonly Uri endpoint;
        private readonly string clientKey;
        public RelayTransport(string endpoint,string limitedClientKey="",HttpMessageHandler handler=null){
            this.endpoint=new Uri(endpoint);
            if(this.endpoint.Scheme!="https" && !(this.endpoint.Scheme=="http" && this.endpoint.IsLoopback))throw new ArgumentException("HTTPS required outside loopback");
            clientKey=limitedClientKey??"";client=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false,UseProxy=!this.endpoint.IsLoopback});client.Timeout=TimeSpan.FromSeconds(15);
        }
        public async Task<RelayResult> SendBundleAsync(ReportBundle bundle,CancellationToken token){
            try {
            using(var request=new HttpRequestMessage(HttpMethod.Post,endpoint)){
                request.Headers.TryAddWithoutValidation("Idempotency-Key",bundle.Report.Id);
                if(clientKey.Length>0)request.Headers.TryAddWithoutValidation("X-Dreamy-Key",clientKey);
                request.Content=new StringContent(Serialize(bundle),Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false)){
                    var code=(int)response.StatusCode;var retry=response.Headers.RetryAfter;
                    var seconds=retry?.Delta?.TotalSeconds??(retry?.Date.HasValue==true?(retry.Date.Value-DateTimeOffset.UtcNow).TotalSeconds:0);
                    if(code==200 || code==202)return new RelayResult(DeliveryStatus.Accepted,httpStatusCode:code);
                    var failure=code==401 || code==403 ? RelayFailure.Authentication :
                        code==429 ? RelayFailure.RateLimit : code==408 || code==504 ? RelayFailure.Timeout :
                        code>=500 ? RelayFailure.Server : code==400 || code==413 || code==415 || code==422 ? RelayFailure.Payload :
                        code==404 || code==405 || code>=300 && code<400 ? RelayFailure.Endpoint : RelayFailure.Unexpected;
                    if(code==408 || code==429 || code>=500)return new RelayResult(DeliveryStatus.Retry,seconds,code,failure);
                    return new RelayResult(DeliveryStatus.Rejected,httpStatusCode:code,failure:failure);
                }
            }
            } catch(OperationCanceledException) when(!token.IsCancellationRequested) {
                return new RelayResult(DeliveryStatus.Retry,failure:RelayFailure.Timeout);
            } catch(HttpRequestException) {
                return new RelayResult(DeliveryStatus.Retry,failure:RelayFailure.Network);
            }
        }
        public static string Serialize(ReportBundle bundle) => SerializeMetadata(bundle, false) +
            ",\"jpegBase64\":" + Quote(bundle.ImageBytes == 0 ? "" : Convert.ToBase64String(bundle.Image)) + "}";

        public static string SerializeReportJson(ReportBundle bundle) => SerializeExport(bundle);
        internal static string SerializeExport(ReportBundle bundle) => SerializeMetadata(bundle, true) +
            ",\"attachment\":{\"file\":" + (bundle.ImageBytes == 0 ? "null" : Quote("screenshot.jpg")) +
            ",\"bytes\":" + bundle.ImageBytes.ToString(CultureInfo.InvariantCulture) + "}}";

        private static string Utc(DateTime value) => Quote(value.ToString("O", CultureInfo.InvariantCulture));

        private static string SerializeMetadata(ReportBundle bundle, bool export)
        {
            var report = bundle.Report;
            var json = new StringBuilder(export ? "{\"schemaVersion\":1" : "{\"schema\":1");
            json.Append(",\"id\":").Append(Quote(report.Id)).Append(",\"message\":").Append(Quote(report.Message))
                .Append(",\"buildId\":").Append(Quote(report.BuildId)).Append(",\"isSynthetic\":").Append(report.IsSynthetic ? "true" : "false")
                .Append(",\"createdUtc\":").Append(Utc(report.CreatedUtc)).Append(",\"context\":").Append(Quote(bundle.Context))
                .Append(",\"screenshotReason\":").Append(Quote(bundle.ScreenshotReason))
                .Append(",\"screenshotUtc\":").Append(bundle.ScreenshotUtc.HasValue ? Utc(bundle.ScreenshotUtc.Value) : Quote(""));
            var details = bundle.Details;
            if (details != null)
            {
                json.Append(",\"details\":{\"source\":").Append(Quote(details.Source)).Append(",\"severity\":").Append(Quote(details.Severity))
                    .Append(",\"stackTrace\":").Append(Quote(details.StackTrace))
                    .Append(",\"occurrenceCount\":").Append(details.OccurrenceCount.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"firstSeenUtc\":").Append(Utc(details.FirstSeenUtc)).Append(",\"lastSeenUtc\":").Append(Utc(details.LastSeenUtc))
                    .Append(",\"performance\":{\"sampledAtUtc\":").Append(details.SampledAtUtc == default ? "null" : Utc(details.SampledAtUtc))
                    .Append(",\"fps\":").Append(details.Fps > 0 ? details.Fps.ToString("R", CultureInfo.InvariantCulture) : "null")
                    .Append(",\"unityAllocatedBytes\":").Append(details.AllocatedBytes.ToString(CultureInfo.InvariantCulture))
                    .Append(",\"unityReservedBytes\":").Append(details.ReservedBytes.ToString(CultureInfo.InvariantCulture)).Append("}}");
            }
            return json.ToString();
        }
        private static string Quote(string value){var b=new StringBuilder("\"");foreach(var c in value){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
        public void Dispose()=>client.Dispose();
    }
}
