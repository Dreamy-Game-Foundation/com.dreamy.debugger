using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace Dreamy.Debugger.Reporting {
    public sealed class RelayResult {
        public DeliveryStatus Status {get;}
        public double RetryAfterSeconds {get;}
        public RelayResult(DeliveryStatus status,double retryAfterSeconds=0){Status=status;RetryAfterSeconds=Math.Max(0,retryAfterSeconds);}
    }
    public interface IBundleTransport {Task<RelayResult> SendBundleAsync(ReportBundle bundle,CancellationToken token);}
    public sealed class RelayTransport : IBundleTransport,IDisposable {
        private readonly HttpClient client;
        private readonly Uri endpoint;
        private readonly string clientKey;
        public RelayTransport(string endpoint,string limitedClientKey="",HttpMessageHandler handler=null){
            this.endpoint=new Uri(endpoint);
            if(this.endpoint.Scheme!="https" && !(this.endpoint.Scheme=="http" && this.endpoint.IsLoopback))throw new ArgumentException("HTTPS required outside loopback");
            clientKey=limitedClientKey??"";client=handler==null?new HttpClient():new HttpClient(handler);client.Timeout=TimeSpan.FromSeconds(15);
        }
        public async Task<RelayResult> SendBundleAsync(ReportBundle bundle,CancellationToken token){
            using(var request=new HttpRequestMessage(HttpMethod.Post,endpoint)){
                request.Headers.TryAddWithoutValidation("Idempotency-Key",bundle.Report.Id);
                if(clientKey.Length>0)request.Headers.TryAddWithoutValidation("X-Dreamy-Key",clientKey);
                request.Content=new StringContent(Serialize(bundle),Encoding.UTF8,"application/json");
                using(var response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false)){
                    var code=(int)response.StatusCode;var retry=response.Headers.RetryAfter;
                    var seconds=retry?.Delta?.TotalSeconds??(retry?.Date.HasValue==true?(retry.Date.Value-DateTimeOffset.UtcNow).TotalSeconds:0);
                    if(code==200 || code==202)return new RelayResult(DeliveryStatus.Accepted);
                    if(code==408 || code==429 || code>=500)return new RelayResult(DeliveryStatus.Retry,seconds);
                    return new RelayResult(DeliveryStatus.Rejected);
                }
            }
        }
        public static string Serialize(ReportBundle b)=>"{\"schema\":1,\"id\":"+Quote(b.Report.Id)+",\"message\":"+Quote(b.Report.Message)+",\"buildId\":"+Quote(b.Report.BuildId)+",\"isSynthetic\":"+(b.Report.IsSynthetic?"true":"false")+",\"createdUtc\":"+Quote(b.Report.CreatedUtc.ToString("O",CultureInfo.InvariantCulture))+",\"context\":"+Quote(b.Context)+",\"screenshotReason\":"+Quote(b.ScreenshotReason)+",\"screenshotUtc\":"+Quote(b.ScreenshotUtc?.ToString("O",CultureInfo.InvariantCulture)??"")+",\"jpegBase64\":"+Quote(b.ImageBytes==0?"":Convert.ToBase64String(b.Image))+"}";
        private static string Quote(string value){var b=new StringBuilder("\"");foreach(var c in value){switch(c){case '"':b.Append("\\\"");break;case '\\':b.Append("\\\\");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
        public void Dispose()=>client.Dispose();
    }
}
