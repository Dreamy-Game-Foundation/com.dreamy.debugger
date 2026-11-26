// Provider endpoints and privileged code are absent from non-Development players.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dreamy.Debugger.Foundation;

namespace Dreamy.Debugger.Reporting.DirectQa
{
    public enum DirectProvider { Discord, Telegram }

    /// <summary>Session-owned credentials, durable per-destination acknowledgements, no Unity/log APIs.</summary>
    public abstract class DirectQaTransport : IBundleTransport, IDisposable
    {
        private readonly HttpClient client;
        private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);
        private readonly string receiptDirectory, destinationHash;
        protected readonly Uri Endpoint;
        public string Destination => "direct:" + destinationHash;
        protected abstract DeliveryTarget Target { get; }
        protected abstract int Steps(ReportBundle bundle);
        protected abstract HttpRequestMessage ReportRequest(ReportBundle bundle, int step);
        protected abstract HttpRequestMessage TextRequest(string message);
        protected abstract bool Acknowledged(Dictionary<string, object> body);

        protected DirectQaTransport(Uri endpoint, string identity, string receiptDirectory, HttpMessageHandler handler)
        {
            Endpoint = endpoint;
            this.receiptDirectory = Path.GetFullPath(receiptDirectory);
            using (var hash = SHA256.Create()) destinationHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "").ToLowerInvariant();
            client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
            var userAgent = endpoint.Host == "discord.com"
                ? "DiscordBot (https://github.com/Dreamy-Game-Foundation/com.dreamy.debugger, 0.1.8)"
                : "DreamyDebugger-QA/0.1.8";
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", userAgent);
        }

        public async Task<RelayResult> TestConnectionAsync(CancellationToken token)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try { return await RequestAsync(TextRequest("[QA TEXT TEST] Dreamy Debugger " + Guid.NewGuid().ToString("N")), token).ConfigureAwait(false); }
            finally { gate.Release(); }
        }

        public async Task<RelayResult> SendBundleAsync(ReportBundle bundle, CancellationToken token)
        {
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var path = ReceiptPath(bundle.Report.Id);
                var state = await Task.Run(() => ReadState(path), token).ConfigureAwait(false);
                if (state.inFlight) return Result(DeliveryStatus.Rejected, RelayFailure.Ambiguous);
                var total = Steps(bundle);
                for (var step = state.completed; step < total; step++)
                {
                    token.ThrowIfCancellationRequested();
                    HttpRequestMessage request;
                    try { request=ReportRequest(bundle,step); }
                    catch(ArgumentException) { return Result(DeliveryStatus.Rejected,RelayFailure.Payload); }
                    using(request) {
                    // Persist BEFORE the provider side effect. A crash or lost acknowledgement cannot silently resend.
                    await Task.Run(() => WriteState(path, step, true), token).ConfigureAwait(false);
                    var result = await RequestAsync(request, token).ConfigureAwait(false);
                    if (result.Status == DeliveryStatus.Accepted)
                        await Task.Run(() => WriteState(path, step + 1, false), token).ConfigureAwait(false);
                    else
                    {
                        if (result.Failure == RelayFailure.Network || result.Failure == RelayFailure.Timeout || result.Failure == RelayFailure.InvalidAcknowledgement ||
                            result.Failure == RelayFailure.Server)
                            return Result(DeliveryStatus.Rejected, RelayFailure.Ambiguous, result.HttpStatusCode);
                        await Task.Run(() => WriteState(path, step, false), token).ConfigureAwait(false);
                        return result;
                    }
                    }
                }
                return Result(DeliveryStatus.Accepted);
            }
            finally { gate.Release(); }
        }

        public async Task AllowUncertainRetryAsync(string reportId, CancellationToken token)
        {
            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                var path = ReceiptPath(reportId);
                await Task.Run(() => { var state = ReadState(path); WriteState(path, state.completed, false); }, token).ConfigureAwait(false);
            }
            finally { gate.Release(); }
        }
        private string ReceiptPath(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new ArgumentException("Invalid report ID.");
            return Path.Combine(receiptDirectory, destinationHash + "-" + id + ".receipt");
        }
        private static (int completed, bool inFlight) ReadState(string path)
        {
            if (!File.Exists(path)) return (0, false);
            var value = File.ReadAllText(path).Split('|');
            if (value.Length != 2 || !int.TryParse(value[0], out var count) || count < 0 || count > 2 || (value[1] != "0" && value[1] != "1"))
                return (0, true); // Corruption never turns an uncertain send into an automatic duplicate.
            return (count, value[1] == "1");
        }
        private void WriteState(string path, int completed, bool inFlight)
        {
            Directory.CreateDirectory(receiptDirectory);
            // Receipt retention exceeds the outbox's seven days; never prune a receipt for a retained report.
            foreach (var file in new DirectoryInfo(receiptDirectory).GetFiles("*.receipt").Where(f => DateTime.UtcNow - f.LastWriteTimeUtc > TimeSpan.FromDays(8))) file.Delete();
            if (!File.Exists(path) && Directory.GetFiles(receiptDirectory, "*.receipt").Length >= 1024) throw new IOException("Direct receipt capacity reached.");
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var bytes = Encoding.ASCII.GetBytes(completed + "|" + (inFlight ? "1" : "0"));
                stream.Write(bytes, 0, bytes.Length); stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }

        private async Task<RelayResult> RequestAsync(HttpRequestMessage request, CancellationToken token)
        {
            using (request)
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
            deadline.CancelAfter(TimeSpan.FromSeconds(15));
            try
            {
                using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false))
                {
                    var code = (int)response.StatusCode;
                    Dictionary<string, object> body = null;
                    if (code == 200 || code == 429)
                    {
                        try
                        {
                            var text = await BoundedBody(response.Content, deadline.Token).ConfigureAwait(false);
                            body = Json.Deserialize(text) as Dictionary<string, object>;
                        }
                        catch (IOException) when (code == 429) { } // Preserve the HTTP status and Retry-After header.
                        catch (FormatException) { }
                    }
                    var retry = response.Headers.RetryAfter?.Delta?.TotalSeconds ??
                        (response.Headers.RetryAfter?.Date.HasValue == true ? (response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow).TotalSeconds : 0);
                    var parameters = body != null && body.TryGetValue("parameters", out var p) ? p as Dictionary<string, object> : null;
                    object delay = null;
                    if (body != null && body.TryGetValue("retry_after", out delay) || parameters != null && parameters.TryGetValue("retry_after", out delay))
                        if (double.TryParse(Convert.ToString(delay, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && !double.IsNaN(seconds) && !double.IsInfinity(seconds)) retry = Math.Max(retry, seconds);
                    if (code == 200 && Acknowledged(body)) return Result(DeliveryStatus.Accepted, code: code);
                    if (code == 200 && body != null && body.TryGetValue("error_code", out var error))
                        int.TryParse(Convert.ToString(error, CultureInfo.InvariantCulture), out code);
                    if (code < 300) return Result(DeliveryStatus.Rejected, RelayFailure.InvalidAcknowledgement, code);
                    var failure = code == 401 || code == 403 ? RelayFailure.Authentication : code == 429 ? RelayFailure.RateLimit :
                        code == 408 || code == 504 ? RelayFailure.Timeout : code >= 500 ? RelayFailure.Server :
                        code == 400 || code == 413 || code == 415 || code == 422 ? RelayFailure.Payload : RelayFailure.Endpoint;
                    return Result(code == 408 || code == 429 || code >= 500 ? DeliveryStatus.Retry : DeliveryStatus.Rejected, failure, code, retry);
                }
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested) { return Result(DeliveryStatus.Retry, RelayFailure.Timeout); }
            catch (HttpRequestException) { return Result(DeliveryStatus.Retry, RelayFailure.Network); }
            catch (IOException) { return Result(DeliveryStatus.Rejected, RelayFailure.InvalidAcknowledgement); }
            }
        }
        private static async Task<string> BoundedBody(HttpContent content, CancellationToken token)
        {
            using (var stream = await content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var data = new MemoryStream())
            {
                var buffer = new byte[4096]; int count;
                while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0)
                {
                    if (data.Length + count > 65536) throw new IOException("Response cap.");
                    data.Write(buffer, 0, count);
                }
                return Encoding.UTF8.GetString(data.ToArray());
            }
        }
        protected RelayResult Result(DeliveryStatus status, RelayFailure failure = RelayFailure.None, int code = 0, double retry = 0)
            => new RelayResult(status, retry, code, failure, Target);
        protected static StringContent JsonContent(object value) => new StringContent(Json.Serialize(value), Encoding.UTF8, "application/json");
        protected static void FilePart(MultipartFormDataContent parts, string name, string file, string mime, byte[] bytes)
        {
            if(bytes==null || bytes.Length>1024*1024)throw new ArgumentException("Direct file cap is 1 MiB.");
            var content = new ByteArrayContent(bytes); content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mime); parts.Add(content, name, file);
        }
        public void Dispose() => client.Dispose();
    }
}
#endif
