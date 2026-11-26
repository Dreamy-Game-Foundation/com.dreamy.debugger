#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Dreamy.Debugger.Reporting.DirectQa
{
    internal sealed class DirectReportPresentation
    {
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly Regex HiddenCharacters = new Regex(@"[\u0000-\u0008\u000e-\u001f\u007f\u202a-\u202e\u2066-\u2069]");
        private static readonly Regex Whitespace = new Regex(@"\s+");
        private static readonly Regex ExceptionName = new Regex(@"^([\w.]*Exception):\s*");
        private static readonly Regex SourceLocation = new Regex(@"\s+\(at\s|\s+\[0x[\da-fA-F]+\]|\s+in\s+(?=[<\\/]|[A-Za-z]:)");
        private static readonly Regex MarkdownControl = new Regex(@"[\\`*_\[\]<>#|~]");
        private static readonly HashSet<string> ContextKeys = new HashSet<string> { "app", "version", "unity", "platform", "device", "os", "scene" };

        private readonly string subject, category, platformVersion, scene, device, unity, fps, memory, severity, repeats, stack, issueKey, build, reportId;
        private readonly DateTime createdUtc;
        private readonly int color;

        internal DirectReportPresentation(ReportBundle bundle)
        {
            var context = new Dictionary<string, string>();
            using (var reader = new StringReader(bundle.Context))
            {
                while (reader.ReadLine() is string line)
                {
                    var split = line.IndexOf('=');
                    if (split <= 0) break;
                    var key = line.Substring(0, split);
                    if (!ContextKeys.Contains(key) || context.ContainsKey(key)) break;
                    context.Add(key, line.Substring(split + 1).Trim());
                }
            }
            string Read(string key) => context.TryGetValue(key, out var value) ? value : "";
            platformVersion = Join(" · ", Preview(Read("platform"), 24), Preview(Read("version"), 24));
            var scenePath = Read("scene").Replace('\\', '/');
            var sceneName = scenePath.Substring(scenePath.LastIndexOf('/') + 1);
            if (sceneName.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
                sceneName = sceneName.Substring(0, sceneName.Length - ".unity".Length);
            scene = Preview(sceneName, 60);
            device = Join(" · ", Preview(Read("device"), 54), Preview(Read("os"), 36));
            unity = Preview(Read("unity"), 24);
            build = Preview(bundle.Report.BuildId, 48);
            reportId = bundle.Report.Id;
            createdUtc = bundle.Report.CreatedUtc;

            var details = bundle.Details;
            var automatic = details?.Source == "auto";
            category = bundle.Report.IsSynthetic ? "QA test" : automatic ? "Automatic error" : "Manual report";
            color = bundle.Report.IsSynthetic ? 0x78849A : automatic ? 0xE25568 : 0x54C7A0;
            using var message = new StringReader(bundle.Report.Message);
            string first;
            do { first = message.ReadLine(); } while (first != null && string.IsNullOrWhiteSpace(first));
            subject = Preview(first, 160);
            if (subject.Length == 0) subject = "Bug report";

            if (details == null) return;
            if (details.Fps > 0)
                fps = details.Fps >= 0.1 ? details.Fps.ToString("F1", Invariant) + " FPS" : "< 0.1 FPS";
            if (details.ReservedBytes > 0)
                memory = string.Format(Invariant, "{0:0} / {1:0} MiB", details.AllocatedBytes / 1048576d, details.ReservedBytes / 1048576d);
            severity = Preview(details.Severity, 48);
            if (automatic)
            {
                var exception = ExceptionName.Match(subject);
                if (exception.Success)
                {
                    severity = Preview(exception.Groups[1].Value, 48);
                    if (subject.Length > exception.Length) subject = subject.Substring(exception.Length);
                }
                if (severity.Length == 0) severity = "Error";
                repeats = details.OccurrenceCount.ToString(Invariant);
            }
            var frames = Frames(details.StackTrace);
            stack = string.Join("\n", frames.Select(frame => Preview(frame, 105)));
            if (automatic && frames.Count > 0 && Read("app").Length > 0)
            {
                using var sha = SHA256.Create();
                var identity = string.Join("\n", Read("app"), severity, Whitespace.Replace(frames[0], ""));
                issueKey = "DD-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity)), 0, 6).Replace("-", "");
            }
        }

        public Dictionary<string, object> DiscordEmbed(bool hasImage)
        {
            string Md(string value) => MarkdownControl.Replace(value, match => "\\" + match.Value);
            string Labeled(string label, string value) => string.IsNullOrEmpty(value) ? "" : "**" + label + "** " + Md(value);
            var fields = new List<object>();
            void Add(string name, string value, bool inline)
            {
                if (!string.IsNullOrEmpty(value))
                    fields.Add(new Dictionary<string, object> { ["name"] = name, ["value"] = value, ["inline"] = inline });
            }
            Add("Environment", Join("\n", Md(platformVersion), Md(device), Labeled("Scene", scene), Labeled("Unity", unity)), true);
            Add("Performance", Join("\n", fps, Labeled("Memory", memory)), true);
            Add("Error", Join(" · ", severity == null ? "" : Md(severity), repeats == null ? "" : repeats + " occurrences"), false);
            if (!string.IsNullOrEmpty(stack)) Add("Stack trace", "```\n" + stack.Replace("`", "ˋ") + "\n```", false);
            Add("Report ID", "`" + reportId + "`", true);
            Add("Issue key", issueKey == null ? "" : "`" + issueKey + "`", true);
            Add("Build", Md(build), false);

            var embed = new Dictionary<string, object>
            {
                ["author"] = new Dictionary<string, object> { ["name"] = "Dreamy Debugger · Direct QA · " + category },
                ["description"] = "**" + Md(subject) + "**",
                ["color"] = color,
                ["fields"] = fields,
                ["timestamp"] = createdUtc.ToString("O", Invariant)
            };
            if (hasImage) embed["image"] = new Dictionary<string, object> { ["url"] = "attachment://screenshot.jpg" };
            return embed;
        }

        public string TelegramCaption()
        {
            const int captionLimit = 1024;
            var caption = new StringBuilder();
            var length = 0;
            void Line(string text, string markup)
            {
                if (length + text.Length + 1 > captionLimit) return;
                caption.Append(markup).Append('\n');
                length += text.Length + 1;
            }
            void Field(string label, string value, bool code = false)
            {
                if (string.IsNullOrEmpty(value)) return;
                var body = Html(value);
                Line(label + " " + value, "<b>" + label + "</b> " + (code ? "<code>" + body + "</code>" : body));
            }
            Line(subject, "<b>" + Html(subject) + "</b>");
            var byline = "Dreamy Debugger · Direct QA · " + category;
            Line(byline, "<i>" + byline + "</i>");
            Line("", "");
            Field("Environment", platformVersion);
            Field("Device", device);
            Field("Scene", scene);
            Field("Performance", Join(" · ", fps, memory));
            Field("Error", Join(" · ", severity, repeats == null ? "" : repeats + " occurrences"));
            Line("", "");
            Field("Report ID", reportId, true);
            Field("Issue key", issueKey, true);
            Field("Build", build);
            var date = createdUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", Invariant);
            Line(date, "<i>" + date + "</i>");
            const string stackLabel = "Stack trace";
            var remaining = captionLimit - length - stackLabel.Length - 3;
            if (!string.IsNullOrEmpty(stack) && remaining > 15)
            {
                Line("", "");
                Line(stackLabel, "<b>" + stackLabel + "</b>");
                var clipped = Clip(stack, remaining);
                Line(clipped, "<pre>" + Html(clipped) + "</pre>");
            }
            return caption.ToString().TrimEnd();
        }

        public static string TelegramScreenshotCaption(string reportId) =>
            "<b>Dreamy Debugger · Original screenshot</b>\n<b>Report ID</b> <code>" + reportId + "</code>";

        private static List<string> Frames(string trace)
        {
            var frames = new List<string>(3);
            using var reader = new StringReader(trace ?? "");
            while (frames.Count < 3 && reader.ReadLine() is string line)
            {
                var frame = line.Trim();
                if (frame.StartsWith("at ", StringComparison.Ordinal)) frame = frame.Substring(3);
                if (frame.StartsWith("UnityEngine.Debug", StringComparison.Ordinal) ||
                    frame.StartsWith("UnityEngine.Logger", StringComparison.Ordinal) ||
                    frame.StartsWith("Dreamy.Debugger.Presentation.Services.Implementation.StandardConsoleService", StringComparison.Ordinal)) continue;
                var source = SourceLocation.Match(frame);
                if (source.Success) frame = frame.Substring(0, source.Index);
                if (frame.IndexOf('(') > 0) frames.Add(frame);
            }
            return frames;
        }

        private static string Html(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
        private static string Join(string separator, params string[] values) => string.Join(separator, values.Where(value => !string.IsNullOrEmpty(value)));
        private static string Preview(string value, int limit) => Clip(Whitespace.Replace(HiddenCharacters.Replace(value ?? "", ""), " ").Trim(), limit);
        private static string Clip(string value, int limit)
        {
            if (value.Length <= limit) return value;
            var end = limit - 1;
            if (end > 0 && char.IsHighSurrogate(value[end - 1])) end--;
            return value.Substring(0, end).TrimEnd() + "…";
        }
    }
}
#endif
