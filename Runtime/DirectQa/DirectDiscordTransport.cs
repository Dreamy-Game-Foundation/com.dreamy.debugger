#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
namespace Dreamy.Debugger.Reporting.DirectQa
{
    public sealed class DirectDiscordTransport : DirectQaTransport
    {
        private static readonly Regex Webhook = new Regex(@"\Ahttps://discord\.com/api(?:/v\d+)?/webhooks/([0-9]+)/[A-Za-z0-9_-]+\z");
        public static bool IsValidWebhook(string value) => value != null && Webhook.IsMatch(value);
        private static Uri Validate(string value)
        { if (!IsValidWebhook(value)) throw new ArgumentException("Enter a Discord incoming webhook without query parameters."); return new Uri(value + "?wait=true"); }
        public DirectDiscordTransport(string webhook, string receipts, HttpMessageHandler handler = null)
            : base(Validate(webhook), "discord/" + Webhook.Match(webhook).Groups[1].Value, receipts, handler) { }
        protected override DeliveryTarget Target => DeliveryTarget.Discord;
        protected override int Steps(ReportBundle bundle) => 1;
        protected override bool Acknowledged(Dictionary<string, object> body) => body != null && body.TryGetValue("id", out var id) && id is string text && Regex.IsMatch(text, @"\A[0-9]+\z");
        protected override HttpRequestMessage TextRequest(string message) => new HttpRequestMessage(HttpMethod.Post, Endpoint)
        { Content = JsonContent(new Dictionary<string, object> { ["content"] = message, ["allowed_mentions"] = new Dictionary<string, object> { ["parse"] = new object[0] } }) };
        protected override HttpRequestMessage ReportRequest(ReportBundle bundle, int step)
        {
            var parts = new MultipartFormDataContent();
            var files = new List<object> { new Dictionary<string, object> { ["id"] = 0, ["filename"] = "report.json" } };
            if (bundle.ImageBytes > 0) files.Add(new Dictionary<string, object> { ["id"] = 1, ["filename"] = "screenshot.jpg" });
            var summary = new DirectReportPresentation(bundle);
            parts.Add(JsonContent(new Dictionary<string, object> { ["embeds"] = new[] { summary.DiscordEmbed(bundle.ImageBytes > 0) },
                ["allowed_mentions"] = new Dictionary<string, object> { ["parse"] = new object[0] }, ["attachments"] = files }), "payload_json");
            FilePart(parts, "files[0]", "report.json", "application/json", Encoding.UTF8.GetBytes(RelayTransport.SerializeReportJson(bundle)));
            if (bundle.ImageBytes > 0) FilePart(parts, "files[1]", "screenshot.jpg", "image/jpeg", bundle.CopyImage());
            return new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = parts };
        }
    }
}
#endif
