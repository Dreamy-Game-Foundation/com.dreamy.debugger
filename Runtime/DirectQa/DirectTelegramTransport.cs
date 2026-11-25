#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
namespace Dreamy.Debugger.Reporting.DirectQa
{
    public sealed class DirectTelegramTransport : DirectQaTransport
    {
        private readonly string chat;
        private static Uri Validate(string token, string chat)
        {
            if (token == null || !Regex.IsMatch(token, @"\A[0-9]+:[A-Za-z0-9_-]{20,}\z") || chat == null || !Regex.IsMatch(chat, @"\A(?:-?[0-9]+|@[A-Za-z0-9_]+)\z"))
                throw new ArgumentException("Enter a Telegram bot token and chat ID.");
            return new Uri("https://api.telegram.org/bot" + token + "/");
        }
        public DirectTelegramTransport(string token, string chat, string receipts, HttpMessageHandler handler = null)
            : base(Validate(token, chat), "telegram/" + token.Split(':')[0] + "/" + chat, receipts, handler) { this.chat = chat; }
        protected override DeliveryTarget Target => DeliveryTarget.Telegram;
        protected override int Steps(ReportBundle bundle) => bundle.ImageBytes > 0 ? 2 : 1;
        protected override bool Acknowledged(Dictionary<string, object> body) => body != null && body.TryGetValue("ok", out var ok) && ok is bool success && success &&
            body.TryGetValue("result", out var result) && result is Dictionary<string, object> message && message.TryGetValue("message_id", out var id) && id is long number && number > 0;
        protected override HttpRequestMessage TextRequest(string message) => new HttpRequestMessage(HttpMethod.Post, new Uri(Endpoint.AbsoluteUri + "sendMessage"))
        { Content = JsonContent(new Dictionary<string, object> { ["chat_id"] = chat, ["text"] = message }) };
        protected override HttpRequestMessage ReportRequest(ReportBundle bundle, int step)
        {
            var parts = new MultipartFormDataContent();
            parts.Add(new StringContent(chat), "chat_id");
            parts.Add(new StringContent("Dreamy Debugger · Report ID: " + bundle.Report.Id + (step == 0 ? " · report JSON" : " · screenshot")), "caption");
            FilePart(parts, "document", step == 0 ? "report.json" : "screenshot.jpg", step == 0 ? "application/json" : "image/jpeg",
                step == 0 ? Encoding.UTF8.GetBytes(RelayTransport.SerializeReportJson(bundle)) : bundle.CopyImage());
            return new HttpRequestMessage(HttpMethod.Post, new Uri(Endpoint.AbsoluteUri + "sendDocument")) { Content = parts };
        }
    }
}
#endif
